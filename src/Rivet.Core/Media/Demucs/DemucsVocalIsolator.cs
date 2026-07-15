using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rivet.Core.Abstractions;

namespace Rivet.Core.Media.Demucs;

/// <summary>
/// IVocalIsolator via Demucs (ADR 0009) — the same idea karaoke-forever uses Spleeter for, with
/// the better-maintained model. Runs Demucs in two-stem mode, which writes a `vocals` file under
/// its output folder; that file is what whisper then transcribes.
///
/// Demucs is not bundled (it drags in PyTorch): like ffmpeg it is located, not shipped. pip
/// installs it as a module more often than an exe, so this tries the `demucs` command first and
/// falls back to `python -m demucs`. A missing install surfaces as one clear error, and only when
/// the user actually turns isolation on.
/// </summary>
public sealed class DemucsVocalIsolator : IVocalIsolator
{
    private readonly IReadOnlyList<(string Exe, string[] Prefix)> _candidates;
    private readonly ILogger<DemucsVocalIsolator> _logger;

    public DemucsVocalIsolator(ILogger<DemucsVocalIsolator> logger, string? demucsPath = null)
    {
        _logger = logger;
        _candidates = demucsPath is not null
            ? [(demucsPath, [])]
            : [("demucs", []), ("python", ["-m", "demucs"]), ("python3", ["-m", "demucs"])];
    }

    public async Task<string> IsolateVocalsAsync(
        string inputMediaPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0d);

        var outDir = Path.Combine(Path.GetTempPath(), $"rivet-demucs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);

        // --two-stems=vocals: split vocals vs the rest only, ~2× faster than 4-stem and all we
        // need. --mp3 keeps the temp small.
        string[] demucsArgs = ["--two-stems", "vocals", "--mp3", "-o", outDir, inputMediaPath];

        _logger.LogInformation("Isolating vocals with Demucs → {OutDir}", outDir);

        using var process = Launch(demucsArgs);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        _ = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Demucs failed ({process.ExitCode}): {await stderr.ConfigureAwait(false)}");

        // Demucs writes <outDir>/<model>/<trackname>/vocals.{mp3,wav}. Names vary — find it.
        var vocals = Directory
            .EnumerateFiles(outDir, "vocals.*", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Demucs produced no vocals track.");

        progress?.Report(1d);
        _logger.LogInformation("Vocals isolated: {Path}", vocals);
        return vocals;
    }

    /// <summary>Starts the first Demucs invocation that launches; throws a clear error if none do.</summary>
    private Process Launch(string[] demucsArgs)
    {
        foreach (var (exe, prefix) in _candidates)
        {
            var start = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in prefix)
                start.ArgumentList.Add(arg);
            foreach (var arg in demucsArgs)
                start.ArgumentList.Add(arg);

            try
            {
                return Process.Start(start) ?? throw new InvalidOperationException("Demucs did not start.");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _logger.LogDebug("Demucs candidate '{Exe}' not launchable; trying next", exe);
            }
        }

        throw new InvalidOperationException(
            "Vocal isolation is on, but Demucs was not found. Install it (pip install demucs) and " +
            "make sure `demucs` or `python -m demucs` runs — or turn vocal isolation off.");
    }
}
