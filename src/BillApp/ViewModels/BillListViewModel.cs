using System.Collections.ObjectModel;
using System.Windows;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using BillApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class BillListViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly INavigationService _navigationService;

    // Track original status for inline editing to detect status changes
    private readonly Dictionary<Guid, PaymentStatus> _originalStatuses = new();

    [ObservableProperty]
    private ObservableCollection<Bill> _bills = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<Account> _accounts = new();

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
        IAccountRepository accountRepository,
        INavigationService navigationService)
    {
        _billRepository = billRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
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

            // Load accounts for display with a "None" option for inline editing
            var accounts = await _accountRepository.GetAllAsync();
            var accountList = new List<Account>
            {
                new Account { Id = Guid.Empty, Name = "(None)" }
            };
            accountList.AddRange(accounts);
            Accounts = new ObservableCollection<Account>(accountList);

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

        // Load category and account for each bill and track original statuses
        _originalStatuses.Clear();
        foreach (var bill in bills)
        {
            if (bill.CategoryId.HasValue && bill.CategoryId.Value != Guid.Empty)
            {
                bill.Category = Categories.FirstOrDefault(c => c.Id == bill.CategoryId.Value);
            }
            if (bill.AccountId.HasValue && bill.AccountId.Value != Guid.Empty)
            {
                bill.Account = Accounts.FirstOrDefault(a => a.Id == bill.AccountId.Value);
            }
            _originalStatuses[bill.Id] = bill.Status;
        }

        // Apply filters
        var filtered = bills.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(b =>
                b.Payee.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
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
            filtered = filtered.Where(b => b.CategoryId == FilterCategoryId.Value);
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

        // Show pay dialog with default values
        var dialog = new PayBillDialog(bill.Payee, bill.AmountDue, bill.DueDate);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() != true)
            return;

        // Mark current bill as paid with values from dialog
        bill.Status = PaymentStatus.Paid;
        bill.AmountPaid = dialog.PaidAmount;
        bill.PaidDate = dialog.PaidDate;

        await _billRepository.UpdateAsync(bill);

        // If this is a recurring bill, create the next occurrence
        if (bill.IsRecurring)
        {
            var nextBill = bill.CreateNextRecurrence();
            await _billRepository.InsertAsync(nextBill);

            // Update linked account balance to match new bill balance
            if (nextBill.AccountId.HasValue && nextBill.AccountId.Value != Guid.Empty)
            {
                var account = await _accountRepository.GetByIdAsync(nextBill.AccountId.Value);
                if (account != null)
                {
                    account.Balance = nextBill.Balance;
                    await _accountRepository.UpdateAsync(account);
                }
            }
        }

        // Refresh the list to update UI
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

            // Update the category reference
            if (bill.CategoryId.HasValue)
            {
                bill.Category = Categories.FirstOrDefault(c => c.Id == bill.CategoryId.Value);
            }
            else
            {
                bill.Category = null;
            }

            // Update the account reference - convert Guid.Empty to null (the "None" option)
            if (bill.AccountId == Guid.Empty)
            {
                bill.AccountId = null;
                bill.Account = null;
            }
            else if (bill.AccountId.HasValue)
            {
                bill.Account = Accounts.FirstOrDefault(a => a.Id == bill.AccountId.Value);
            }
            else
            {
                bill.Account = null;
            }

            await _billRepository.UpdateAsync(bill);

            // Create next recurring bill if being marked as paid and is recurring
            if (beingMarkedAsPaid && bill.IsRecurring)
            {
                var nextBill = bill.CreateNextRecurrence();
                await _billRepository.InsertAsync(nextBill);

                // Update linked account balance to match new bill balance
                if (nextBill.AccountId.HasValue && nextBill.AccountId.Value != Guid.Empty)
                {
                    var account = await _accountRepository.GetByIdAsync(nextBill.AccountId.Value);
                    if (account != null)
                    {
                        account.Balance = nextBill.Balance;
                        await _accountRepository.UpdateAsync(account);
                    }
                }

                await LoadBillsAsync(); // Refresh to show the new bill
            }
            else
            {
                // Sync pending bill balance to linked account
                if (bill.Status == PaymentStatus.Pending &&
                    bill.AccountId.HasValue && bill.AccountId.Value != Guid.Empty)
                {
                    var account = await _accountRepository.GetByIdAsync(bill.AccountId.Value);
                    if (account != null)
                    {
                        account.Balance = bill.Balance;
                        await _accountRepository.UpdateAsync(account);
                    }
                }

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
