namespace Rivet.Core.Media;

/// <summary>
/// The shape of the source video — enough to size the subtitle canvas so burned text lands
/// where it should regardless of whether the clip is 1080x1920 vertical or 1920x1080.
/// </summary>
public readonly record struct VideoInfo(int Width, int Height, TimeSpan Duration, double Fps)
{
    /// <summary>Shorts/TikTok are portrait. Some captions styles key off this.</summary>
    public bool IsPortrait => Height >= Width;
}
