using System.Collections.ObjectModel;
using System.Windows;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using BillApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp.ViewModels;

public partial class BillListViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPayeeRepository _payeeRepository;
    private readonly INavigationService _navigationService;

    // Track original status for inline editing to detect status changes
    private readonly Dictionary<Guid, PaymentStatus> _originalStatuses = new();

    [ObservableProperty]
    private ObservableCollection<Bill> _bills = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<Payee> _payees = new();

    [ObservableProperty]
    private Bill? _selectedBill;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _filterStatus = "Active";

    [ObservableProperty]
    private Guid? _filterCategoryId;

    public BillListViewModel(
        IBillRepository billRepository,
        ICategoryRepository categoryRepository,
        IPayeeRepository payeeRepository,
        INavigationService navigationService)
    {
        _billRepository = billRepository;
        _categoryRepository = categoryRepository;
        _payeeRepository = payeeRepository;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Load categories for the filter dropdown
            var categories = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);

            // Load payees
            var payees = await _payeeRepository.GetAllAsync();
            Payees = new ObservableCollection<Payee>(payees);

            // Load all bills
            await LoadBillsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBillsAsync()
    {
        // Materialize the list to avoid lazy evaluation issues
        var bills = (await _billRepository.GetAllAsync()).ToList();

        // Build lookup dictionaries
        var payeeLookup = Payees.ToDictionary(p => p.Id);
        var categoryLookup = Categories.ToDictionary(c => c.Id);

        // Load payee and category for each bill and track original statuses
        _originalStatuses.Clear();
        foreach (var bill in bills)
        {
            // Load payee
            if (payeeLookup.TryGetValue(bill.PayeeId, out var payee))
            {
                bill.Payee = payee;
                // Also load payee's category
                if (payee.CategoryId.HasValue && categoryLookup.TryGetValue(payee.CategoryId.Value, out var payeeCategory))
                {
                    payee.Category = payeeCategory;
                }
            }

            // Load payment account (now a Payee)
            if (bill.PaymentAccountId.HasValue && payeeLookup.TryGetValue(bill.PaymentAccountId.Value, out var paymentAccount))
            {
                bill.PaymentAccount = paymentAccount;
            }

            _originalStatuses[bill.Id] = bill.Status;
        }

        // Apply filters
        var filtered = bills.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(b =>
                (b.Payee?.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.Notes?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply status filter
        filtered = FilterStatus switch
        {
            "Active" => filtered.Where(b => b.Status == PaymentStatus.Pending || b.Status == PaymentStatus.Overdue),
            "Pending" => filtered.Where(b => b.Status == PaymentStatus.Pending),
            "Overdue" => filtered.Where(b => b.Status == PaymentStatus.Overdue),
            "Paid" => filtered.Where(b => b.Status == PaymentStatus.Paid),
            _ => filtered // "All" or any other value shows everything
        };

        if (FilterCategoryId.HasValue)
        {
            filtered = filtered.Where(b => b.Payee?.CategoryId == FilterCategoryId.Value);
        }

        // Sort by due date
        Bills = new ObservableCollection<Bill>(filtered.OrderBy(b => b.DueDate));
    }

    [RelayCommand]
    private async Task AddBillAsync()
    {
        await _navigationService.NavigateToAsync<BillEditViewModel>();
    }

    [RelayCommand]
    private async Task QuickAddBillsAsync()
    {
        var payeeRepo = App.Services.GetRequiredService<IPayeeRepository>();
        var billRepo = App.Services.GetRequiredService<IBillRepository>();

        var dialog = new QuickAddDialog(payeeRepo, billRepo);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            await LoadBillsAsync();
        }
    }

    [RelayCommand]
    private async Task EditBillAsync(Bill? bill)
    {
        if (bill != null)
        {
            await _navigationService.NavigateToAsync<BillEditViewModel>(bill.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteBillAsync(Bill? bill)
    {
        if (bill == null) return;

        // TODO: Add confirmation dialog
        await _billRepository.DeleteAsync(bill.Id);
        Bills.Remove(bill);
    }

    [RelayCommand]
    private async Task MarkAsPaidAsync(Bill? bill)
    {
        if (bill == null) return;

        // Build payment methods list (None, Cash, then payment accounts)
        var paymentAccounts = await _payeeRepository.GetPaymentAccountsAsync();
        var paymentMethods = new List<PaymentMethodItem>
        {
            new PaymentMethodItem { Name = "(None)" },
            new PaymentMethodItem { Name = "Cash", IsCash = true }
        };
        paymentMethods.AddRange(paymentAccounts
            .Where(p => p.IsActive)
            .Select(p => new PaymentMethodItem { AccountId = p.Id, Name = p.Name }));

        // Determine default payment method from bill's existing payment info
        PaymentMethodItem? defaultPaymentMethod = null;
        if (bill.IsCashPayment)
        {
            defaultPaymentMethod = new PaymentMethodItem { Name = "Cash", IsCash = true };
        }
        else if (bill.PaymentAccountId.HasValue)
        {
            defaultPaymentMethod = new PaymentMethodItem { AccountId = bill.PaymentAccountId.Value };
        }

        // Fetch the most recent paid bill for this payee (excluding current)
        var lastPaidBill = (await _billRepository.GetAllAsync())
            .Where(b => b.PayeeId == bill.PayeeId && b.Status == PaymentStatus.Paid && b.Id != bill.Id)
            .OrderByDescending(b => b.PaidDate ?? b.DueDate)
            .FirstOrDefault();

        string? lastPaidMethod = null;
        if (lastPaidBill != null)
        {
            if (lastPaidBill.IsCashPayment)
            {
                lastPaidMethod = "Cash";
            }
            else if (lastPaidBill.PaymentAccountId.HasValue)
            {
                var lastPaidAccount = await _payeeRepository.GetByIdAsync(lastPaidBill.PaymentAccountId.Value);
                lastPaidMethod = lastPaidAccount?.Name ?? "Account";
            }
            else
            {
                lastPaidMethod = "(None)";
            }
        }

        // Show pay dialog with default values
        var payeeName = bill.Payee?.Name ?? "Unknown";
        var dialog = new PayBillDialog(
            payeeName,
            bill.AmountDue,
            bill.Balance,
            bill.DueDate,
            paymentMethods,
            defaultPaymentMethod,
            lastPaidBill?.AmountDue,
            lastPaidBill?.AmountPaid,
            lastPaidBill?.PaidDate,
            lastPaidMethod);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() != true)
            return;

        // Mark current bill as paid with values from dialog
        bill.Status = PaymentStatus.Paid;
        bill.Balance = dialog.Balance;
        bill.AmountPaid = dialog.PaidAmount;
        bill.PaidDate = dialog.PaidDate;
        bill.Confirmation = dialog.Confirmation;

        // Set payment method
        if (dialog.SelectedPaymentMethod != null)
        {
            bill.IsCashPayment = dialog.SelectedPaymentMethod.IsCash;
            bill.PaymentAccountId = dialog.SelectedPaymentMethod.AccountId;
        }

        await _billRepository.UpdateAsync(bill);

        var payee = await _payeeRepository.GetByIdAsync(bill.PayeeId);

        // Update payment account balance
        if (!bill.IsCashPayment && bill.PaymentAccountId.HasValue)
        {
            var paymentAcct = await _payeeRepository.GetByIdAsync(bill.PaymentAccountId.Value);
            if (paymentAcct?.IsAccount == true)
            {
                if (paymentAcct.IsAsset)
                    paymentAcct.Balance = Math.Max(0, paymentAcct.Balance - bill.AmountPaid); // Checking decreases, min 0
                else if (paymentAcct.IsLiability)
                    paymentAcct.Balance += bill.AmountPaid; // Credit card increases
                await _payeeRepository.UpdateAsync(paymentAcct);
            }
        }

        // If this is a recurring bill, create the next occurrence
        if (bill.IsRecurring)
        {
            // Only carry balance forward for financial accounts
            var carryBalance = payee?.IsAccount == true;
            var nextBill = bill.CreateNextRecurrence(carryBalance);
            await _billRepository.InsertAsync(nextBill);

            // Update the payee account balance to match the new bill's balance
            if (carryBalance && payee != null)
            {
                payee.Balance = nextBill.Balance;
                await _payeeRepository.UpdateAsync(payee);
            }
        }

        // Refresh the list to update UI
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task CreateNewBillFromExistingAsync(Bill? bill)
    {
        if (bill == null) return;

        // Calculate suggested values based on the existing bill
        var suggestedDueDate = bill.Frequency switch
        {
            RecurrenceFrequency.Weekly => bill.DueDate.AddDays(7),
            RecurrenceFrequency.BiWeekly => bill.DueDate.AddDays(14),
            RecurrenceFrequency.Monthly => bill.DueDate.AddMonths(1),
            RecurrenceFrequency.Quarterly => bill.DueDate.AddMonths(3),
            RecurrenceFrequency.SemiAnnually => bill.DueDate.AddMonths(6),
            RecurrenceFrequency.Annually => bill.DueDate.AddYears(1),
            _ => DateTime.Today.AddMonths(1) // Default to one month from today for non-recurring
        };

        var suggestedAmountDue = bill.AmountDue;
        // Only carry balance forward for financial accounts
        var isFinancialAccount = bill.Payee?.IsAccount == true;
        var suggestedBalance = isFinancialAccount ? Math.Max(0, bill.Balance - bill.AmountPaid) : 0;

        // Show the dialog
        var payeeName = bill.Payee?.Name ?? "Unknown";
        var dialog = new NewBillFromExistingDialog(
            payeeName,
            suggestedDueDate,
            suggestedAmountDue,
            suggestedBalance < 0 ? 0 : suggestedBalance);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() != true)
            return;

        // Create the new bill
        var newBill = new Bill
        {
            PayeeId = bill.PayeeId,
            AmountDue = dialog.AmountDue,
            AmountPaid = 0,
            Balance = dialog.Balance,
            DueDate = dialog.DueDate,
            Status = PaymentStatus.Pending,
            Frequency = bill.Frequency,
            Notes = bill.Notes,
            ColorCode = bill.ColorCode,
            PreviousBillId = bill.Id
        };

        await _billRepository.InsertAsync(newBill);

        // Refresh the list to show the new bill
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        FilterStatus = "Active";
        FilterCategoryId = null;
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task SaveBillInlineAsync(Bill? bill)
    {
        if (bill == null) return;

        try
        {
            // Check if status is being changed to Paid
            bool beingMarkedAsPaid = bill.Status == PaymentStatus.Paid &&
                _originalStatuses.TryGetValue(bill.Id, out var originalStatus) &&
                originalStatus != PaymentStatus.Paid;

            // If being marked as paid, set paid date and default amount paid
            if (beingMarkedAsPaid)
            {
                bill.PaidDate ??= DateTime.Now;
                if (bill.AmountPaid == 0)
                {
                    bill.AmountPaid = bill.AmountDue;
                }
            }

            await _billRepository.UpdateAsync(bill);

            // Create next recurring bill if being marked as paid and is recurring
            if (beingMarkedAsPaid && bill.IsRecurring)
            {
                var carryBalance = bill.Payee?.IsAccount == true;
                var nextBill = bill.CreateNextRecurrence(carryBalance);
                await _billRepository.InsertAsync(nextBill);

                // Update the payee account balance to match the new bill's balance
                if (carryBalance && bill.Payee != null)
                {
                    var payee = await _payeeRepository.GetByIdAsync(bill.PayeeId);
                    if (payee != null)
                    {
                        payee.Balance = nextBill.Balance;
                        await _payeeRepository.UpdateAsync(payee);
                    }
                }

                await LoadBillsAsync(); // Refresh to show the new bill
            }
            else
            {
                // Update the tracked original status
                _originalStatuses[bill.Id] = bill.Status;

                // Refresh the item in the collection to update the UI
                // (Bill doesn't implement INotifyPropertyChanged)
                var index = Bills.IndexOf(bill);
                if (index >= 0)
                {
                    Bills.RemoveAt(index);
                    Bills.Insert(index, bill);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
        }
    }

    // Status options for the filter dropdown
    public IEnumerable<string> StatusFilterOptions => new[] { "Active", "All", "Pending", "Overdue", "Paid" };

    // Status options for inline editing (the actual enum values)
    public IEnumerable<PaymentStatus> StatusOptions => Enum.GetValues<PaymentStatus>();

    // Frequency options for inline editing
    public IEnumerable<RecurrenceFrequency> FrequencyOptions => Enum.GetValues<RecurrenceFrequency>();
}
