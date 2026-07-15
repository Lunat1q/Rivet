using System.Text.RegularExpressions;
using Rivet.Core.Abstractions;
using Rivet.Core.Audio;
using Rivet.Core.Transcription;
using Whisper.net;

namespace Rivet.Core.Whisper;

/// <summary>
/// ITranscriber over ggml-org/whisper.cpp (via the Whisper.net P/Invoke binding, ADR 0001).
///
/// Transcription runs normally — a whisper segment is a full phrase, which is what keeps the
/// *text* correct. Per-word timing comes from whisper's token timestamps: each decoded token
/// carries a start/end (in centiseconds), and a "word" is the run of sub-word tokens between
/// one leading space and the next. That gives an accurate time for every word without a
/// separate alignment model, which is what the karaoke highlight rides on (ADR 0003).
/// </summary>
public sealed class WhisperTranscriber : ITranscriber
{
    private readonly WhisperProcessor _processor;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public WhisperTranscriber(WhisperProcessor processor) => _processor = processor;

    public async Task<Transcript> TranscribeAsync(
        AudioBuffer audio,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var totalSeconds = audio.Duration.TotalSeconds;
        var words = new List<TranscribedWord>();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (var segment in _processor
                               .ProcessAsync(audio.Samples, cancellationToken)
                               .ConfigureAwait(false))
            {
                // whisper.cpp is confident on music/noise; drop segments it flags as non-speech.
                if (segment.NoSpeechProbability > 0.8f)
                    continue;

                AppendWords(segment, words);

                if (totalSeconds > 0)
                    progress?.Report(Math.Clamp(segment.End.TotalSeconds / totalSeconds, 0d, 1d));
            }
        }
        finally
        {
            _gate.Release();
        }

        progress?.Report(1d);
        return new Transcript(words);
    }

    /// <summary>
    /// Regroups a segment's tokens into words. whisper emits sub-word BPE tokens, each prefixed
    /// with a space when it begins a new word ("границы" comes back as " гр","ани","цы"). So a
    /// new word starts at every leading-space token; punctuation, which carries no leading space,
    /// sticks to the word before it.
    /// </summary>
    private static void AppendWords(SegmentData segment, List<TranscribedWord> words)
    {
        if (segment.Tokens is null)
            return;

        var current = new List<WhisperToken>();

        foreach (var token in segment.Tokens)
        {
            var text = token.Text;
            if (string.IsNullOrEmpty(text) || IsSpecialToken(text))
                continue;

            var startsWord = char.IsWhiteSpace(text[0]);
            if (startsWord && current.Count > 0)
            {
                Flush(current, words);
                current.Clear();
            }

            current.Add(token);
        }

        if (current.Count > 0)
            Flush(current, words);
    }

    private static void Flush(List<WhisperToken> pieces, List<TranscribedWord> words)
    {
        var text = Clean(string.Concat(pieces.Select(p => p.Text)));
        if (text.Length == 0)
            return;

        // Token Start/End are whisper centiseconds (t0/t1); ×10 → milliseconds.
        var start = TimeSpan.FromMilliseconds(pieces[0].Start * 10);
        var end = TimeSpan.FromMilliseconds(pieces[^1].End * 10);
        if (end < start)
            end = start;

        var confidence = pieces.Average(p => p.Probability);
        words.Add(new TranscribedWord(text, start, end, confidence));
    }

    /// <summary>whisper's control tokens: [_BEG_], [_TT_nnn] (timestamps), &lt;|ru|&gt; etc.</summary>
    private static bool IsSpecialToken(string text) =>
        (text.StartsWith("[_", StringComparison.Ordinal) && text.EndsWith(']')) ||
        (text.StartsWith("<|", StringComparison.Ordinal) && text.EndsWith("|>", StringComparison.Ordinal));

    /// <summary>
    /// Strips whisper's inline non-speech annotations — "[BLANK_AUDIO]", "(music)", "*sighs*" —
    /// and collapses whitespace. A word that was nothing but an annotation cleans to empty and
    /// is dropped by <see cref="Flush" />.
    /// </summary>
    internal static string Clean(string text) =>
        WhitespaceRuns.Replace(NonSpeechAnnotations.Replace(text, " "), " ").Trim();

    private static readonly Regex NonSpeechAnnotations =
        new(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*", RegexOptions.Compiled);

    private static readonly Regex WhitespaceRuns = new(@"\s+", RegexOptions.Compiled);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _processor.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
