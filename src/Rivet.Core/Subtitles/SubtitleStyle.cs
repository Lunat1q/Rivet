namespace Rivet.Core.Subtitles;

/// <summary>Where the caption block sits on the frame. Numpad-style, like ASS alignment.</summary>
public enum CaptionPosition
{
    Top,
    Center,
    Bottom
}

/// <summary>
/// Every knob the user turns to make the captions theirs. Sizes that must scale with the
/// clip are expressed as a percentage of video height, not pixels — a 6% font is the same
/// visual weight on a 720p export and a 4K one, which is the whole point of letting people
/// re-use a preset across clips.
/// </summary>
public sealed record SubtitleStyle
{
    public string FontName { get; init; } = "Arial";
    public bool Bold { get; init; } = true;

    /// <summary>Font height as a percentage of video height. ~7% is the loud short-form default.</summary>
    public double FontSizePercent { get; init; } = 7.0;

    /// <summary>Resting word colour, #RRGGBB.</summary>
    public string PrimaryColor { get; init; } = "#FFFFFF";

    /// <summary>The word being spoken *right now*, #RRGGBB. The viral "one word pops" effect.</summary>
    public string HighlightColor { get; init; } = "#F5E000";

    public string OutlineColor { get; init; } = "#000000";

    /// <summary>Outline thickness as a percentage of video height. A thick outline is what keeps
    /// text legible over any footage.</summary>
    public double OutlineWidthPercent { get; init; } = 0.5;

    /// <summary>How much the active word grows, as a percentage. 100 = no growth.</summary>
    public int HighlightScalePercent { get; init; } = 115;

    /// <summary>SHOUTING is the short-form convention. On by default.</summary>
    public bool Uppercase { get; init; } = true;

    /// <summary>Words shown on screen at once. 1-4 is the readable range at this size.</summary>
    public int MaxWordsPerCaption { get; init; } = 3;

    public CaptionPosition Position { get; init; } = CaptionPosition.Center;

    /// <summary>Distance of the caption block from the frame edge, as a percentage of height.</summary>
    public double MarginVerticalPercent { get; init; } = 12.0;
}
