using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class AccountEditViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly INavigationService _navigationService;

    private Guid? _editingAccountId;

    [ObservableProperty]
    private string _title = "Add Account";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private AccountType _accountType = AccountType.Checking;

    [ObservableProperty]
    private string? _accountNumber;

    [ObservableProperty]
    private string? _institution;

    [ObservableProperty]
    private decimal _balance;

    [ObservableProperty]
    private decimal? _creditLimit;

    [ObservableProperty]
    private decimal? _interestRate;

    [ObservableProperty]
    private string? _loginUrl;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isPaymentAccount;

    [ObservableProperty]
    private string? _validationError;

    public AccountEditViewModel(
        IAccountRepository accountRepository,
        IEncryptionService encryptionService,
        INavigationService navigationService)
    {
        _accountRepository = accountRepository;
        _encryptionService = encryptionService;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        IsLoading = true;

        try
        {
            // If parameter is a Guid, we're editing an existing account
            if (parameter is Guid accountId)
            {
                _editingAccountId = accountId;
                Title = "Edit Account";

                var account = await _accountRepository.GetByIdAsync(accountId);
                if (account != null)
                {
                    Name = account.Name;
                    AccountType = account.AccountType;
                    Institution = account.Institution;
                    Balance = account.Balance;
                    CreditLimit = account.CreditLimit;
                    InterestRate = account.InterestRate;
                    LoginUrl = account.LoginUrl;
                    Notes = account.Notes;
                    IsActive = account.IsActive;
                    IsPaymentAccount = account.IsPaymentAccount;

                    // Decrypt sensitive fields
                    AccountNumber = DecryptIfNotEmpty(account.AccountNumber);
                    Username = DecryptIfNotEmpty(account.Username);
                    Password = DecryptIfNotEmpty(account.Password);
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Account name is required";
            return;
        }

        ValidationError = null;

        try
        {
            if (_editingAccountId.HasValue)
            {
                // Update existing account
                var account = await _accountRepository.GetByIdAsync(_editingAccountId.Value);
                if (account != null)
                {
                    account.Name = Name;
                    account.AccountType = AccountType;
                    account.Institution = Institution;
                    account.Balance = Balance;
                    account.CreditLimit = CreditLimit;
                    account.InterestRate = InterestRate;
                    account.LoginUrl = LoginUrl;
                    account.Notes = Notes;
                    account.IsActive = IsActive;
                    account.IsPaymentAccount = IsPaymentAccount;

                    // Encrypt sensitive fields
                    account.AccountNumber = EncryptIfNotEmpty(AccountNumber);
                    account.Username = EncryptIfNotEmpty(Username);
                    account.Password = EncryptIfNotEmpty(Password);

                    await _accountRepository.UpdateAsync(account);
                }
            }
            else
            {
                // Create new account
                var account = new Account
                {
                    Name = Name,
                    AccountType = AccountType,
                    Institution = Institution,
                    Balance = Balance,
                    CreditLimit = CreditLimit,
                    InterestRate = InterestRate,
                    LoginUrl = LoginUrl,
                    Notes = Notes,
                    IsActive = IsActive,
                    IsPaymentAccount = IsPaymentAccount,

                    // Encrypt sensitive fields
                    AccountNumber = EncryptIfNotEmpty(AccountNumber),
                    Username = EncryptIfNotEmpty(Username),
                    Password = EncryptIfNotEmpty(Password)
                };

                await _accountRepository.InsertAsync(account);
            }

            // Navigate back to list
            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            ValidationError = $"Failed to save: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _navigationService.GoBackAsync();
    }

    private string? EncryptIfNotEmpty(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return _encryptionService.Encrypt(value);
    }

    private string? DecryptIfNotEmpty(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        try
        {
            return _encryptionService.Decrypt(value);
        }
        catch
        {
            // If decryption fails (e.g., data wasn't encrypted), return as-is
            return value;
        }
    }

    // Options for dropdown
    public IEnumerable<AccountType> AccountTypeOptions => Enum.GetValues<AccountType>();
}
