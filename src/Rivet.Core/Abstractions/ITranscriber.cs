using Rivet.Core.Audio;
using Rivet.Core.Transcription;

namespace Rivet.Core.Abstractions;

/// <summary>
/// Turns a whole 16 kHz mono float32 track into a word-timed transcript.
/// The implementation is whisper.cpp (ADR 0001); nothing above this interface knows that.
/// Instances are NOT thread-safe — whisper.cpp contexts aren't.
/// </summary>
public interface ITranscriber : IAsyncDisposable
{
    /// <param name="progress">0..1 over the clip, as segments come back. Approximate.</param>
    Task<Transcript> TranscribeAsync(
        AudioBuffer audio,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Loads the model once and hands out a transcriber. Model load is the expensive part.</summary>
public interface ITranscriberFactory : IAsyncDisposable
{
    Task<ITranscriber> CreateAsync(TranscriptionOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Which native whisper.cpp backend is in use. The single biggest factor in how long a
/// render takes (GPU vs CPU is ~60x), so the UI has to be able to say it out loud.
/// </summary>
public interface ITranscriptionBackend
{
    /// <summary>Human-readable, e.g. "GPU (Vulkan)" or "CPU". Only meaningful once a model is loaded.</summary>
    string Backend { get; }

    bool IsGpu { get; }
}

/// <summary>Locates or downloads a whisper.cpp GGML model file.</summary>
public interface IWhisperModelProvider
{
    /// <param name="progress">0..1 download progress. Not reported when the model is cached.</param>
    Task<string> GetModelPathAsync(
        WhisperModel model,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    bool IsDownloaded(WhisperModel model);
}
