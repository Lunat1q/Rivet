using Rivet.Core.Audio;
using Rivet.Core.Media;

namespace Rivet.Core.Abstractions;

/// <summary>
/// Everything Rivet needs from the video container itself: what shape it is, its audio pulled
/// out as whisper-ready PCM, and the finished subtitles burned back in. The implementation
/// shells out to ffmpeg/ffprobe (ADR 0002); nothing above this interface knows that.
/// </summary>
public interface IMediaProcessor
{
    /// <summary>Dimensions and duration — needed to size the subtitle canvas correctly.</summary>
    Task<VideoInfo> ProbeAsync(string videoPath, CancellationToken cancellationToken = default);

    /// <summary>Decode the video's audio to 16 kHz mono float32, the only format whisper.cpp reads.</summary>
    Task<AudioBuffer> ExtractAudioAsync(
        string videoPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Burn an ASS subtitle file into the video (libass via ffmpeg's subtitles filter) and
    /// write the result. Re-encodes video; copies audio.
    /// </summary>
    Task BurnSubtitlesAsync(
        string videoPath,
        string assPath,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a single frame of the video with the subtitles burned on, at the given time, to a
    /// PNG. This is the editor's live preview — cheap enough to call as the user edits, because it
    /// decodes one frame, not the whole clip. Returns the path to a temporary PNG.
    /// </summary>
    Task<string> RenderPreviewFrameAsync(
        string videoPath,
        string assPath,
        TimeSpan at,
        CancellationToken cancellationToken = default);
}
