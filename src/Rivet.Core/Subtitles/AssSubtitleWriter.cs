using System.Globalization;
using System.Text;
using Rivet.Core.Media;

namespace Rivet.Core.Subtitles;

/// <summary>
/// Turns captions into an ASS subtitle file styled the short-form way: big outlined text with
/// the currently-spoken word popping in a highlight colour.
///
/// The "one word pops" effect is done with one Dialogue event *per word* rather than ASS
/// karaoke (\k) tags. \k only cross-fades a fill; it cannot grow the active word or change its
/// outline. A per-word event carries a full override block, so the highlighted word can also
/// scale up — which is the look people actually mean by "TikTok captions". The cost is more
/// events (one per word), which libass handles without noticing.
/// </summary>
public static class AssSubtitleWriter
{
    public static string Write(IReadOnlyList<Caption> captions, SubtitleStyle style, VideoInfo video)
    {
        var height = video.Height > 0 ? video.Height : 1920;
        var width = video.Width > 0 ? video.Width : 1080;

        var fontSize = Math.Max(1, (int)Math.Round(height * style.FontSizePercent / 100.0));
        var outline = Math.Max(1, (int)Math.Round(height * style.OutlineWidthPercent / 100.0));
        var marginV = (int)Math.Round(height * style.MarginVerticalPercent / 100.0);
        var marginLR = (int)Math.Round(width * 0.06);
        var alignment = style.Position switch
        {
            CaptionPosition.Top => 8,
            CaptionPosition.Center => 5,
            _ => 2
        };

        var sb = new StringBuilder();
        sb.Append("[Script Info]\n")
          .Append("ScriptType: v4.00+\n")
          .Append("WrapStyle: 0\n")
          .Append("ScaledBorderAndShadow: yes\n")
          .Append(CultureInfo.InvariantCulture, $"PlayResX: {width}\n")
          .Append(CultureInfo.InvariantCulture, $"PlayResY: {height}\n\n");

        sb.Append("[V4+ Styles]\n")
          .Append("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, ")
          .Append("BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, ")
          .Append("BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n")
          .Append(CultureInfo.InvariantCulture,
              $"Style: Rivet,{style.FontName},{fontSize},{Color(style.PrimaryColor)},{Color(style.HighlightColor)},")
          .Append(CultureInfo.InvariantCulture,
              $"{Color(style.OutlineColor)},&H64000000,{(style.Bold ? -1 : 0)},0,0,0,100,100,0,0,1,")
          .Append(CultureInfo.InvariantCulture,
              $"{outline},0,{alignment},{marginLR},{marginLR},{marginV},1\n\n");

        sb.Append("[Events]\n")
          .Append("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");

        for (var k = 0; k < captions.Count; k++)
        {
            // A card stays up until the next card begins, not until its own last word ends —
            // otherwise a fast phrase flashes for a fraction of a second. During a long pause the
            // card lingers only up to MaxGapFill, then clears. Capping at the next card's start is
            // also what guarantees two cards are never on screen at once.
            var next = k + 1 < captions.Count ? captions[k + 1].Start : TimeSpan.MaxValue;
            WriteCaption(sb, captions[k], next, style);
        }

        return sb.ToString();
    }

    /// <summary>How long a card may linger past its last word into a silent gap.</summary>
    private static readonly TimeSpan MaxGapFill = TimeSpan.FromMilliseconds(1000);

    /// <summary>Smallest slice a single word's highlight may occupy, so it is actually seen.</summary>
    private static readonly TimeSpan MinWordDuration = TimeSpan.FromMilliseconds(150);

    private static void WriteCaption(StringBuilder sb, Caption caption, TimeSpan nextCaptionStart, SubtitleStyle style)
    {
        var words = caption.Words;
        var n = words.Count;
        var start = caption.Start;

        // End: fill the gap to the next card, but not more than MaxGapFill, and never past the
        // next card. Then make sure the whole thing is long enough for every word to be read.
        var end = Min(caption.End + MaxGapFill, nextCaptionStart);
        var readable = start + n * MinWordDuration;
        if (end < readable)
            end = Min(readable, nextCaptionStart);
        if (end <= start)
            end = start + MinWordDuration;

        // Tile [start, end] into one strictly-increasing, non-overlapping slice per word. whisper
        // sometimes collapses several words onto one timestamp; clamping against a per-word
        // minimum spreads those out instead of stacking zero-width events (which read as a
        // doubled, flickering card).
        var slice = Min(MinWordDuration, (end - start) / n);
        var bounds = new TimeSpan[n + 1];
        bounds[0] = start;
        bounds[n] = end;
        for (var i = 1; i < n; i++)
        {
            var lo = bounds[i - 1] + slice;
            var hi = end - (n - i) * slice;
            bounds[i] = Clamp(words[i].Start, lo, hi);
        }

        for (var i = 0; i < n; i++)
        {
            sb.Append("Dialogue: 0,")
              .Append(Time(bounds[i])).Append(',')
              .Append(Time(bounds[i + 1])).Append(",Rivet,,0,0,0,,")
              .Append(Line(words, i, style))
              .Append('\n');
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Clamp(TimeSpan value, TimeSpan lo, TimeSpan hi)
    {
        if (hi < lo)
            return lo;
        return value < lo ? lo : value > hi ? hi : value;
    }

    private static string Line(IReadOnlyList<Transcription.TranscribedWord> words, int active, SubtitleStyle style)
    {
        var scale = style.HighlightScalePercent;
        var highlight = $"{{\\fscx{scale}\\fscy{scale}\\c{Color(style.HighlightColor)}}}";

        var sb = new StringBuilder();
        for (var i = 0; i < words.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');

            var text = Escape(style.Uppercase ? words[i].Text.ToUpperInvariant() : words[i].Text);
            if (i == active)
                sb.Append(highlight).Append(text).Append("{\\r}");
            else
                sb.Append(text);
        }
        return sb.ToString();
    }

    /// <summary>#RRGGBB to ASS &amp;H00BBGGRR (opaque; ASS colours are BGR and little-endian).</summary>
    internal static string Color(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6)
            return "&H00FFFFFF";
        var r = h[..2];
        var g = h.Substring(2, 2);
        var b = h.Substring(4, 2);
        return $"&H00{b}{g}{r}".ToUpperInvariant();
    }

    /// <summary>ASS time is H:MM:SS.cc (centiseconds).</summary>
    internal static string Time(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;
        var cs = Math.Min(99, (int)Math.Round(t.Milliseconds / 10.0));
        return string.Create(CultureInfo.InvariantCulture,
            $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{cs:00}");
    }

    /// <summary>Braces open override blocks in ASS; a spoken word must never do that by accident.</summary>
    private static string Escape(string word) =>
        word.Replace("\\", "\\\\").Replace("{", "(").Replace("}", ")");
}
