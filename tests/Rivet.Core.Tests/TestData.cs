using Rivet.Core.Transcription;

namespace Rivet.Core.Tests;

internal static class TestData
{
    public static TranscribedWord Word(string text, double start, double end, float conf = 1f) =>
        new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), conf);

    public static Transcript Clip(params TranscribedWord[] words) => new(words);
}
