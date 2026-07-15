using Rivet.Core.Media;

namespace Rivet.Core.Tests;

public class OutputNamingTests
{
    [Theory]
    [InlineData(@"C:\videos\clip.mp4", @"C:\videos\clip-captioned.mp4")]
    [InlineData(@"C:\videos\my.long.name.mov", @"C:\videos\my.long.name-captioned.mp4")]
    [InlineData(@"clip.webm", "clip-captioned.mp4")]
    public void CaptionedPath_appends_suffix_and_forces_mp4(string input, string expectedTail)
    {
        var result = OutputNaming.CaptionedPath(input);
        Assert.Equal(Path.GetFileName(expectedTail), Path.GetFileName(result));
    }

    [Fact]
    public void CaptionedPath_keeps_input_directory()
    {
        var result = OutputNaming.CaptionedPath(@"C:\a\b\clip.mp4");
        Assert.Equal(Path.GetFullPath(@"C:\a\b\clip-captioned.mp4"), Path.GetFullPath(result));
    }

    [Fact]
    public void FirstRender_when_nothing_exists_uses_base_name()
    {
        var next = OutputNaming.NextVersionedPath(@"C:\v\clip-captioned.mp4", []);
        Assert.Equal(@"C:\v\clip-captioned.mp4", next);
    }

    [Fact]
    public void SecondRender_bumps_to_v2()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4", ["clip-captioned.mp4"]);
        Assert.Equal(@"C:\v\clip-captioned_v2.mp4", next);
    }

    [Fact]
    public void ThirdRender_bumps_to_v3()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4", ["clip-captioned.mp4", "clip-captioned_v2.mp4"]);
        Assert.Equal(@"C:\v\clip-captioned_v3.mp4", next);
    }

    // The reported bug: v2 then v3 created, v2 deleted — the next render must be v4, never reuse v2.
    [Fact]
    public void Versioning_is_monotonic_and_does_not_reuse_a_deleted_gap()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4", ["clip-captioned.mp4", "clip-captioned_v3.mp4"]);
        Assert.Equal(@"C:\v\clip-captioned_v4.mp4", next);
    }

    [Fact]
    public void Highest_version_wins_regardless_of_listing_order()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4",
            ["clip-captioned_v10.mp4", "clip-captioned.mp4", "clip-captioned_v2.mp4"]);
        Assert.Equal(@"C:\v\clip-captioned_v11.mp4", next);
    }

    [Fact]
    public void Unrelated_files_are_ignored()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4",
            ["other.mp4", "clip-captioned.txt", "clip-captioned_vX.mp4", "clipcaptioned.mp4"]);
        Assert.Equal(@"C:\v\clip-captioned.mp4", next);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var next = OutputNaming.NextVersionedPath(
            @"C:\v\clip-captioned.mp4", ["CLIP-CAPTIONED.MP4"]);
        Assert.Equal(@"C:\v\clip-captioned_v2.mp4", next);
    }

    [Fact]
    public void Disk_overload_returns_base_when_directory_is_empty()
    {
        var dir = Directory.CreateTempSubdirectory("rivet-naming");
        try
        {
            var basePath = Path.Combine(dir.FullName, "clip-captioned.mp4");
            Assert.Equal(basePath, OutputNaming.NextVersionedPath(basePath));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Disk_overload_versions_against_real_files()
    {
        var dir = Directory.CreateTempSubdirectory("rivet-naming");
        try
        {
            var basePath = Path.Combine(dir.FullName, "clip-captioned.mp4");
            File.WriteAllText(basePath, "");
            File.WriteAllText(Path.Combine(dir.FullName, "clip-captioned_v2.mp4"), "");

            Assert.Equal(
                Path.Combine(dir.FullName, "clip-captioned_v3.mp4"),
                OutputNaming.NextVersionedPath(basePath));
        }
        finally { dir.Delete(recursive: true); }
    }
}
