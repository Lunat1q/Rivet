using Rivet.Core.Transcription;

namespace Rivet.Core.Subtitles;

/// <summary>
/// One on-screen group of words shown together (e.g. up to three). The words keep their own
/// individual timings so the renderer can highlight them one at a time.
/// </summary>
public sealed record Caption(IReadOnlyList<TranscribedWord> Words)
{
    public TimeSpan Start => Words[0].Start;
    public TimeSpan End => Words[^1].End;
}

/// <summary>
/// Groups a flat word stream into on-screen captions. Two things end a caption: hitting the
/// word cap, or a real pause in speech — captions that break where the speaker breathes read
/// far better than ones chopped every N words mid-thought. Sentence-ending punctuation also
/// forces a break so a new sentence starts a new card.
/// </summary>
public static class CaptionBuilder
{
    /// <summary>A gap longer than this between two words starts a new caption.</summary>
    private static readonly TimeSpan PauseThreshold = TimeSpan.FromMilliseconds(500);

    public static IReadOnlyList<Caption> Build(Transcript transcript, int maxWordsPerCaption)
    {
        var max = Math.Max(1, maxWordsPerCaption);
        var captions = new List<Caption>();
        var current = new List<TranscribedWord>();

        foreach (var word in transcript.Words)
        {
            if (current.Count > 0)
            {
                var gap = word.Start - current[^1].End;
                if (current.Count >= max || gap > PauseThreshold || EndsSentence(current[^1].Text))
                {
                    captions.Add(new Caption(current));
                    current = [];
                }
            }

            current.Add(word);
        }

        if (current.Count > 0)
            captions.Add(new Caption(current));

        return captions;
    }

    private static bool EndsSentence(string word) =>
        word.Length > 0 && word[^1] is '.' or '!' or '?' or '…';
}
