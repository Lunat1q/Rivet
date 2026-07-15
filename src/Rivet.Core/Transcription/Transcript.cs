namespace Rivet.Core.Transcription;

/// <summary>
/// One spoken word with its timing and how sure whisper.cpp was of it. The times are the
/// pillar of the whole app: the viral caption style highlights whichever word is being said
/// *right now*, so per-word timing is not a nicety, it is the product.
/// </summary>
/// <param name="Start">When the word begins, relative to the start of the video.</param>
/// <param name="End">When the word ends.</param>
/// <param name="Confidence">0..1 mean token probability. Below ~0.6 the word is close to a guess.</param>
public readonly record struct TranscribedWord(string Text, TimeSpan Start, TimeSpan End, float Confidence);

/// <summary>The full word-timed transcript of a clip, in order.</summary>
public sealed record Transcript(IReadOnlyList<TranscribedWord> Words)
{
    public static readonly Transcript Empty = new([]);

    public bool IsEmpty => Words.Count == 0;

    public string Text => string.Join(' ', Words.Select(w => w.Text));
}
