using System.Text.RegularExpressions;

namespace Rivet.Core.Media;

/// <summary>
/// Names the captioned file next to its source and versions re-renders so a finished export is
/// never silently overwritten: <c>clip-captioned.mp4</c>, then <c>_v2</c>, <c>_v3</c>, …
///
/// Versions are <b>monotonic</b> — always one past the highest that has ever existed. Deleting
/// <c>_v2</c> does not free the name up again; the next render after <c>_v3</c> is <c>_v4</c>.
/// That keeps the newest render obvious and stops an old and a new "v2" from meaning two things.
/// </summary>
public static class OutputNaming
{
    private const string Suffix = "-captioned";

    /// <summary>The base output path: the input's name + "-captioned.mp4", in the input's folder.</summary>
    public static string CaptionedPath(string inputVideoPath)
    {
        var dir = Path.GetDirectoryName(inputVideoPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(inputVideoPath);
        return Path.Combine(dir, $"{name}{Suffix}.mp4");
    }

    /// <summary>
    /// The next free versioned path for a base like <c>x.mp4</c>: the base itself when nothing
    /// exists yet, otherwise <c>x_v{N+1}.mp4</c> where N is the highest version already on disk.
    /// </summary>
    public static string NextVersionedPath(string basePath)
    {
        var dir = Path.GetDirectoryName(basePath);
        IEnumerable<string?> existing = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Select(Path.GetFileName)
            : [];
        return NextVersionedPath(basePath, existing);
    }

    // The pure core, so the version arithmetic is unit-testable without touching the disk.
    internal static string NextVersionedPath(string basePath, IEnumerable<string?> existingFileNames)
    {
        var dir = Path.GetDirectoryName(basePath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);

        // The base name is version 1; "name_vN.ext" is version N.
        var pattern = new Regex(
            $"^{Regex.Escape(name)}(?:_v(?<v>[0-9]+))?{Regex.Escape(ext)}$",
            RegexOptions.IgnoreCase);

        var highest = 0;
        foreach (var file in existingFileNames)
        {
            if (file is null)
                continue;
            var m = pattern.Match(file);
            if (!m.Success)
                continue;
            var v = m.Groups["v"].Success ? int.Parse(m.Groups["v"].Value) : 1;
            if (v > highest)
                highest = v;
        }

        return highest == 0
            ? basePath
            : Path.Combine(dir, $"{name}_v{highest + 1}{ext}");
    }
}
