using BillApp.Settings;

namespace BillApp.Services;

/// <summary>
/// Service for persisting user settings like window size and column widths.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current user settings.
    /// </summary>
    UserSettings Settings { get; }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    void Load();

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    void Save();
}
