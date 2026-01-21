using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.ViewModels;

namespace BillApp.Views.Dialogs;

public partial class QuickAddDialog : Window
{
    private readonly IPayeeRepository _payeeRepository;
    private readonly IBillRepository _billRepository;
    private List<Payee> _payees = new();
    private List<PaymentMethodItem> _paymentMethods = new();
    private List<ParsedRowItem> _parsedRows = new();

    public QuickAddDialog(IPayeeRepository payeeRepository, IBillRepository billRepository)
    {
        InitializeComponent();
        _payeeRepository = payeeRepository;
        _billRepository = billRepository;

        // Populate day of month dropdown (1-31)
        for (int i = 1; i <= 31; i++)
        {
            DayOfMonthComboBox.Items.Add(i);
        }
        DayOfMonthComboBox.SelectedIndex = 0;

        Loaded += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        LoadingText.Text = "Loading...";
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            // Load active payees only
            var allPayees = await _payeeRepository.GetAllAsync();
            _payees = allPayees.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
            PayeeComboBox.ItemsSource = _payees;

            // Load payment methods (Cash + active payment accounts)
            var paymentAccounts = await _payeeRepository.GetPaymentAccountsAsync();
            _paymentMethods = new List<PaymentMethodItem>
            {
                new PaymentMethodItem { Name = "Cash", IsCash = true }
            };
            _paymentMethods.AddRange(paymentAccounts
                .Where(p => p.IsActive)
                .Select(p => new PaymentMethodItem
                {
                    AccountId = p.Id,
                    Name = p.Name
                }));
            PaymentMethodComboBox.ItemsSource = _paymentMethods;
            PaymentMethodComboBox.SelectedIndex = 0;
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void PayeeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }

