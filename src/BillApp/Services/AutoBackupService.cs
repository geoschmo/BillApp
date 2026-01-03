using System.IO;
using System.Windows;
using BillApp.Core.Interfaces.Services;
using BillApp.Settings;
using BillApp.Views.Dialogs;

namespace BillApp.Services;

/// <summary>
/// Service that manages automatic scheduled backups.
/// Checks on startup and periodically if a backup is due.
/// </summary>
public class AutoBackupService : IAutoBackupService
{
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;
    private Timer? _timer;
    private bool _isDisposed;

    // Check every hour if backup is due
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public AutoBackupService(IBackupService backupService, ISettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        // Check immediately on startup, then periodically
        _timer = new Timer(
            async _ => await CheckAndBackupIfDueAsync(),
            null,
            TimeSpan.FromSeconds(30), // Initial delay to let app fully load
            CheckInterval);
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public async Task CheckAndBackupIfDueAsync()
    {
        try
        {
            var settings = _settingsService.Settings.BackupSettings;

            // Skip if auto-backup is disabled
            if (!settings.AutoBackupEnabled)
                return;

            // Check if backup is due
            if (!IsBackupDue(settings))
                return;

            // For auto-backup, we need a stored password or we skip
            // Since we don't store passwords, auto-backup will prompt user on next app launch
            // For now, we'll just log that a backup is due
            // In a future enhancement, could use Windows Credential Manager

            // Check if we're on the UI thread and can show a dialog
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await PromptForAutoBackupAsync(settings);
                });
            }
        }
        catch
        {
            // Silently fail - don't interrupt the user for auto-backup errors
        }
    }

    private bool IsBackupDue(BackupSettings settings)
    {
        if (!settings.LastAutoBackupTime.HasValue)
            return true; // Never backed up

        var lastBackup = settings.LastAutoBackupTime.Value;
        var now = DateTime.UtcNow;

        return settings.Frequency switch
        {
            BackupFrequency.Daily => (now - lastBackup).TotalDays >= 1,
            BackupFrequency.Weekly => (now - lastBackup).TotalDays >= 7,
            BackupFrequency.Monthly => (now - lastBackup).TotalDays >= 30,
            _ => false
        };
    }

    private async Task PromptForAutoBackupAsync(BackupSettings settings)
    {
        // Ask user if they want to create a backup now
        var result = MessageBox.Show(
            "An automatic backup is due. Would you like to create a backup now?",
            "Backup Reminder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        // Get password from dialog
        var passwordDialog = new BackupPasswordDialog(isRestoreMode: false);
        passwordDialog.Owner = Application.Current.MainWindow;

        if (passwordDialog.ShowDialog() != true || string.IsNullOrEmpty(passwordDialog.Password))
            return;

        try
        {
            // Ensure backup folder exists
            if (!Directory.Exists(settings.BackupFolder))
                Directory.CreateDirectory(settings.BackupFolder);

            // Generate backup filename
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var backupPath = Path.Combine(settings.BackupFolder, $"backup_{timestamp}.billbackup");

            await _backupService.CreateBackupAsync(backupPath, passwordDialog.Password);

            // Update last backup time
            settings.LastAutoBackupTime = DateTime.UtcNow;
            _settingsService.Save();

            // Clean up old backups
            CleanupOldBackups(settings);

            MessageBox.Show(
                $"Backup created successfully!\n\n{backupPath}",
                "Backup Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to create backup:\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CleanupOldBackups(BackupSettings settings)
    {
        if (!Directory.Exists(settings.BackupFolder))
            return;

        var backupFiles = Directory.GetFiles(settings.BackupFolder, "*.billbackup")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        // Delete files exceeding max count
        var filesToDeleteByCount = backupFiles.Skip(settings.MaxBackupsToKeep);

        // Delete files older than max age
        var cutoffDate = DateTime.Now.AddDays(-settings.MaxBackupAgeDays);
        var filesToDeleteByAge = backupFiles.Where(f => f.CreationTime < cutoffDate);

        // Combine and delete
        var filesToDelete = filesToDeleteByCount.Union(filesToDeleteByAge).Distinct();

        foreach (var file in filesToDelete)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _timer?.Dispose();
        _isDisposed = true;
    }
}
