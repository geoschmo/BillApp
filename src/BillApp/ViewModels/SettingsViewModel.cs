using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BillApp.Core.Exceptions;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
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
    private readonly IImportService _importService;
    private readonly ISettingsService _settingsService;
    private readonly IPayeeRepository _payeeRepository;
    private readonly IBillRepository _billRepository;
    private readonly IAccountRepository _accountRepository;

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

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isReassigning;

    [ObservableProperty]
    private ObservableCollection<Payee> _payees = new();

    [ObservableProperty]
    private Payee? _sourcePayee;

    [ObservableProperty]
    private Payee? _targetPayee;

    [ObservableProperty]
    private string? _reassignmentResult;

    [ObservableProperty]
    private bool _isReassigningPaymentAccounts;

    [ObservableProperty]
    private ObservableCollection<Account> _paymentAccounts = new();

    [ObservableProperty]
    private Account? _sourcePaymentAccount;

    [ObservableProperty]
    private Account? _targetPaymentAccount;

    [ObservableProperty]
    private string? _paymentAccountReassignmentResult;

    public BackupFrequency[] FrequencyOptions => Enum.GetValues<BackupFrequency>();

    public string DatabaseFolder => Path.GetDirectoryName(LiteDbContext.GetDefaultDatabasePath()) ?? string.Empty;

    public SettingsViewModel(
        IBackupService backupService,
        IImportService importService,
        ISettingsService settingsService,
        IPayeeRepository payeeRepository,
        IBillRepository billRepository,
        IAccountRepository accountRepository)
    {
        _backupService = backupService;
        _importService = importService;
        _settingsService = settingsService;
        _payeeRepository = payeeRepository;
        _billRepository = billRepository;
        _accountRepository = accountRepository;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        LoadSettings();
        await LoadPayeesAsync();
        await LoadPaymentAccountsAsync();
        await base.OnNavigatedToAsync(parameter);
    }

    private async Task LoadPayeesAsync()
    {
        var payees = await _payeeRepository.GetAllAsync();
        Payees = new ObservableCollection<Payee>(payees.OrderBy(p => p.Name));
        SourcePayee = null;
        TargetPayee = null;
        ReassignmentResult = null;
    }

    private async Task LoadPaymentAccountsAsync()
    {
        var accounts = await _accountRepository.GetAllAsync();
        var paymentAccounts = accounts.Where(a => a.IsPaymentAccount).OrderBy(a => a.Name);
        PaymentAccounts = new ObservableCollection<Account>(paymentAccounts);
        SourcePaymentAccount = null;
        TargetPaymentAccount = null;
        PaymentAccountReassignmentResult = null;
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
    private async Task ImportDataAsync()
    {
        // Prompt for backup first
        var backupResult = MessageBox.Show(
            "It's recommended to create a backup before importing data.\n\nWould you like to create a backup now?",
            "Create Backup First?",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (backupResult == MessageBoxResult.Cancel)
            return;

        if (backupResult == MessageBoxResult.Yes)
        {
            await CreateBackupAsync();

            // If backup failed or was cancelled, abort import
            if (IsBackingUp)
                return;
        }

        // Show import dialog
        var importDialog = new ImportDialog(_importService);
        importDialog.Owner = Application.Current.MainWindow;

        if (importDialog.ShowDialog() == true && importDialog.ExecutionResult?.Success == true)
        {
            var result = importDialog.ExecutionResult;
            var message = $"Import completed successfully!\n\n" +
                         $"Accounts created: {result.AccountsCreated}\n" +
                         $"Bills created: {result.BillsCreated}\n" +
                         $"Payment accounts created: {result.PaymentAccountsCreated}\n\n" +
                         $"The application will now restart to apply changes.";

            MessageBox.Show(
                message,
                "Import Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Restart application
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
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

    [RelayCommand]
    private async Task ReassignPayeesAsync()
    {
        if (SourcePayee == null || TargetPayee == null)
        {
            ReassignmentResult = "Please select both source and target payees.";
            return;
        }

        if (SourcePayee.Id == TargetPayee.Id)
        {
            ReassignmentResult = "Source and target payees must be different.";
            return;
        }

        // Confirm the action
        var result = MessageBox.Show(
            $"This will reassign all bills from \"{SourcePayee.Name}\" to \"{TargetPayee.Name}\".\n\n" +
            $"This action cannot be undone. Continue?",
            "Confirm Payee Reassignment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsReassigning = true;
        ReassignmentResult = null;

        try
        {
            // Get all bills for the source payee
            var bills = await _billRepository.GetByPayeeAsync(SourcePayee.Id);
            var billsList = bills.ToList();

            if (billsList.Count == 0)
            {
                ReassignmentResult = $"No bills found for \"{SourcePayee.Name}\".";
                return;
            }

            // Update each bill to point to the target payee
            foreach (var bill in billsList)
            {
                bill.PayeeId = TargetPayee.Id;
                await _billRepository.UpdateAsync(bill);
            }

            ReassignmentResult = $"Successfully reassigned {billsList.Count} bill(s) from \"{SourcePayee.Name}\" to \"{TargetPayee.Name}\".";

            // Ask if user wants to delete the now-empty source payee
            var deleteResult = MessageBox.Show(
                $"Would you like to delete the payee \"{SourcePayee.Name}\" since it no longer has any bills?",
                "Delete Empty Payee?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (deleteResult == MessageBoxResult.Yes)
            {
                await _payeeRepository.DeleteAsync(SourcePayee.Id);
                ReassignmentResult += $"\n\"{SourcePayee.Name}\" was deleted.";
                await LoadPayeesAsync();
            }
            else
            {
                // Just clear the selections
                SourcePayee = null;
                TargetPayee = null;
            }
        }
        catch (Exception ex)
        {
            ReassignmentResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsReassigning = false;
        }
    }

    [RelayCommand]
    private async Task ReassignPaymentAccountsAsync()
    {
        if (SourcePaymentAccount == null || TargetPaymentAccount == null)
        {
            PaymentAccountReassignmentResult = "Please select both source and target payment accounts.";
            return;
        }

        if (SourcePaymentAccount.Id == TargetPaymentAccount.Id)
        {
            PaymentAccountReassignmentResult = "Source and target payment accounts must be different.";
            return;
        }

        // Confirm the action
        var result = MessageBox.Show(
            $"This will change the payment account on all bills paid with \"{SourcePaymentAccount.Name}\" to \"{TargetPaymentAccount.Name}\".\n\n" +
            $"This action cannot be undone. Continue?",
            "Confirm Payment Account Reassignment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsReassigningPaymentAccounts = true;
        PaymentAccountReassignmentResult = null;

        try
        {
            // Get all bills and filter for those with the source payment account
            var allBills = await _billRepository.GetAllAsync();
            var billsToUpdate = allBills.Where(b => b.PaymentAccountId == SourcePaymentAccount.Id).ToList();

            if (billsToUpdate.Count == 0)
            {
                PaymentAccountReassignmentResult = $"No bills found with payment account \"{SourcePaymentAccount.Name}\".";
                return;
            }

            // Update each bill to use the target payment account
            foreach (var bill in billsToUpdate)
            {
                bill.PaymentAccountId = TargetPaymentAccount.Id;
                await _billRepository.UpdateAsync(bill);
            }

            PaymentAccountReassignmentResult = $"Successfully updated {billsToUpdate.Count} bill(s) from \"{SourcePaymentAccount.Name}\" to \"{TargetPaymentAccount.Name}\".";

            // Clear selections
            SourcePaymentAccount = null;
            TargetPaymentAccount = null;
        }
        catch (Exception ex)
        {
            PaymentAccountReassignmentResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsReassigningPaymentAccounts = false;
        }
    }
}
