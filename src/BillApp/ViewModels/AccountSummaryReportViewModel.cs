using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class AccountSummaryReportViewModel : ViewModelBase
{
    private readonly IPayeeRepository _payeeRepository;
    private readonly IBillRepository _billRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _title = "Account Summary Report";

    [ObservableProperty]
    private ObservableCollection<Payee> _accounts = new();

    [ObservableProperty]
    private Payee? _selectedAccount;

    [ObservableProperty]
    private int _billHistoryCount = 15;

    [ObservableProperty]
    private ObservableCollection<BillReportItem> _bills = new();

    // Decrypted account details for display
    [ObservableProperty]
    private string? _accountName;

    [ObservableProperty]
    private string? _accountNumber;

    [ObservableProperty]
    private string? _paymentUrl;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _accountType;

    [ObservableProperty]
    private string? _institution;

    [ObservableProperty]
    private decimal _balance;

    [ObservableProperty]
    private decimal? _creditLimit;

    [ObservableProperty]
    private decimal? _interestRate;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private bool _showReport;

    [ObservableProperty]
    private ObservableCollection<int> _blankLines = new();

    public AccountSummaryReportViewModel(
        IPayeeRepository payeeRepository,
        IBillRepository billRepository,
        IEncryptionService encryptionService,
        INavigationService navigationService)
    {
        _payeeRepository = payeeRepository;
        _billRepository = billRepository;
        _encryptionService = encryptionService;
        _navigationService = navigationService;
    }

    public int[] BillHistoryOptions { get; } = { 5, 10, 15, 20, 25, 30, 50 };

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        IsLoading = true;
        ShowReport = false;

        try
        {
            // Load all active payees (accounts)
            var allPayees = await _payeeRepository.GetAllWithCategoriesAsync();
            var activePayees = allPayees.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
            Accounts = new ObservableCollection<Payee>(activePayees);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedAccount == null)
            return;

        IsLoading = true;

        try
        {
            var account = SelectedAccount;

            // Set account details
            AccountName = account.Name;
            AccountNumber = account.AccountNumber;
            PaymentUrl = account.PaymentUrl;
            Username = DecryptIfNotEmpty(account.Username);
            Password = DecryptIfNotEmpty(account.Password);
            Notes = account.Notes;
            AccountType = account.IsAccount ? account.AccountType?.ToString() : null;
            Institution = account.Institution;
            Balance = account.Balance;
            CreditLimit = account.CreditLimit;
            InterestRate = account.InterestRate;
            Category = account.Category?.Name;

            // Get bills for this account
            var allBills = await _billRepository.GetByPayeeAsync(account.Id);
            var paymentAccounts = (await _payeeRepository.GetPaymentAccountsAsync()).ToList();

            // Sort: paid bills oldest first, then pending bills by due date
            var paidBills = allBills
                .Where(b => b.Status == PaymentStatus.Paid)
                .OrderBy(b => b.PaidDate ?? b.DueDate)
                .ToList();

            var pendingBills = allBills
                .Where(b => b.Status == PaymentStatus.Pending)
                .OrderBy(b => b.DueDate)
                .ToList();

            // Take the last N paid bills (most recent) and then append pending
            var recentPaidBills = paidBills
                .Skip(Math.Max(0, paidBills.Count - BillHistoryCount))
                .ToList();

            var combinedBills = recentPaidBills.Concat(pendingBills).ToList();

            // Convert to report items
            var billItems = combinedBills.Select(b =>
            {
                var paymentMethod = "N/A";
                if (b.IsCashPayment)
                {
                    paymentMethod = "Cash";
                }
                else if (b.PaymentAccountId.HasValue)
                {
                    var paymentAccount = paymentAccounts.FirstOrDefault(p => p.Id == b.PaymentAccountId.Value);
                    paymentMethod = paymentAccount?.Name ?? "Unknown";
                }

                return new BillReportItem
                {
                    DueDate = b.DueDate,
                    PaidDate = b.PaidDate,
                    AmountDue = b.AmountDue,
                    AmountPaid = b.AmountPaid,
                    Balance = b.Balance,
                    PaymentMethod = paymentMethod,
                    Status = b.Status
                };
            }).ToList();

            Bills = new ObservableCollection<BillReportItem>(billItems);

            // Calculate blank lines to fill the page (approx 28 total lines fit on page)
            const int maxLinesOnPage = 28;
            var blankLineCount = Math.Max(0, maxLinesOnPage - billItems.Count);
            BlankLines = new ObservableCollection<int>(Enumerable.Range(0, blankLineCount));

            ShowReport = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Print(Visual? visual)
    {
        if (visual == null)
            return;

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            printDialog.PrintVisual(visual, $"Account Summary - {AccountName}");
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await _navigationService.GoBackAsync();
    }

    private string? DecryptIfNotEmpty(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        try
        {
            return _encryptionService.Decrypt(value);
        }
        catch
        {
            return value;
        }
    }
}

/// <summary>
/// Represents a bill item for display in the report.
/// </summary>
public class BillReportItem
{
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }

    public string StatusDisplay => Status == PaymentStatus.Pending ? "Pending" : "Paid";
    public string PaidStatusDisplay => Status == PaymentStatus.Paid ? "Paid" : "";

    // Display properties that show blank for zero/null values
    public string PaidDateDisplay => PaidDate.HasValue ? PaidDate.Value.ToString("MM/dd/yy") : "";
    public string AmountDueDisplay => AmountDue != 0 ? AmountDue.ToString("N2") : "";
    public string AmountPaidDisplay => AmountPaid != 0 ? AmountPaid.ToString("N2") : "";
    public string BalanceDisplay => Balance != 0 ? Balance.ToString("N2") : "";
}
