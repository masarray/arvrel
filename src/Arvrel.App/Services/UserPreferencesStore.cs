using System.Text.Json;

namespace Arvrel.App.Services;

internal sealed record UserPreferences
{
    public int SchemaVersion { get; init; } = 1;
    public string? LastSclPath { get; init; }
    public string? LastAdapterSelector { get; init; }
    public string? LastAdapterDisplayName { get; init; }
    public string? LastAdapterMacAddress { get; init; }
}

internal sealed class UserPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public UserPreferencesStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ARVREL",
            "user-preferences.json");
    }

    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new UserPreferences();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions) ?? new UserPreferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserPreferences();
        }
    }

    public bool TrySave(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
