using System.IO;
using System.Windows;
using BillApp.Core.Exceptions;
using BillApp.Core.Interfaces.Services;
using BillApp.Infrastructure.Data;
using BillApp.Infrastructure.Services;
using BillApp.Services;
using BillApp.Settings;
using BillApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BillApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _title = "Settings";

    [ObservableProperty]
    private string _backupFolder = string.Empty;

    [ObservableProperty]
    private bool _autoBackupEnabled;

    [ObservableProperty]
    private BackupFrequency _selectedFrequency = BackupFrequency.Weekly;

    [ObservableProperty]
    private int _maxBackupsToKeep = 10;

    [ObservableProperty]
    private int _maxBackupAgeDays = 90;

    [ObservableProperty]
    private string? _lastBackupInfo;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private bool _isRestoring;

    public BackupFrequency[] FrequencyOptions => Enum.GetValues<BackupFrequency>();

    public string DatabaseFolder => Path.GetDirectoryName(LiteDbContext.GetDefaultDatabasePath()) ?? string.Empty;

    public SettingsViewModel(IBackupService backupService, ISettingsService settingsService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
    }

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        LoadSettings();
        return base.OnNavigatedToAsync(parameter);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings.BackupSettings;
        BackupFolder = settings.BackupFolder;
        AutoBackupEnabled = settings.AutoBackupEnabled;
        SelectedFrequency = settings.Frequency;
        MaxBackupsToKeep = settings.MaxBackupsToKeep;
        MaxBackupAgeDays = settings.MaxBackupAgeDays;

        if (settings.LastAutoBackupTime.HasValue)
        {
            LastBackupInfo = $"Last backup: {settings.LastAutoBackupTime.Value.ToLocalTime():g}";
        }
        else
        {
            LastBackupInfo = "No backups created yet";
        }
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Settings.BackupSettings;
        settings.BackupFolder = BackupFolder;
        settings.AutoBackupEnabled = AutoBackupEnabled;
        settings.Frequency = SelectedFrequency;
        settings.MaxBackupsToKeep = MaxBackupsToKeep;
        settings.MaxBackupAgeDays = MaxBackupAgeDays;
        _settingsService.Save();
    }

    partial void OnAutoBackupEnabledChanged(bool value) => SaveSettings();
    partial void OnSelectedFrequencyChanged(BackupFrequency value) => SaveSettings();
    partial void OnMaxBackupsToKeepChanged(int value) => SaveSettings();
    partial void OnMaxBackupAgeDaysChanged(int value) => SaveSettings();

    [RelayCommand]
    private void BrowseBackupFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Backup Folder",
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            BackupFolder = dialog.FolderName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        // Get password from dialog
        var passwordDialog = new BackupPasswordDialog(isRestoreMode: false);
        passwordDialog.Owner = Application.Current.MainWindow;

        if (passwordDialog.ShowDialog() != true || string.IsNullOrEmpty(passwordDialog.Password))
            return;

        IsBackingUp = true;

        try
        {
            // Ensure backup folder exists
            if (!Directory.Exists(BackupFolder))
                Directory.CreateDirectory(BackupFolder);

            // Generate backup filename
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var backupPath = Path.Combine(BackupFolder, $"backup_{timestamp}.billbackup");

            await _backupService.CreateBackupAsync(backupPath, passwordDialog.Password);

            // Update last backup time
            _settingsService.Settings.BackupSettings.LastAutoBackupTime = DateTime.UtcNow;
            _settingsService.Save();
            LoadSettings();

            MessageBox.Show(
                $"Backup created successfully!\n\n{backupPath}",
                "Backup Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Clean up old backups
            await CleanupOldBackupsAsync();
        }
        catch (WeakPasswordException ex)
        {
            MessageBox.Show(
                ex.Message,
                "Password Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to create backup:\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        // Select backup file
        var openDialog = new OpenFileDialog
        {
            Title = "Select Backup File",
            Filter = "BillApp Backups (*.billbackup)|*.billbackup|All Files (*.*)|*.*",
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openDialog.ShowDialog() != true)
            return;

        try
        {
            // Read manifest to show details
            var manifest = await BackupService.ReadManifestAsync(openDialog.FileName);

            // Show confirmation dialog
            var confirmDialog = new RestoreConfirmationDialog(manifest);
            confirmDialog.Owner = Application.Current.MainWindow;

            if (confirmDialog.ShowDialog() != true)
                return;

            // Get password
            var passwordDialog = new BackupPasswordDialog(isRestoreMode: true);
            passwordDialog.Owner = Application.Current.MainWindow;

            if (passwordDialog.ShowDialog() != true || string.IsNullOrEmpty(passwordDialog.Password))
                return;

            IsRestoring = true;

            await _backupService.RestoreBackupAsync(openDialog.FileName, passwordDialog.Password);

            MessageBox.Show(
                "Backup restored successfully!\n\nThe application will now restart to apply changes.",
                "Restore Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Restart application
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
        }
        catch (InvalidBackupPasswordException)
        {
            MessageBox.Show(
                "The password is incorrect.",
                "Password Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (CorruptedBackupException ex)
        {
            MessageBox.Show(
                $"The backup file is corrupted:\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (IncompatibleBackupVersionException ex)
        {
            MessageBox.Show(
                $"This backup was created with an incompatible version:\n\n{ex.Message}",
                "Version Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to restore backup:\n\n{ex.Message}",
                "Restore Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsRestoring = false;
        }
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        if (Directory.Exists(BackupFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", BackupFolder);
        }
        else
        {
            MessageBox.Show(
                "Backup folder does not exist yet. Create a backup first.",
                "Folder Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void OpenDatabaseFolder()
    {
        if (Directory.Exists(DatabaseFolder))
        {
            System.Diagnostics.Process.Start("explorer.exe", DatabaseFolder);
        }
    }

    private async Task CleanupOldBackupsAsync()
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(BackupFolder))
                return;

            var backupFiles = Directory.GetFiles(BackupFolder, "*.billbackup")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // Delete files exceeding max count
            var filesToDeleteByCount = backupFiles.Skip(MaxBackupsToKeep);

            // Delete files older than max age
            var cutoffDate = DateTime.Now.AddDays(-MaxBackupAgeDays);
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
        });
    }
}
