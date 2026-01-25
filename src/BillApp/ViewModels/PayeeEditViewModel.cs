using System.Collections.ObjectModel;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class PayeeEditViewModel : ViewModelBase
{
    private readonly IPayeeRepository _payeeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly INavigationService _navigationService;

    private Guid? _editingPayeeId;

    [ObservableProperty]
    private string _title = "Add Payee";

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    // Core payee fields
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Guid? _categoryId;

    [ObservableProperty]
    private string? _paymentUrl;

    [ObservableProperty]
    private string? _accountNumber;

    [ObservableProperty]
    private string? _notes;

    // Account fields
    [ObservableProperty]
    private bool _isAccount;

    [ObservableProperty]
    private AccountType? _accountType;

    [ObservableProperty]
    private decimal _balance;

    [ObservableProperty]
    private decimal? _creditLimit;

    [ObservableProperty]
    private decimal? _interestRate;

    [ObservableProperty]
    private string? _institution;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isPaymentAccount;

    [ObservableProperty]
    private bool _isAutopay;

    [ObservableProperty]
    private string? _validationError;

    public PayeeEditViewModel(
        IPayeeRepository payeeRepository,
        ICategoryRepository categoryRepository,
        IEncryptionService encryptionService,
        INavigationService navigationService)
    {
        _payeeRepository = payeeRepository;
        _categoryRepository = categoryRepository;
        _encryptionService = encryptionService;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        IsLoading = true;

        try
        {
            // Load categories
            var categories = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);

            // If parameter is a Guid, we're editing an existing payee
            if (parameter is Guid payeeId)
            {
                _editingPayeeId = payeeId;
                Title = "Edit Payee";

                var payee = await _payeeRepository.GetByIdAsync(payeeId);
                if (payee != null)
                {
                    Name = payee.Name;
                    CategoryId = payee.CategoryId;
                    PaymentUrl = payee.PaymentUrl;
                    AccountNumber = payee.AccountNumber;
                    Notes = payee.Notes;

                    IsAccount = payee.IsAccount;
                    AccountType = payee.AccountType;
                    Balance = payee.Balance;
                    CreditLimit = payee.CreditLimit;
                    InterestRate = payee.InterestRate;
                    Institution = payee.Institution;

                    // Decrypt credentials for editing
                    Username = DecryptIfNotEmpty(payee.Username);
                    Password = DecryptIfNotEmpty(payee.Password);

                    IsActive = payee.IsActive;
                    IsPaymentAccount = payee.IsPaymentAccount;
                    IsAutopay = payee.IsAutopay;
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnIsAccountChanged(bool value)
    {
        // Set sensible defaults when toggling IsAccount
        if (value)
        {
            AccountType ??= Core.Enums.AccountType.Checking;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Payee name is required";
            return;
        }

        ValidationError = null;

        try
        {
            if (_editingPayeeId.HasValue)
            {
                // Update existing payee
                var payee = await _payeeRepository.GetByIdAsync(_editingPayeeId.Value);
                if (payee != null)
                {
                    payee.Name = Name.Trim();
                    payee.CategoryId = CategoryId;
                    payee.PaymentUrl = PaymentUrl;
                    payee.AccountNumber = AccountNumber;
                    payee.Notes = Notes;

                    payee.IsAccount = IsAccount;
                    payee.AccountType = IsAccount ? AccountType : null;
                    payee.Balance = IsAccount ? Balance : 0;
                    payee.CreditLimit = IsAccount ? CreditLimit : null;
                    payee.InterestRate = IsAccount ? InterestRate : null;
                    payee.Institution = IsAccount ? Institution : null;

                    // Encrypt credentials
                    payee.Username = EncryptIfNotEmpty(Username);
                    payee.Password = EncryptIfNotEmpty(Password);

                    payee.IsActive = IsActive;
                    payee.IsPaymentAccount = IsPaymentAccount;
                    payee.IsAutopay = IsAutopay;

                    await _payeeRepository.UpdateAsync(payee);
                }
            }
            else
            {
                // Create new payee
                var payee = new Payee
                {
                    Name = Name.Trim(),
                    CategoryId = CategoryId,
                    PaymentUrl = PaymentUrl,
                    AccountNumber = AccountNumber,
                    Notes = Notes,
                    IsAccount = IsAccount,
                    AccountType = IsAccount ? AccountType : null,
                    Balance = IsAccount ? Balance : 0,
                    CreditLimit = IsAccount ? CreditLimit : null,
                    InterestRate = IsAccount ? InterestRate : null,
                    Institution = IsAccount ? Institution : null,
                    Username = EncryptIfNotEmpty(Username),
                    Password = EncryptIfNotEmpty(Password),
                    IsActive = IsActive,
                    IsPaymentAccount = IsPaymentAccount,
                    IsAutopay = IsAutopay
                };

                await _payeeRepository.InsertAsync(payee);
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
            return null;
        return _encryptionService.Encrypt(value);
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
            return value; // Return as-is if decryption fails
        }
    }

    // Options for dropdowns
    public IEnumerable<AccountType> AccountTypeOptions => Enum.GetValues<AccountType>();
}
