using Rivet.Core.Subtitles;
using static Rivet.Core.Tests.TestData;

namespace Rivet.Core.Tests;

public class CaptionBuilderTests
{
    [Fact]
    public void Empty_transcript_yields_no_captions()
    {
        var captions = CaptionBuilder.Build(Clip(), 3);
        Assert.Empty(captions);
    }

    [Fact]
    public void Breaks_when_word_cap_is_hit()
    {
        var t = Clip(
            Word("one", 0.0, 0.2), Word("two", 0.2, 0.4),
            Word("three", 0.4, 0.6), Word("four", 0.6, 0.8));

        var captions = CaptionBuilder.Build(t, maxWordsPerCaption: 2);

        Assert.Equal(2, captions.Count);
        Assert.Equal(2, captions[0].Words.Count);
        Assert.Equal(2, captions[1].Words.Count);
    }

    [Fact]
    public void Breaks_on_a_pause_longer_than_the_threshold()
    {
        // 600ms gap between "two" and "three" exceeds the 500ms pause threshold.
        var t = Clip(
            Word("one", 0.0, 0.2), Word("two", 0.2, 0.4),
            Word("three", 1.0, 1.2));

        var captions = CaptionBuilder.Build(t, maxWordsPerCaption: 10);

        Assert.Equal(2, captions.Count);
        Assert.Equal(["one", "two"], captions[0].Words.Select(w => w.Text));
        Assert.Equal(["three"], captions[1].Words.Select(w => w.Text));
    }

    [Fact]
    public void Does_not_break_on_a_pause_shorter_than_the_threshold()
    {
        var t = Clip(
            Word("one", 0.0, 0.2), Word("two", 0.4, 0.6)); // 200ms gap

        var captions = CaptionBuilder.Build(t, maxWordsPerCaption: 10);

        Assert.Single(captions);
    }

    [Theory]
    [InlineData("end.")]
    [InlineData("stop!")]
    [InlineData("really?")]
    [InlineData("wait…")]
    public void Sentence_ending_punctuation_forces_a_break(string firstWord)
    {
        var t = Clip(
            Word(firstWord, 0.0, 0.2), Word("next", 0.25, 0.45)); // small gap, no cap hit

        var captions = CaptionBuilder.Build(t, maxWordsPerCaption: 10);

        Assert.Equal(2, captions.Count);
    }

    [Fact]
    public void MaxWordsPerCaption_below_one_is_treated_as_one()
    {
        var t = Clip(Word("a", 0, 0.1), Word("b", 0.1, 0.2));

        var captions = CaptionBuilder.Build(t, maxWordsPerCaption: 0);

        Assert.Equal(2, captions.Count);
    }

    [Fact]
    public void Caption_start_and_end_come_from_first_and_last_word()
    {
        var caption = new Caption([Word("a", 1.0, 1.5), Word("b", 1.6, 2.0)]);
        Assert.Equal(TimeSpan.FromSeconds(1.0), caption.Start);
        Assert.Equal(TimeSpan.FromSeconds(2.0), caption.End);
    }
}
