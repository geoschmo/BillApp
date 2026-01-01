using System.Collections.ObjectModel;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class BillListViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly INavigationService _navigationService;

    // Track original status for inline editing to detect status changes
    private readonly Dictionary<Guid, PaymentStatus> _originalStatuses = new();

    [ObservableProperty]
    private ObservableCollection<Bill> _bills = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private Bill? _selectedBill;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private PaymentStatus? _filterStatus;

    [ObservableProperty]
    private Guid? _filterCategoryId;

    public BillListViewModel(
        IBillRepository billRepository,
        ICategoryRepository categoryRepository,
        INavigationService navigationService)
    {
        _billRepository = billRepository;
        _categoryRepository = categoryRepository;
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
        var bills = await _billRepository.GetAllAsync();

        // Load category for each bill and track original statuses
        _originalStatuses.Clear();
        foreach (var bill in bills)
        {
            if (bill.CategoryId.HasValue)
            {
                bill.Category = Categories.FirstOrDefault(c => c.Id == bill.CategoryId);
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

        if (FilterStatus.HasValue)
        {
            filtered = filtered.Where(b => b.Status == FilterStatus.Value);
        }

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

        // Mark current bill as paid
        bill.Status = PaymentStatus.Paid;
        bill.PaidDate = DateTime.Now;

        // If amount paid wasn't set, default to amount due
        if (bill.AmountPaid == 0)
        {
            bill.AmountPaid = bill.AmountDue;
        }

        await _billRepository.UpdateAsync(bill);

        // If this is a recurring bill, create the next occurrence
        if (bill.IsRecurring)
        {
            var nextBill = bill.CreateNextRecurrence();
            await _billRepository.InsertAsync(nextBill);
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
        FilterStatus = null;
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
                bill.Category = Categories.FirstOrDefault(c => c.Id == bill.CategoryId);
            }
            else
            {
                bill.Category = null;
            }

            await _billRepository.UpdateAsync(bill);

            // Create next recurring bill if being marked as paid and is recurring
            if (beingMarkedAsPaid && bill.IsRecurring)
            {
                var nextBill = bill.CreateNextRecurrence();
                await _billRepository.InsertAsync(nextBill);
                await LoadBillsAsync(); // Refresh to show the new bill
            }
            else
            {
                // Update the tracked original status
                _originalStatuses[bill.Id] = bill.Status;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
        }
    }

    // Status options for the filter dropdown
    public IEnumerable<PaymentStatus> StatusOptions => Enum.GetValues<PaymentStatus>();

    // Frequency options for inline editing
    public IEnumerable<RecurrenceFrequency> FrequencyOptions => Enum.GetValues<RecurrenceFrequency>();
}
