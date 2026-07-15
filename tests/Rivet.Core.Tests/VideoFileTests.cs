using Rivet.Core.Media;

namespace Rivet.Core.Tests;

public class VideoFileTests
{
    [Theory]
    [InlineData(@"C:\a\clip.mp4")]
    [InlineData("clip.MOV")]
    [InlineData("clip.MkV")]
    [InlineData("a.b.webm")]
    [InlineData("movie.avi")]
    [InlineData("reel.m4v")]
    public void IsVideo_accepts_known_extensions_any_case(string path) =>
        Assert.True(VideoFile.IsVideo(path));

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("clip.mp3")]
    [InlineData("image.png")]
    [InlineData("noextension")]
    public void IsVideo_rejects_everything_else(string path) =>
        Assert.False(VideoFile.IsVideo(path));

    [Fact]
    public void Patterns_are_derived_from_extensions()
    {
        Assert.Equal(VideoFile.Extensions.Count, VideoFile.Patterns.Count);
        Assert.Contains("*.mp4", VideoFile.Patterns);
        Assert.All(VideoFile.Patterns, p => Assert.StartsWith("*.", p));
    }
}
