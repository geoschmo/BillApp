using System.IO;

namespace BillApp.Settings;

/// <summary>
/// Stores user preferences like window size and column widths.
/// </summary>
public class UserSettings
{
    // Window settings
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public bool IsMaximized { get; set; } = false;

    // Column widths by view and column header
    public Dictionary<string, Dictionary<string, double>> ColumnWidths { get; set; } = new();

    // Backup settings
    public BackupSettings BackupSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets column width for a specific view and column.
    /// </summary>
    public double GetColumnWidth(string viewName, string columnHeader, double defaultWidth)
    {
        if (ColumnWidths.TryGetValue(viewName, out var columns) &&
            columns.TryGetValue(columnHeader, out var width))
        {
            return width;
        }
        return defaultWidth;
    }

    /// <summary>
    /// Sets column width for a specific view and column.
    /// </summary>
    public void SetColumnWidth(string viewName, string columnHeader, double width)
    {
        if (!ColumnWidths.ContainsKey(viewName))
        {
            ColumnWidths[viewName] = new Dictionary<string, double>();
        }
        ColumnWidths[viewName][columnHeader] = width;
    }
}

/// <summary>
/// Backup-related settings.
/// </summary>
public class BackupSettings
{
    /// <summary>
    /// Folder where backups are stored.
    /// </summary>
    public string BackupFolder { get; set; } = GetDefaultBackupFolder();

    /// <summary>
    /// Whether automatic backups are enabled.
    /// </summary>
    public bool AutoBackupEnabled { get; set; } = false;

    /// <summary>
    /// How often to create automatic backups.
    /// </summary>
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Weekly;

    /// <summary>
    /// When the last automatic backup was created.
    /// </summary>
    public DateTime? LastAutoBackupTime { get; set; }

    /// <summary>
    /// Maximum number of backup files to keep.
    /// </summary>
    public int MaxBackupsToKeep { get; set; } = 10;

    /// <summary>
    /// Maximum age of backup files in days.
    /// </summary>
    public int MaxBackupAgeDays { get; set; } = 90;

    private static string GetDefaultBackupFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BillApp Backups");
    }
}

/// <summary>
/// Frequency options for automatic backups.
/// </summary>
public enum BackupFrequency
{
    Daily,
    Weekly,
    Monthly
}
