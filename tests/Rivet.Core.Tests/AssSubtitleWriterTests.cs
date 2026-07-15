using Rivet.Core.Media;
using Rivet.Core.Subtitles;
using static Rivet.Core.Tests.TestData;

namespace Rivet.Core.Tests;

public class AssSubtitleWriterTests
{
    private static readonly VideoInfo Portrait = new(1080, 1920, TimeSpan.FromSeconds(30), 30);

    [Theory]
    [InlineData("#FFFFFF", "&H00FFFFFF")]
    [InlineData("#000000", "&H00000000")]
    [InlineData("#F5E000", "&H0000E0F5")] // R,G,B reversed to B,G,R
    [InlineData("FFFFFF", "&H00FFFFFF")]  // leading # optional
    public void Color_converts_rrggbb_to_ass_bgr(string hex, string expected) =>
        Assert.Equal(expected, AssSubtitleWriter.Color(hex));

    [Theory]
    [InlineData("#FFF")]     // wrong length
    [InlineData("nothex!")]
    public void Color_falls_back_to_white_on_bad_input(string hex) =>
        Assert.Equal("&H00FFFFFF", AssSubtitleWriter.Color(hex));

    [Fact]
    public void Time_formats_hmmss_centiseconds()
    {
        Assert.Equal("0:00:01.50", AssSubtitleWriter.Time(TimeSpan.FromSeconds(1.5)));
        Assert.Equal("0:01:05.00", AssSubtitleWriter.Time(TimeSpan.FromSeconds(65)));
        Assert.Equal("1:00:00.00", AssSubtitleWriter.Time(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Time_clamps_negative_to_zero()
    {
        Assert.Equal("0:00:00.00", AssSubtitleWriter.Time(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void Write_emits_header_and_resolution_from_video()
    {
        var ass = AssSubtitleWriter.Write([new Caption([Word("hi", 0, 0.5)])], new SubtitleStyle(), Portrait);

        Assert.Contains("[Script Info]", ass);
        Assert.Contains("PlayResX: 1080", ass);
        Assert.Contains("PlayResY: 1920", ass);
        Assert.Contains("[V4+ Styles]", ass);
        Assert.Contains("[Events]", ass);
    }

    [Fact]
    public void Write_falls_back_to_1080x1920_when_video_dimensions_are_zero()
    {
        var ass = AssSubtitleWriter.Write(
            [new Caption([Word("hi", 0, 0.5)])], new SubtitleStyle(), new VideoInfo(0, 0, TimeSpan.Zero, 0));

        Assert.Contains("PlayResX: 1080", ass);
        Assert.Contains("PlayResY: 1920", ass);
    }

    [Fact]
    public void Write_emits_one_dialogue_event_per_word()
    {
        var caption = new Caption([Word("one", 0, 0.3), Word("two", 0.3, 0.6), Word("three", 0.6, 0.9)]);
        var ass = AssSubtitleWriter.Write([caption], new SubtitleStyle(), Portrait);

        var events = ass.Split('\n').Count(l => l.StartsWith("Dialogue:"));
        Assert.Equal(3, events);
    }

    [Fact]
    public void Uppercase_style_shouts_the_text()
    {
        var caption = new Caption([Word("hi", 0, 0.5)]);
        var ass = AssSubtitleWriter.Write([caption], new SubtitleStyle { Uppercase = true }, Portrait);
        Assert.Contains("HI", ass);
    }

    [Fact]
    public void Non_uppercase_style_keeps_original_case()
    {
        var caption = new Caption([Word("hi", 0, 0.5)]);
        var ass = AssSubtitleWriter.Write([caption], new SubtitleStyle { Uppercase = false }, Portrait);
        Assert.Contains("hi", ass);
        Assert.DoesNotContain("HI", ass);
    }

    [Fact]
    public void Active_word_gets_the_highlight_override_block()
    {
        var caption = new Caption([Word("a", 0, 0.3), Word("b", 0.3, 0.6)]);
        var ass = AssSubtitleWriter.Write(
            [caption], new SubtitleStyle { HighlightScalePercent = 120, Uppercase = false }, Portrait);

        Assert.Contains("\\fscx120\\fscy120", ass); // the active word scales up
        Assert.Contains("{\\r}", ass);              // and resets after
    }

    [Fact]
    public void Braces_in_a_spoken_word_are_neutralised()
    {
        // A literal brace would otherwise open an ASS override block.
        var caption = new Caption([Word("{evil}", 0, 0.5)]);
        var ass = AssSubtitleWriter.Write([caption], new SubtitleStyle { Uppercase = false }, Portrait);

        Assert.Contains("(evil)", ass);
        Assert.DoesNotContain("{evil}", ass);
    }

    [Theory]
    [InlineData(CaptionPosition.Top, ",8,")]
    [InlineData(CaptionPosition.Center, ",5,")]
    [InlineData(CaptionPosition.Bottom, ",2,")]
    public void Position_maps_to_ass_alignment(CaptionPosition position, string alignmentToken)
    {
        var ass = AssSubtitleWriter.Write(
            [new Caption([Word("hi", 0, 0.5)])], new SubtitleStyle { Position = position }, Portrait);

        var styleLine = ass.Split('\n').First(l => l.StartsWith("Style: Rivet"));
        Assert.Contains(alignmentToken, styleLine);
    }

    [Fact]
    public void Two_cards_never_overlap_in_time()
    {
        var captions = new[]
        {
            new Caption([Word("a", 0.0, 0.3)]),
            new Caption([Word("b", 5.0, 5.3)]),
        };
        var ass = AssSubtitleWriter.Write(captions, new SubtitleStyle(), Portrait);

        // First card must clear (within MaxGapFill = 1s) well before the second begins at 5s.
        Assert.Contains("0:00:01.30", ass); // 0.3 end + 1s gap-fill cap
    }
}