    private void PasteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ParseAndValidate();
    }

    private void ParseAndValidate()
    {
        var text = PasteTextBox.Text;
        _parsedRows.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            PreviewList.ItemsSource = null;
            SummaryText.Text = "Select a payee and paste data to preview.";
            UpdateButtonState();
            return;
        }

        var selectedDay = (int)(DayOfMonthComboBox.SelectedItem ?? 1);
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int rowNum = 0;

        foreach (var line in lines)
        {
            rowNum++;
            var item = ParseLine(line, rowNum, selectedDay);
            _parsedRows.Add(item);
        }

        PreviewList.ItemsSource = _parsedRows.ToList();

        var validCount = _parsedRows.Count(r => r.IsValid);
        var totalCount = _parsedRows.Count;

        if (validCount == 0)
        {
            SummaryText.Text = $"No valid rows found. Each row needs a parseable date and amount.";
        }
        else
        {
            SummaryText.Text = $"Ready to add {validCount} of {totalCount} rows as paid bills.";
        }

        UpdateButtonState();
    }

    private ParsedRowItem ParseLine(string line, int rowNum, int dueDayOfMonth)
    {
        var item = new ParsedRowItem { RowNumber = rowNum.ToString() };

        // Split by tab first, then by multiple spaces as fallback
        var fields = line.Split('\t');
        if (fields.Length == 1)
        {
            // No tabs found, try splitting by 2+ spaces
            fields = Regex.Split(line.Trim(), @"\s{2,}");
        }

        DateTime? paidDate = null;
        decimal? amount = null;

        foreach (var field in fields)
        {
            var trimmed = field.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Try to parse as date if we don't have one yet
            if (!paidDate.HasValue && TryParseDate(trimmed, out var parsedDate))
            {
                paidDate = parsedDate;
                continue;
            }

            // Try to parse as amount if we don't have one yet
            if (!amount.HasValue && TryParseAmount(trimmed, out var parsedAmount))
            {
                amount = parsedAmount;
                continue;
            }
        }

        if (paidDate.HasValue && amount.HasValue)
        {
            item.PaidDate = paidDate.Value;
            item.Amount = Math.Abs(amount.Value);
            item.DueDate = CalculateDueDate(paidDate.Value, dueDayOfMonth);
            item.IsValid = true;
            item.Status = "Valid";
            item.PaidDateDisplay = paidDate.Value.ToString("M/d/yyyy");
            item.DueDateDisplay = $"Due: {item.DueDate.ToString("M/d/yyyy")}";
            item.AmountDisplay = item.Amount.ToString("C");
        }
        else
        {
            item.IsValid = false;
            var missing = new List<string>();
            if (!paidDate.HasValue) missing.Add("date");
            if (!amount.HasValue) missing.Add("amount");
            item.Status = $"Missing: {string.Join(", ", missing)}";
            item.PaidDateDisplay = paidDate?.ToString("M/d/yyyy") ?? "-";
            item.DueDateDisplay = "-";
            item.AmountDisplay = amount?.ToString("C") ?? "-";
        }

        return item;
    }

    private bool TryParseDate(string input, out DateTime result)
    {
        result = default;

        // Try various date formats
        string[] formats = new[]
        {
            "M/d/yyyy", "MM/dd/yyyy",
            "M/d/yy", "MM/dd/yy",
            "yyyy-MM-dd",
            "M-d-yyyy", "MM-dd-yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }
        }

        // Also try standard parsing as fallback
        return DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private bool TryParseAmount(string input, out decimal result)
    {
        result = 0;

        // Clean up the input
        var cleaned = input.Trim();

        // Handle accounting format negative: ($123.45) or (123.45)
        bool isAccountingNegative = cleaned.StartsWith("(") && cleaned.EndsWith(")");
        if (isAccountingNegative)
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

        // Handle leading negative sign: -$123.45 or -123.45
        bool hasLeadingMinus = cleaned.StartsWith("-");
        if (hasLeadingMinus)
        {
            cleaned = cleaned.Substring(1);
        }

        // Remove currency symbol and commas
        cleaned = cleaned.Replace("$", "").Replace(",", "").Trim();

        // Try to parse
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            // We always make the result positive (absolute value)
            result = Math.Abs(result);
            return true;
        }

        return false;
    }

    private DateTime CalculateDueDate(DateTime paidDate, int dayOfMonth)
    {
        var daysInMonth = DateTime.DaysInMonth(paidDate.Year, paidDate.Month);
        var actualDay = Math.Min(dayOfMonth, daysInMonth);
        return new DateTime(paidDate.Year, paidDate.Month, actualDay);
    }

    private void UpdateButtonState()
    {
        var hasPayee = PayeeComboBox.SelectedItem != null;
        var hasValidRows = _parsedRows.Any(r => r.IsValid);
        AddBillsButton.IsEnabled = hasPayee && hasValidRows;
    }

    private async void AddBillsButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPayee = PayeeComboBox.SelectedItem as Payee;
        if (selectedPayee == null) return;

        var validRows = _parsedRows.Where(r => r.IsValid).ToList();
        if (!validRows.Any()) return;

        var selectedPaymentMethod = PaymentMethodComboBox.SelectedItem as PaymentMethodItem;

        AddBillsButton.IsEnabled = false;
        LoadingText.Text = "Adding bills...";
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(50); // Allow UI to update

        try
        {
            int addedCount = 0;

            foreach (var row in validRows)
            {
                var bill = new Bill
                {
                    PayeeId = selectedPayee.Id,
                    AmountDue = row.Amount,
                    AmountPaid = row.Amount,
                    Balance = 0,
                    DueDate = row.DueDate,
                    PaidDate = row.PaidDate,
                    Status = PaymentStatus.Paid,
                    Frequency = RecurrenceFrequency.None,
                    IsCashPayment = selectedPaymentMethod?.IsCash ?? false,
                    PaymentAccountId = selectedPaymentMethod?.AccountId
                };

                await _billRepository.InsertAsync(bill);
                addedCount++;
            }

            MessageBox.Show(
                $"Successfully added {addedCount} bill(s).",
                "Quick Add Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to add bills: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            AddBillsButton.IsEnabled = true;
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

internal class ParsedRowItem
{
    public string RowNumber { get; set; } = "";
    public DateTime PaidDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsValid { get; set; }
    public string Status { get; set; } = "";
    public string PaidDateDisplay { get; set; } = "";
    public string DueDateDisplay { get; set; } = "";
    public string AmountDisplay { get; set; } = "";
}
