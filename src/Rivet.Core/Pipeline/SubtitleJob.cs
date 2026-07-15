using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;

namespace Rivet.Core.Pipeline;

/// <summary>One end-to-end request: this video in, that captioned video out, styled like so.</summary>
public sealed record SubtitleJob
{
    public required string InputVideoPath { get; init; }
    public required string OutputVideoPath { get; init; }
    public TranscriptionOptions Transcription { get; init; } = new();
    public SubtitleStyle Style { get; init; } = new();

    /// <summary>Run the audio through Demucs first so whisper hears voice, not music (ADR 0009).</summary>
    public bool IsolateVocals { get; init; }
}

public enum JobStage
{
    Preparing,
    IsolatingVocals,
    ExtractingAudio,
    Transcribing,
    Rendering,
    Burning,
    Done
}

/// <param name="Fraction">0..1 within the current stage. Stages are not equal length; the UI
/// shows the stage name so the bar resetting between them is not surprising.</param>
public readonly record struct JobProgress(JobStage Stage, double Fraction)
{
    public string Label => Stage switch
    {
        JobStage.Preparing => "Reading the video…",
        JobStage.IsolatingVocals => "Isolating vocals (this is slow)…",
        JobStage.ExtractingAudio => "Extracting audio…",
        JobStage.Transcribing => "Transcribing speech…",
        JobStage.Rendering => "Building captions…",
        JobStage.Burning => "Rendering the video…",
        _ => "Done"
    };
}
