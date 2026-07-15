using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;

namespace Rivet.App.ViewModels;

/// <summary>A labelled option for a ComboBox — the name is what the user reads, the value is what
/// the engine gets. ToString drives the ComboBox display.</summary>
public sealed record Choice<T>(string Name, T Value, string? Detail = null)
{
    public override string ToString() => Name;
}

/// <summary>Every drop-down's options in one place. Plain-language names, engine values behind them.</summary>
public static class Choices
{
    public static readonly IReadOnlyList<Choice<WhisperModel>> Qualities =
    [
        new("Fast", WhisperModel.Small, "Quick, small download. Good for clear speech."),
        new("Balanced", WhisperModel.LargeV3Turbo, "The sweet spot. Recommended."),
        new("Best", WhisperModel.LargeV3, "Most accurate, slowest, largest download.")
    ];

    public static readonly IReadOnlyList<Choice<ComputeBackend>> Processings =
    [
        new("Automatic", ComputeBackend.Auto, "Use the GPU if there is one."),
        new("GPU", ComputeBackend.Gpu, "Fastest, if you have a discrete graphics card."),
        new("CPU", ComputeBackend.Cpu, "Works everywhere; slower on long clips.")
    ];

    public static readonly IReadOnlyList<Choice<CaptionPosition>> Positions =
    [
        new("Middle", CaptionPosition.Center),
        new("Bottom", CaptionPosition.Bottom),
        new("Top", CaptionPosition.Top)
    ];

    // libass renders with the system's installed fonts; these are the safe, ubiquitous ones.
    public static readonly IReadOnlyList<Choice<string>> Fonts =
    [
        new("Arial", "Arial"),
        new("Impact", "Impact"),
        new("Verdana", "Verdana"),
        new("Georgia", "Georgia"),
        new("Comic Sans MS", "Comic Sans MS")
    ];

    public static readonly IReadOnlyList<Choice<string>> PrimaryColors =
    [
        new("White", "#FFFFFF"),
        new("Black", "#101010"),
        new("Yellow", "#F5E000")
    ];

    public static readonly IReadOnlyList<Choice<string>> HighlightColors =
    [
        new("Yellow", "#F5E000"),
        new("Green", "#39E58C"),
        new("Cyan", "#39D3E5"),
        new("Pink", "#E5399A"),
        new("Orange", "#F58A39")
    ];
}
