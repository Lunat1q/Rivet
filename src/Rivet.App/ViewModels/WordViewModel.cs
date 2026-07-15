using CommunityToolkit.Mvvm.ComponentModel;
using Rivet.Core.Transcription;

namespace Rivet.App.ViewModels;

/// <summary>
/// One editable word in the review step. The user can fix the text whisper got wrong and nudge
/// the start/end (in seconds) to realign the highlight. Times are shown in seconds because that
/// is what a person scrubbing a 20-second clip actually thinks in.
/// </summary>
public sealed partial class WordViewModel : ObservableObject
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private double _start;
    [ObservableProperty] private double _end;

    public WordViewModel(TranscribedWord word)
    {
        _text = word.Text;
        _start = Math.Round(word.Start.TotalSeconds, 2);
        _end = Math.Round(word.End.TotalSeconds, 2);
    }

    public TranscribedWord ToWord() =>
        new(Text.Trim(), TimeSpan.FromSeconds(Start), TimeSpan.FromSeconds(Math.Max(Start, End)), 1f);
}
