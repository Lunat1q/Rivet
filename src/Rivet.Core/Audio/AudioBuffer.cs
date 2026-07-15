namespace Rivet.Core.Audio;

/// <summary>
/// A whole video's audio, decoded to the only format whisper.cpp accepts: 16 kHz mono
/// float32 in [-1, 1]. Rivet is offline — it holds the entire track in memory rather than
/// streaming, because whisper.cpp transcribes a finite buffer anyway and a short-form clip
/// is minutes, not hours (ADR 0003).
/// </summary>
/// <param name="Samples">PCM samples for the full track.</param>
public readonly record struct AudioBuffer(float[] Samples)
{
    public TimeSpan Duration => AudioConstants.DurationOf(Samples.Length);
}

public static class AudioConstants
{
    /// <summary>whisper.cpp is hard-wired to 16 kHz. Not a preference.</summary>
    public const int SampleRate = 16_000;

    public const int Channels = 1;

    public static TimeSpan DurationOf(int sampleCount) =>
        TimeSpan.FromSeconds((double)sampleCount / SampleRate);
}
