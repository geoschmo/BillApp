using System.Collections.ObjectModel;
using System.Windows;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class PayeeListViewModel : ViewModelBase
{
    private readonly IPayeeRepository _payeeRepository;
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<Payee> _payees = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private Payee? _selectedPayee;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _filterType = "All";

    [ObservableProperty]
    private bool _showInactive;

    // Account summary values
    [ObservableProperty]
    private decimal _totalAssets;

    [ObservableProperty]
    private decimal _totalLiabilities;

    [ObservableProperty]
    private decimal _netWorth;

    public PayeeListViewModel(
        IPayeeRepository payeeRepository,
        IBillRepository billRepository,
        ICategoryRepository categoryRepository,
        INavigationService navigationService)
    {
        _payeeRepository = payeeRepository;
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
            // Load categories for display
            var categories = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);
            var categoryLookup = categories.ToDictionary(c => c.Id);

            // Load payees with categories
            var payees = (await _payeeRepository.GetAllAsync()).ToList();

            // Set category navigation property
            foreach (var payee in payees)
            {
                if (payee.CategoryId.HasValue && categoryLookup.TryGetValue(payee.CategoryId.Value, out var category))
                {
                    payee.Category = category;
                }
            }

            // Apply filter
            var filtered = payees.AsEnumerable();

            // Filter inactive accounts unless ShowInactive is checked
            if (!ShowInactive)
            {
                filtered = filtered.Where(p => p.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(p =>
                    p.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    (p.Institution?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Notes?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // Apply type filter
            filtered = FilterType switch
            {
                "Accounts" => filtered.Where(p => p.IsAccount),
                "Non-Accounts" => filtered.Where(p => !p.IsAccount),
                "Payment Accounts" => filtered.Where(p => p.IsPaymentAccount),
                _ => filtered // "All"
            };

            Payees = new ObservableCollection<Payee>(filtered.OrderBy(p => p.Name));

            // Calculate account summary for accounts only
            var accounts = payees.Where(p => p.IsAccount).ToList();
            TotalAssets = accounts.Where(a => a.IsAsset).Sum(a => a.Balance);
            TotalLiabilities = accounts.Where(a => a.IsLiability).Sum(a => a.Balance);
            NetWorth = TotalAssets - TotalLiabilities;
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

    [RelayCommand]
    private async Task AddPayeeAsync()
    {
        await _navigationService.NavigateToAsync<PayeeEditViewModel>();
    }

    [RelayCommand]
    private async Task EditPayeeAsync(Payee? payee)
    {
        if (payee != null)
        {
            await _navigationService.NavigateToAsync<PayeeEditViewModel>(payee.Id);
        }
    }

    [RelayCommand]
    private async Task DeletePayeeAsync(Payee? payee)
    {
        if (payee == null) return;

        // Check for linked bills (as payee)
        var billsAsPayee = (await _billRepository.GetByPayeeAsync(payee.Id)).ToList();

        // Check for linked bills (as payment account)
        var billsAsPaymentAccount = payee.IsPaymentAccount
            ? (await _billRepository.GetByPaymentAccountAsync(payee.Id)).ToList()
            : new List<Bill>();

        var totalLinkedBills = billsAsPayee.Count + billsAsPaymentAccount.Count;

        if (totalLinkedBills > 0)
        {
            var message = $"Cannot delete '{payee.Name}' because it is linked to bills:\n\n";

            if (billsAsPayee.Count > 0)
                message += $"- {billsAsPayee.Count} bill(s) use this as the payee\n";

            if (billsAsPaymentAccount.Count > 0)
                message += $"- {billsAsPaymentAccount.Count} bill(s) use this as the payment account\n";

            message += "\nPlease reassign or delete these bills first.";

            MessageBox.Show(message, "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Confirm deletion
        var entityType = payee.IsAccount ? "account" : "payee";
        var result = MessageBox.Show(
            $"Are you sure you want to delete the {entityType} '{payee.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        await _payeeRepository.DeleteAsync(payee.Id);
        Payees.Remove(payee);

        // Recalculate summary
        if (payee.IsAccount)
        {
            if (payee.IsAsset)
                TotalAssets -= payee.Balance;
            else if (payee.IsLiability)
                TotalLiabilities -= payee.Balance;
            NetWorth = TotalAssets - TotalLiabilities;
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        FilterType = "All";
        ShowInactive = false;
        await LoadDataAsync();
    }

    // Filter options
    public IEnumerable<string> TypeFilterOptions => new[] { "All", "Accounts", "Non-Accounts", "Payment Accounts" };

    // Account type options
    public IEnumerable<AccountType> AccountTypeOptions => Enum.GetValues<AccountType>();
}
