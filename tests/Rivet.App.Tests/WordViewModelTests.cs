using Rivet.App.ViewModels;
using Rivet.Core.Transcription;

namespace Rivet.App.Tests;

public class WordViewModelTests
{
    private static WordViewModel From(string text, double start, double end) =>
        new(new TranscribedWord(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), 0.9f));

    [Fact]
    public void Seconds_are_rounded_to_two_places_for_display()
    {
        var vm = From("hi", 1.23456, 2.98765);
        Assert.Equal(1.23, vm.Start);
        Assert.Equal(2.99, vm.End);
    }

    [Fact]
    public void ToWord_trims_surrounding_whitespace()
    {
        var vm = From("  hi  ", 0, 1);
        Assert.Equal("hi", vm.ToWord().Text);
    }

    [Fact]
    public void ToWord_never_lets_end_precede_start()
    {
        var vm = From("hi", 2.0, 2.0);
        vm.End = 1.0; // user typed an end before the start
        var word = vm.ToWord();
        Assert.Equal(word.Start, word.End);
    }

    [Fact]
    public void ToWord_preserves_a_valid_range()
    {
        var vm = From("hi", 1.0, 3.0);
        var word = vm.ToWord();
        Assert.Equal(TimeSpan.FromSeconds(1.0), word.Start);
        Assert.Equal(TimeSpan.FromSeconds(3.0), word.End);
    }
}
