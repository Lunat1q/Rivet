namespace Rivet.Core.Media;

/// <summary>
/// The video containers Rivet accepts. One list, shared by the file picker and drag-and-drop
/// so the two never drift apart on what counts as "a video".
/// </summary>
public static class VideoFile
{
    public static readonly IReadOnlyList<string> Extensions =
        [".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v"];

    /// <summary>File-picker glob patterns, e.g. "*.mp4".</summary>
    public static readonly IReadOnlyList<string> Patterns =
        Extensions.Select(e => "*" + e).ToArray();

    public static bool IsVideo(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
}
