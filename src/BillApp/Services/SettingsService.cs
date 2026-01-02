using System.IO;
using System.Text.Json;
using BillApp.Settings;

namespace BillApp.Services;

/// <summary>
/// Service for persisting user settings to a JSON file.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        // Store settings in AppData/Local/BillApp
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var billAppFolder = Path.Combine(appDataPath, "BillApp");
        Directory.CreateDirectory(billAppFolder);
        _settingsPath = Path.Combine(billAppFolder, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions) ?? new UserSettings();
            }
        }
        catch
        {
            // If settings file is corrupted, start fresh
            Settings = new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail - settings are not critical
        }
    }
}
