using Microsoft.Extensions.Logging;
using Rivet.Core.Abstractions;
using Rivet.Core.Media;
using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;

namespace Rivet.Core.Pipeline;

/// <summary>
/// The app in two halves that the UI drives separately: <see cref="TranscribeAsync" /> turns a
/// video into a word-timed transcript (optionally isolating vocals first), and
/// <see cref="RenderAsync" /> burns a — possibly user-edited — transcript back in. Between them
/// sits the editor. <see cref="RunAsync" /> chains both for a one-shot, no-editing job.
///
/// Everything it calls is an interface, so the ffmpeg, whisper.cpp and Demucs choices stay
/// swappable (ADR 0002, 0001, 0009).
/// </summary>
public sealed class SubtitlePipeline
{
    private readonly IMediaProcessor _media;
    private readonly ITranscriberFactory _transcribers;
    private readonly IVocalIsolator _vocals;
    private readonly ILogger<SubtitlePipeline> _logger;

    public SubtitlePipeline(
        IMediaProcessor media,
        ITranscriberFactory transcribers,
        IVocalIsolator vocals,
        ILogger<SubtitlePipeline> logger)
    {
        _media = media;
        _transcribers = transcribers;
        _vocals = vocals;
        _logger = logger;
    }

    /// <summary>Probe + (optional) isolate + extract + transcribe. The video is not touched.</summary>
    public async Task<(Transcript Transcript, VideoInfo Video)> TranscribeAsync(
        SubtitleJob job,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        void Report(JobStage stage, double fraction) => progress?.Report(new JobProgress(stage, fraction));

        Report(JobStage.Preparing, 0);
        var info = await _media.ProbeAsync(job.InputVideoPath, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Video {W}x{H}, {Duration}", info.Width, info.Height, info.Duration);

        var audioSource = job.InputVideoPath;
        if (job.IsolateVocals)
            audioSource = await _vocals.IsolateVocalsAsync(
                job.InputVideoPath,
                new Progress<double>(f => Report(JobStage.IsolatingVocals, f)),
                cancellationToken).ConfigureAwait(false);

        var audio = await _media.ExtractAudioAsync(
            audioSource,
            new Progress<double>(f => Report(JobStage.ExtractingAudio, f)),
            cancellationToken).ConfigureAwait(false);

        await using var transcriber = await _transcribers
            .CreateAsync(job.Transcription, cancellationToken).ConfigureAwait(false);

        var transcript = await transcriber.TranscribeAsync(
            audio,
            new Progress<double>(f => Report(JobStage.Transcribing, f)),
            cancellationToken).ConfigureAwait(false);

        if (transcript.IsEmpty)
            throw new InvalidOperationException(
                "No speech was found in this video, so there is nothing to caption.");

        return (transcript, info);
    }

    /// <summary>Lay out a transcript as captions and burn them into the video.</summary>
    public async Task RenderAsync(
        SubtitleJob job,
        Transcript transcript,
        VideoInfo video,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        void Report(JobStage stage, double fraction) => progress?.Report(new JobProgress(stage, fraction));

        Report(JobStage.Rendering, 0);
        var assPath = await WriteAssAsync(transcript, job.Style, video, cancellationToken).ConfigureAwait(false);
        Report(JobStage.Rendering, 1);

        try
        {
            await _media.BurnSubtitlesAsync(
                job.InputVideoPath, assPath, job.OutputVideoPath,
                new Progress<double>(f => Report(JobStage.Burning, f)),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(assPath);
        }

        Report(JobStage.Done, 1);
        _logger.LogInformation("Wrote captioned video to {Path}", job.OutputVideoPath);
    }

    /// <summary>Transcribe then render, with no chance to edit in between.</summary>
    public async Task RunAsync(
        SubtitleJob job,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var (transcript, info) = await TranscribeAsync(job, progress, cancellationToken).ConfigureAwait(false);
        await RenderAsync(job, transcript, info, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the styled ASS to a temp file and returns its path. Shared by render and preview.</summary>
    public static async Task<string> WriteAssAsync(
        Transcript transcript,
        SubtitleStyle style,
        VideoInfo video,
        CancellationToken cancellationToken = default)
    {
        var captions = CaptionBuilder.Build(transcript, style.MaxWordsPerCaption);
        var ass = AssSubtitleWriter.Write(captions, style, video);
        var assPath = Path.Combine(Path.GetTempPath(), $"rivet-{Guid.NewGuid():N}.ass");
        await File.WriteAllTextAsync(assPath, ass, cancellationToken).ConfigureAwait(false);
        return assPath;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not remove temp subtitle file {Path}", path);
        }
    }
}
