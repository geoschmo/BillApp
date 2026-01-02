using System.Collections.ObjectModel;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class AccountListViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<Account> _accounts = new();

    [ObservableProperty]
    private Account? _selectedAccount;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private AccountType? _filterType;

    [ObservableProperty]
    private decimal _totalAssets;

    [ObservableProperty]
    private decimal _totalLiabilities;

    [ObservableProperty]
    private decimal _netWorth;

    public AccountListViewModel(
        IAccountRepository accountRepository,
        INavigationService navigationService)
    {
        _accountRepository = accountRepository;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var accounts = (await _accountRepository.GetAllAsync()).ToList();

            // Apply filters
            var filtered = accounts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(a =>
                    a.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Institution?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (FilterType.HasValue)
            {
                filtered = filtered.Where(a => a.AccountType == FilterType.Value);
            }

            // Sort by name
            Accounts = new ObservableCollection<Account>(filtered.OrderBy(a => a.Name));

            // Calculate totals
            CalculateTotals(accounts);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load accounts: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CalculateTotals(List<Account> accounts)
    {
        TotalAssets = accounts
            .Where(a => a.IsActive && a.IsAsset)
            .Sum(a => a.Balance);

        TotalLiabilities = accounts
            .Where(a => a.IsActive && a.IsLiability)
            .Sum(a => a.Balance);

        NetWorth = TotalAssets - TotalLiabilities;
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        await _navigationService.NavigateToAsync<AccountEditViewModel>();
    }

    [RelayCommand]
    private async Task EditAccountAsync(Account? account)
    {
        if (account != null)
        {
            await _navigationService.NavigateToAsync<AccountEditViewModel>(account.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(Account? account)
    {
        if (account == null) return;

        // TODO: Add confirmation dialog
        await _accountRepository.DeleteAsync(account.Id);
        Accounts.Remove(account);
        CalculateTotals(Accounts.ToList());
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        FilterType = null;
        await LoadAccountsAsync();
    }

    // Account type options for the filter dropdown
    public IEnumerable<AccountType> AccountTypeOptions => Enum.GetValues<AccountType>();
}
