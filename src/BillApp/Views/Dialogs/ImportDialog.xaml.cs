using System.Windows;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
using Microsoft.Win32;

namespace BillApp.Views.Dialogs;

public partial class ImportDialog : Window
{
    private readonly IImportService _importService;
    private ImportValidationResult? _validationResult;
    private string? _currentFilePath;

    public ImportMode SelectedMode => ReplaceAllRadio.IsChecked == true
        ? ImportMode.ReplaceAll
        : ImportMode.AddToExisting;

    public ImportExecutionResult? ExecutionResult { get; private set; }

    public ImportDialog(IImportService importService)
    {
        InitializeComponent();
        _importService = importService;

        SummaryText.Text = "Select a CSV file to import.";

        // Re-validate when import mode changes
        AddToExistingRadio.Checked += ImportModeChanged;
        ReplaceAllRadio.Checked += ImportModeChanged;
    }

    private void ImportModeChanged(object sender, RoutedEventArgs e)
    {
        // Re-validate if we have a file selected
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            ValidateFile(_currentFilePath);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Select Import File"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFilePath = dialog.FileName;
            FilePathBox.Text = dialog.FileName;
            ValidateFile(dialog.FileName);
        }
    }

    private async void ValidateFile(string filePath)
    {
        ImportButton.IsEnabled = false;
        SummaryText.Text = "Validating file...";
        ValidationList.ItemsSource = null;

        // Show loading overlay and force UI refresh
        LoadingText.Text = "Validating file...";
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(50); // Allow UI to update

        try
        {
            var mode = SelectedMode;
            _validationResult = await Task.Run(() => _importService.ValidateFileAsync(filePath, mode));

            // Convert to display items
            var displayItems = _validationResult.ValidationItems
                .Select(v => new ValidationDisplayItem
                {
                    RowDisplay = v.RowNumber > 0 ? $"Row {v.RowNumber}" : "",
                    Severity = v.Severity.ToString(),
                    Message = $"[{v.Field}] {v.Message}"
                })
                .ToList();

            ValidationList.ItemsSource = displayItems;

            // Update summary
            var summaryParts = new List<string>();

            if (_validationResult.Payees.Count > 0)
                summaryParts.Add($"{_validationResult.Payees.Count} payee(s)");

            if (_validationResult.Bills.Count > 0)
                summaryParts.Add($"{_validationResult.Bills.Count} bill(s)");

            if (_validationResult.PaymentAccountsToCreate.Count > 0)
                summaryParts.Add($"{_validationResult.PaymentAccountsToCreate.Count} payment account(s) to create");

            if (_validationResult.ErrorCount > 0)
            {
                SummaryText.Text = $"Cannot import: {_validationResult.ErrorCount} error(s) found. Please fix the CSV file and try again.";
                ImportButton.IsEnabled = false;
            }
            else if (_validationResult.Bills.Count == 0)
            {
                SummaryText.Text = "No valid data to import.";
                ImportButton.IsEnabled = false;
            }
            else
            {
                var warningText = _validationResult.WarningCount > 0
                    ? $" ({_validationResult.WarningCount} warning(s))"
                    : "";

                SummaryText.Text = $"Ready to import: {string.Join(", ", summaryParts)}.{warningText}";
                ImportButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Error reading file: {ex.Message}";
            ImportButton.IsEnabled = false;
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_validationResult == null || !_validationResult.IsValid)
            return;

        // Confirm if replacing all
        if (SelectedMode == ImportMode.ReplaceAll)
        {
            var confirmResult = MessageBox.Show(
                "This will DELETE all existing payees and bills before importing. Are you sure?",
                "Confirm Replace All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;
        }

        ImportButton.IsEnabled = false;
        SummaryText.Text = "Importing...";

        // Capture mode before background thread
        var mode = SelectedMode;

        // Show loading overlay and force UI refresh
        LoadingText.Text = "Importing data...";
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(50); // Allow UI to update

        try
        {
            ExecutionResult = await Task.Run(() => _importService.ExecuteImportAsync(_validationResult, mode));

            if (ExecutionResult.Success)
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show(
                    $"Import failed: {ExecutionResult.ErrorMessage}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ImportButton.IsEnabled = true;
                SummaryText.Text = "Import failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Import failed: {ex.Message}",
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ImportButton.IsEnabled = true;
            SummaryText.Text = "Import failed. Please try again.";
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

internal class ValidationDisplayItem
{
    public string RowDisplay { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
}
