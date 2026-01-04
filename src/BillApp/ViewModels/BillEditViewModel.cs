using System.Collections.ObjectModel;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

/// <summary>
/// Represents a payment method option in the dropdown (None, Cash, or an Account)
/// </summary>
public class PaymentMethodItem
{
    public Guid? AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCash { get; set; }
    public bool IsNone => AccountId == null && !IsCash;
}

public partial class BillEditViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly INavigationService _navigationService;

    private Guid? _editingBillId;
    private PaymentStatus _originalStatus; // Track original status to detect changes

    [ObservableProperty]
    private string _title = "Add Bill";

    [ObservableProperty]
    private string _payee = string.Empty;

    [ObservableProperty]
    private decimal _amountDue;

    [ObservableProperty]
    private decimal _amountPaid;

    [ObservableProperty]
    private decimal _balance;

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private PaymentStatus _status = PaymentStatus.Pending;

    [ObservableProperty]
    private DateTime? _paidDate;

    [ObservableProperty]
    private RecurrenceFrequency _frequency = RecurrenceFrequency.None;

    [ObservableProperty]
    private Guid? _categoryId;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _paymentUrl;

    [ObservableProperty]
    private string? _accountNumber;

    [ObservableProperty]
    private Guid? _accountId;

    [ObservableProperty]
    private string? _confirmation;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<PaymentMethodItem> _paymentMethods = new();

    [ObservableProperty]
    private PaymentMethodItem? _selectedPaymentMethod;

    [ObservableProperty]
    private string? _validationError;

    public BillEditViewModel(
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
        IsLoading = true;

        try
        {
            // Load categories
            var categories = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);

            // Load payment methods (None, Cash, then active payment accounts)
            var accounts = await _accountRepository.GetActiveAsync();
            var paymentAccounts = accounts.Where(a => a.IsPaymentAccount).ToList();
            var paymentMethodList = new List<PaymentMethodItem>
            {
                new PaymentMethodItem { Name = "(None)" },
                new PaymentMethodItem { Name = "Cash", IsCash = true }
            };
            paymentMethodList.AddRange(paymentAccounts.Select(a => new PaymentMethodItem
            {
                AccountId = a.Id,
                Name = a.Name
            }));
            PaymentMethods = new ObservableCollection<PaymentMethodItem>(paymentMethodList);
            SelectedPaymentMethod = PaymentMethods.First(); // Default to (None)

            // If parameter is a Guid, we're editing an existing bill
            if (parameter is Guid billId)
            {
                _editingBillId = billId;
                Title = "Edit Bill";

                var bill = await _billRepository.GetByIdAsync(billId);
                if (bill != null)
                {
                    Payee = bill.Payee;
                    AmountDue = bill.AmountDue;
                    AmountPaid = bill.AmountPaid;
                    Balance = bill.Balance;
                    DueDate = bill.DueDate;
                    Status = bill.Status;
                    PaidDate = bill.PaidDate;
                    Frequency = bill.Frequency;
                    CategoryId = bill.CategoryId;
                    AccountId = bill.AccountId;
                    Notes = bill.Notes;
                    PaymentUrl = bill.PaymentUrl;
                    AccountNumber = bill.AccountNumber;
                    Confirmation = bill.Confirmation;

                    // Set selected payment method based on bill data
                    if (bill.IsCashPayment)
                    {
                        SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.IsCash);
                    }
                    else if (bill.PaymentAccountId.HasValue)
                    {
                        SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.AccountId == bill.PaymentAccountId);
                    }
                    // Otherwise defaults to (None)

                    _originalStatus = bill.Status; // Remember original status
                }
            }
            else
            {
                _originalStatus = PaymentStatus.Pending;
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
        if (string.IsNullOrWhiteSpace(Payee))
        {
            ValidationError = "Payee name is required";
            return;
        }

        if (AmountDue < 0)
        {
            ValidationError = "Amount due cannot be negative";
            return;
        }

        ValidationError = null;

        try
        {
            // Check if status is being changed to Paid
            bool beingMarkedAsPaid = Status == PaymentStatus.Paid && _originalStatus != PaymentStatus.Paid;

            // Convert Guid.Empty to null for AccountId (the "None" option)
            var accountIdToSave = AccountId == Guid.Empty ? null : AccountId;

            // Determine payment method values from selection
            bool isCashPayment = SelectedPaymentMethod?.IsCash ?? false;
            Guid? paymentAccountId = SelectedPaymentMethod?.AccountId;

            if (_editingBillId.HasValue)
            {
                // Update existing bill
                var bill = await _billRepository.GetByIdAsync(_editingBillId.Value);
                if (bill != null)
                {
                    bill.Payee = Payee;
                    bill.AmountDue = AmountDue;
                    bill.AmountPaid = AmountPaid;
                    bill.Balance = Balance;
                    bill.DueDate = DueDate;
                    bill.Status = Status;
                    bill.PaidDate = PaidDate;
                    bill.Frequency = Frequency;
                    bill.CategoryId = CategoryId;
                    bill.AccountId = accountIdToSave;
                    bill.Notes = Notes;
                    bill.PaymentUrl = PaymentUrl;
                    bill.AccountNumber = AccountNumber;
                    bill.Confirmation = Confirmation;
                    bill.IsCashPayment = isCashPayment;
                    bill.PaymentAccountId = paymentAccountId;

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
                        var nextBill = bill.CreateNextRecurrence();
                        await _billRepository.InsertAsync(nextBill);
                    }
                }
            }
            else
            {
                // Create new bill
                var bill = new Bill
                {
                    Payee = Payee,
                    AmountDue = AmountDue,
                    AmountPaid = AmountPaid,
                    Balance = Balance,
                    DueDate = DueDate,
                    Status = Status,
                    PaidDate = PaidDate,
                    Frequency = Frequency,
                    CategoryId = CategoryId,
                    AccountId = accountIdToSave,
                    Notes = Notes,
                    PaymentUrl = PaymentUrl,
                    AccountNumber = AccountNumber,
                    Confirmation = Confirmation,
                    IsCashPayment = isCashPayment,
                    PaymentAccountId = paymentAccountId
                };

                // If new bill is created as paid with a recurring frequency
                if (Status == PaymentStatus.Paid)
                {
                    bill.PaidDate ??= DateTime.Now;
                    if (bill.AmountPaid == 0)
                    {
                        bill.AmountPaid = bill.AmountDue;
                    }
                }

                await _billRepository.InsertAsync(bill);

                // Create next recurring bill if created as paid and is recurring
                if (Status == PaymentStatus.Paid && bill.IsRecurring)
                {
                    var nextBill = bill.CreateNextRecurrence();
                    await _billRepository.InsertAsync(nextBill);
                }
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

    // Options for dropdowns
    public IEnumerable<PaymentStatus> StatusOptions => Enum.GetValues<PaymentStatus>();
    public IEnumerable<RecurrenceFrequency> FrequencyOptions => Enum.GetValues<RecurrenceFrequency>();
}
