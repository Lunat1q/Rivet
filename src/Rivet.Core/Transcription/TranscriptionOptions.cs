namespace Rivet.Core.Transcription;

/// <summary>
/// Which processor whisper.cpp runs the model on. Not a native-library choice — the Vulkan
/// build carries CPU kernels too, so this is a per-model-load flag (ADR 0004) and can be
/// changed between jobs without restarting the app.
/// </summary>
public enum ComputeBackend
{
    /// <summary>GPU when one is present, CPU when not. whisper.cpp decides at load time.</summary>
    Auto,

    /// <summary>Insist on the GPU. Fails to start rather than silently running 60x slower.</summary>
    Gpu,

    /// <summary>Never touch the GPU. On a weak integrated GPU this is often the faster option.</summary>
    Cpu
}

/// <summary>Model sizes as published by ggml-org/whisper.cpp.</summary>
public enum WhisperModel
{
    Tiny,
    Base,
    Small,
    Medium,
    LargeV3,
    /// <summary>Best accuracy/speed trade-off. Default.</summary>
    LargeV3Turbo
}

public sealed record TranscriptionOptions
{
    public WhisperModel Model { get; init; } = WhisperModel.LargeV3Turbo;

    /// <summary>ISO-639-1 code, or "auto". Auto-detect is fine offline: the whole clip is present.</summary>
    public string Language { get; init; } = "auto";

    /// <summary>CPU or GPU. The single biggest lever on how long a render takes (ADR 0004).</summary>
    public ComputeBackend Backend { get; init; } = ComputeBackend.Auto;

    /// <summary>0 = let whisper.cpp pick (cores - 1).</summary>
    public int Threads { get; init; }

    /// <summary>
    /// Utterances whose whisper no-speech probability exceeds this are dropped.
    /// whisper.cpp answers confident nonsense when handed music or noise.
    /// </summary>
    public float NoSpeechThreshold { get; init; } = 0.6f;
}
