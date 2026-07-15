namespace Rivet.Core.Media.FFmpeg;

/// <summary>
/// Finds ffmpeg and ffprobe. Rivet does not bundle them (ADR 0002): they are ~120 MB, their
/// build/licence choice belongs to the user, and every machine that edits video already has a
/// copy. We look in the obvious places and otherwise trust PATH; a missing binary surfaces as
/// one clear error, not a mystery crash.
/// </summary>
public sealed class FFmpegLocator
{
    public string FFmpegPath { get; }
    public string FFprobePath { get; }

    public FFmpegLocator(string? overrideDirectory = null)
    {
        FFmpegPath = Resolve("ffmpeg", overrideDirectory);
        FFprobePath = Resolve("ffprobe", overrideDirectory);
    }

    private static string Resolve(string tool, string? overrideDirectory)
    {
        var exe = OperatingSystem.IsWindows() ? tool + ".exe" : tool;

        var candidates = new List<string>();
        if (overrideDirectory is not null)
            candidates.Add(Path.Combine(overrideDirectory, exe));

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rivet", "ffmpeg", exe));
        candidates.Add(Path.Combine(@"C:\ffmpeg", exe));

        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return candidate;

        // Not found on disk — hand back the bare name and let the OS resolve it from PATH.
        return tool;
    }
}
