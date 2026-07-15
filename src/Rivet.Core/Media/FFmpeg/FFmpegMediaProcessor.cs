using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rivet.Core.Abstractions;
using Rivet.Core.Audio;

namespace Rivet.Core.Media.FFmpeg;

/// <summary>
/// IMediaProcessor by shelling out to ffmpeg/ffprobe (ADR 0002). Audio comes back as raw
/// f32le on stdout — no temp WAV — and subtitles are burned with libass via the subtitles
/// filter, which is the same path every "hardcoded captions" tool uses because it is the one
/// that renders ASS styling (karaoke, outline, positioning) correctly.
/// </summary>
public sealed class FFmpegMediaProcessor : IMediaProcessor
{
    private readonly FFmpegLocator _locator;
    private readonly ILogger<FFmpegMediaProcessor> _logger;

    public FFmpegMediaProcessor(FFmpegLocator locator, ILogger<FFmpegMediaProcessor> logger)
    {
        _locator = locator;
        _logger = logger;
    }

    public async Task<VideoInfo> ProbeAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var (stdout, exit) = await RunCapturingAsync(
            _locator.FFprobePath,
            ["-v", "error", "-select_streams", "v:0",
             "-show_entries", "stream=width,height,r_frame_rate:format=duration",
             "-of", "json", videoPath],
            cancellationToken).ConfigureAwait(false);

        if (exit != 0)
            throw new InvalidOperationException($"ffprobe failed ({exit}) on {videoPath}.");

        using var doc = JsonDocument.Parse(stdout);
        var stream = doc.RootElement.GetProperty("streams")[0];

        var width = stream.GetProperty("width").GetInt32();
        var height = stream.GetProperty("height").GetInt32();
        var fps = ParseFraction(stream.GetProperty("r_frame_rate").GetString());
        var duration = double.Parse(
            doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!,
            CultureInfo.InvariantCulture);

        return new VideoInfo(width, height, TimeSpan.FromSeconds(duration), fps);
    }

    public async Task<AudioBuffer> ExtractAudioAsync(
        string videoPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0d);

        // Raw 16 kHz mono float32 straight to stdout: exactly what whisper.cpp ingests, no
        // intermediate file to write and clean up.
        var start = NewStartInfo(_locator.FFmpegPath,
            ["-v", "error", "-i", videoPath,
             "-vn", "-ac", AudioConstants.Channels.ToString(),
             "-ar", AudioConstants.SampleRate.ToString(),
             "-f", "f32le", "pipe:1"]);

        using var process = Start(start);

        using var buffer = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(buffer, cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        await copy.ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg audio extraction failed ({process.ExitCode}): {await stderr.ConfigureAwait(false)}");

        var samples = ToFloats(buffer.GetBuffer(), (int)buffer.Length);
        progress?.Report(1d);
        return new AudioBuffer(samples);
    }

    public async Task BurnSubtitlesAsync(
        string videoPath,
        string assPath,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var info = await ProbeAsync(videoPath, cancellationToken).ConfigureAwait(false);
        var totalUs = info.Duration.TotalSeconds * 1_000_000d;

        // Pass the .ass by *filename only*, with ffmpeg's working directory set to its folder.
        // The subtitles filter's own parser treats ':' and '\' as syntax; a bare filename with
        // no drive letter sidesteps the Windows path-escaping minefield entirely.
        var assDir = Path.GetDirectoryName(Path.GetFullPath(assPath))!;
        var assName = Path.GetFileName(assPath);

        var start = NewStartInfo(_locator.FFmpegPath,
            ["-y", "-v", "error", "-progress", "pipe:1", "-nostats",
             "-i", Path.GetFullPath(videoPath),
             "-vf", $"subtitles={assName}",
             "-c:a", "copy", Path.GetFullPath(outputPath)]);
        start.WorkingDirectory = assDir;

        using var process = Start(start);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        // ffmpeg's -progress stream is key=value lines; out_time_us against the known duration
        // is the only honest percentage available for a re-encode.
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (totalUs > 0 && line.StartsWith("out_time_us=", StringComparison.Ordinal)
                && long.TryParse(line.AsSpan("out_time_us=".Length), out var us))
                progress?.Report(Math.Clamp(us / totalUs, 0d, 1d));
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg subtitle burn failed ({process.ExitCode}): {await stderr.ConfigureAwait(false)}");

        progress?.Report(1d);
    }

    public async Task<string> RenderPreviewFrameAsync(
        string videoPath,
        string assPath,
        TimeSpan at,
        CancellationToken cancellationToken = default)
    {
        var assDir = Path.GetDirectoryName(Path.GetFullPath(assPath))!;
        var assName = Path.GetFileName(assPath);
        var outputPath = Path.Combine(Path.GetTempPath(), $"rivet-preview-{Guid.NewGuid():N}.png");

        // -ss *after* -i: accurate seek, so the subtitles filter sees the real timestamp and shows
        // the caption that belongs at `at`. Decoding up to the seek point is fine — previews are of
        // short-form clips. Filename-only + working directory dodges path escaping (see burn).
        var seconds = at.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
        var start = NewStartInfo(_locator.FFmpegPath,
            ["-y", "-v", "error", "-i", Path.GetFullPath(videoPath),
             "-ss", seconds, "-frames:v", "1",
             "-vf", $"subtitles={assName}", Path.GetFullPath(outputPath)]);
        start.WorkingDirectory = assDir;

        using var process = Start(start);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        _ = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg preview failed ({process.ExitCode}): {await stderr.ConfigureAwait(false)}");

        return outputPath;
    }

    private static float[] ToFloats(byte[] bytes, int length)
    {
        var count = length / sizeof(float);
        var samples = new float[count];
        for (var i = 0; i < count; i++)
            samples[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        return samples;
    }

    private static double ParseFraction(string? fraction)
    {
        // r_frame_rate is "num/den", e.g. "30000/1001".
        if (fraction is null)
            return 0d;
        var parts = fraction.Split('/');
        return parts.Length == 2 && double.TryParse(parts[1], CultureInfo.InvariantCulture, out var den) && den != 0
            ? double.Parse(parts[0], CultureInfo.InvariantCulture) / den
            : 0d;
    }

    private async Task<(string StdOut, int ExitCode)> RunCapturingAsync(
        string exe, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        using var process = Start(NewStartInfo(exe, args));
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        _ = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (await stdout.ConfigureAwait(false), process.ExitCode);
    }

    private static ProcessStartInfo NewStartInfo(string exe, IReadOnlyList<string> args)
    {
        var start = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        return start;
    }

    private Process Start(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start)
                   ?? throw new InvalidOperationException("ffmpeg process did not start.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "Could not launch {Exe}", start.FileName);
            throw new InvalidOperationException(
                $"Could not run '{start.FileName}'. Install ffmpeg and make sure it is on your PATH " +
                "(or in C:\\ffmpeg).", ex);
        }
    }
}
