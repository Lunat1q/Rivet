using Microsoft.Extensions.Logging.Abstractions;
using Rivet.App.Composition;
using Rivet.App.ViewModels;
using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;

namespace Rivet.App.Tests;

public class ChoicesTests
{
    [Fact]
    public void Every_dropdown_the_view_model_indexes_has_the_expected_entries()
    {
        // MainViewModel's defaults index these; an empty or reordered list would crash at startup.
        Assert.True(Choices.Qualities.Count >= 2);
        Assert.NotEmpty(Choices.Processings);
        Assert.NotEmpty(Choices.Positions);
        Assert.NotEmpty(Choices.Fonts);
        Assert.NotEmpty(Choices.PrimaryColors);
        Assert.NotEmpty(Choices.HighlightColors);
    }

    [Fact]
    public void Balanced_turbo_is_the_recommended_default_quality()
    {
        Assert.Equal(WhisperModel.LargeV3Turbo, Choices.Qualities[1].Value);
    }

    [Fact]
    public void Automatic_backend_is_the_default_processing()
    {
        Assert.Equal(ComputeBackend.Auto, Choices.Processings[0].Value);
    }

    [Fact]
    public void Colors_are_rrggbb_hex()
    {
        foreach (var c in Choices.PrimaryColors.Concat(Choices.HighlightColors))
        {
            Assert.StartsWith("#", c.Value);
            Assert.Equal(7, c.Value.Length);
        }
    }

    [Fact]
    public void Choice_display_name_is_used_for_ToString()
    {
        var choice = new Choice<int>("Nice", 42);
        Assert.Equal("Nice", choice.ToString());
    }
}

public class JsonUserSettingsStoreTests
{
    private static JsonUserSettingsStore StoreAt(string path) =>
        new(NullLogger<JsonUserSettingsStore>.Instance, path);

    [Fact]
    public void Missing_file_loads_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rivet-{Guid.NewGuid():N}.json");
        Assert.NotNull(StoreAt(path).Load());
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var dir = Directory.CreateTempSubdirectory("rivet-settings");
        try
        {
            var store = StoreAt(Path.Combine(dir.FullName, "settings.json"));
            store.Save(new UserSettings { FontName = "Impact", WordsPerCaption = 4, Uppercase = false });

            var loaded = store.Load();
            Assert.Equal("Impact", loaded.FontName);
            Assert.Equal(4, loaded.WordsPerCaption);
            Assert.False(loaded.Uppercase);
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_instead_of_throwing()
    {
        var dir = Directory.CreateTempSubdirectory("rivet-settings");
        try
        {
            var path = Path.Combine(dir.FullName, "settings.json");
            File.WriteAllText(path, "{ not valid json ");
            var loaded = StoreAt(path).Load();
            Assert.NotNull(loaded);
            Assert.Null(loaded.FontName);
        }
        finally { dir.Delete(recursive: true); }
    }
}

public class AppVersionTests
{
    [Fact]
    public void Current_is_a_non_empty_version_string()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppVersion.Current));
        Assert.DoesNotContain('+', AppVersion.Current); // git hash stripped
    }
}
