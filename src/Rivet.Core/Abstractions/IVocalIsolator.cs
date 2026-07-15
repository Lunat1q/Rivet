namespace Rivet.Core.Abstractions;

/// <summary>
/// Strips music and ambient noise, leaving (mostly) the voice — so whisper transcribes clean
/// speech instead of speech-plus-a-song. Optional and off by default: it needs a heavy external
/// tool (Demucs, ADR 0009) and is only worth its runtime on noisy or musical clips.
/// </summary>
public interface IVocalIsolator
{
    /// <summary>
    /// Isolates the vocal track of a media file and returns a path to the isolated audio.
    /// The original video is untouched — only what whisper hears changes.
    /// </summary>
    Task<string> IsolateVocalsAsync(
        string inputMediaPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
