using Rivet.Core.Media.FFmpeg;

namespace Rivet.Core.Tests;

public class FFmpegLocatorTests
{
    private static string Exe(string tool) => OperatingSystem.IsWindows() ? tool + ".exe" : tool;

    [Fact]
    public void Uses_binaries_from_the_override_directory_when_present()
    {
        var dir = Directory.CreateTempSubdirectory("rivet-ffmpeg");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, Exe("ffmpeg")), "");
            File.WriteAllText(Path.Combine(dir.FullName, Exe("ffprobe")), "");

            var locator = new FFmpegLocator(dir.FullName);

            Assert.Equal(Path.Combine(dir.FullName, Exe("ffmpeg")), locator.FFmpegPath);
            Assert.Equal(Path.Combine(dir.FullName, Exe("ffprobe")), locator.FFprobePath);
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Falls_back_to_bare_name_for_PATH_resolution_when_not_found()
    {
        var empty = Directory.CreateTempSubdirectory("rivet-ffmpeg-empty");
        try
        {
            var locator = new FFmpegLocator(empty.FullName);
            // Not in the override dir -> either found in another known spot on this machine, or the
            // bare name handed to the OS for PATH resolution. Never a path inside the empty dir.
            Assert.True(locator.FFmpegPath == "ffmpeg" || File.Exists(locator.FFmpegPath));
            Assert.True(locator.FFprobePath == "ffprobe" || File.Exists(locator.FFprobePath));
            Assert.DoesNotContain(empty.FullName, locator.FFmpegPath);
        }
        finally { empty.Delete(recursive: true); }
    }
}
