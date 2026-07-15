using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Rivet.App.Composition;

/// <summary>
/// What the user picked last time. Not app config — purely "stop making me re-pick my caption
/// style every launch". Stored in %LOCALAPPDATA%/Rivet/settings.json.
/// </summary>
public sealed record UserSettings
{
    public string? QualityName { get; init; }
    public string? ProcessingName { get; init; }
    public string? FontName { get; init; }
    public string? PrimaryColorName { get; init; }
    public string? HighlightColorName { get; init; }
    public string? PositionName { get; init; }
    public double? FontSizePercent { get; init; }
    public int? WordsPerCaption { get; init; }
    public bool? Uppercase { get; init; }
    public bool? IsolateVocals { get; init; }
}

public interface IUserSettingsStore
{
    UserSettings Load();
    void Save(UserSettings settings);
}

public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<JsonUserSettingsStore> _logger;

    public JsonUserSettingsStore(ILogger<JsonUserSettingsStore> logger, string? path = null)
    {
        _logger = logger;
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rivet",
            "settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new UserSettings();

            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path), Options)
                   ?? new UserSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt preferences file must never stop the app from starting.
            _logger.LogWarning(ex, "Could not read {Path}; falling back to defaults", _path);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save settings to {Path}", _path);
        }
    }
}
