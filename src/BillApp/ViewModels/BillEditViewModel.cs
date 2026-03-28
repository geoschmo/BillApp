using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Forms;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

/// <summary>
/// Represents a payment method option in the dropdown (None, Cash, or a Payment Account)
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
    private readonly IPayeeRepository _payeeRepository;
    private readonly INavigationService _navigationService;

    private Guid? _editingBillId;
    private PaymentStatus _originalStatus; // Track original status to detect changes

    [ObservableProperty]
    private string _title = "Add Bill";

    [ObservableProperty]
    private ObservableCollection<Payee> _payees = new();

    [ObservableProperty]
    private Payee? _selectedPayee;

    [ObservableProperty]
    private string _newPayeeName = string.Empty;

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
    private string? _notes;

    [ObservableProperty]
    private string? _confirmation;

    [ObservableProperty]
    private string? _colorCode;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<PaymentMethodItem> _paymentMethods = new();

    [ObservableProperty]
    private PaymentMethodItem? _selectedPaymentMethod;

    [ObservableProperty]
    private string? _validationError;

    // Payee-specific fields (editable when a payee is selected or being created)
    [ObservableProperty]
    private Guid? _payeeCategoryId;

    [ObservableProperty]
    private string? _payeePaymentUrl;

    [ObservableProperty]
    private string? _payeeAccountNumber;

    [ObservableProperty]
    private string? _payeeNotes;

    [ObservableProperty]
    private bool _payeeIsAutopay;

    public BillEditViewModel(
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
        IsLoading = true;

        try
        {
            // Load categories
            var categories = await _categoryRepository.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);

            // Load payees
            var payees = (await _payeeRepository.GetAllAsync())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Payees = new ObservableCollection<Payee>(payees);

            // Load payment methods (None, Cash, then active payment accounts)
            var paymentAccounts = await _payeeRepository.GetPaymentAccountsAsync();
            var paymentMethodList = new List<PaymentMethodItem>
            {
                new PaymentMethodItem { Name = "(None)" },
                new PaymentMethodItem { Name = "Cash", IsCash = true }
            };
            paymentMethodList.AddRange(paymentAccounts
                .Where(p => p.IsActive)
                .Select(p => new PaymentMethodItem
                {
                    AccountId = p.Id,
                    Name = p.Name
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
                    // Set payee
                    SelectedPayee = Payees.FirstOrDefault(p => p.Id == bill.PayeeId);
                    if (SelectedPayee != null)
                    {
                        PayeeCategoryId = SelectedPayee.CategoryId;
                        PayeePaymentUrl = SelectedPayee.PaymentUrl;
                        PayeeAccountNumber = SelectedPayee.AccountNumber;
                        PayeeNotes = SelectedPayee.Notes;
                        PayeeIsAutopay = SelectedPayee.IsAutopay;
                    }

                    AmountDue = bill.AmountDue;
                    AmountPaid = bill.AmountPaid;
                    Balance = bill.Balance;
                    DueDate = bill.DueDate;
                    Status = bill.Status;
                    PaidDate = bill.PaidDate;
                    Frequency = bill.Frequency;
                    Notes = bill.Notes;
                    Confirmation = bill.Confirmation;
                    ColorCode = bill.ColorCode;

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

    partial void OnSelectedPayeeChanged(Payee? value)
    {
        if (value != null)
        {
            PayeeCategoryId = value.CategoryId;
            PayeePaymentUrl = value.PaymentUrl;
            PayeeAccountNumber = value.AccountNumber;
            PayeeNotes = value.Notes;
            PayeeIsAutopay = value.IsAutopay;
            NewPayeeName = string.Empty; // Clear new payee name when selecting existing
        }
        OnPropertyChanged(nameof(IsPayeeFinancialAccount));
    }

    /// <summary>
    /// Returns true if the selected payee is a financial account (has balance tracking).
    /// Used to show/hide balance field in the UI.
    /// </summary>
    public bool IsPayeeFinancialAccount => SelectedPayee?.IsAccount == true;

    [RelayCommand]
    private void PickColor()
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (TryGetDrawingColor(ColorCode, out var existing))
        {
            dialog.Color = existing;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var color = dialog.Color;
            ColorCode = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }

    [RelayCommand]
    private void ClearColor()
    {
        ColorCode = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Validate - must have either selected payee or new payee name
        if (SelectedPayee == null && string.IsNullOrWhiteSpace(NewPayeeName))
        {
            ValidationError = "Please select a payee or enter a new payee name";
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
            // Get or create payee
            Payee payee;
            if (SelectedPayee != null)
            {
                payee = SelectedPayee;
                // Update payee fields if changed
                bool payeeChanged = payee.CategoryId != PayeeCategoryId ||
                                   payee.PaymentUrl != PayeePaymentUrl ||
                                   payee.AccountNumber != PayeeAccountNumber ||
                                   payee.Notes != PayeeNotes ||
                                   payee.IsAutopay != PayeeIsAutopay;
                if (payeeChanged)
                {
                    payee.CategoryId = PayeeCategoryId;
                    payee.PaymentUrl = PayeePaymentUrl;
                    payee.AccountNumber = PayeeAccountNumber;
                    payee.Notes = PayeeNotes;
                    payee.IsAutopay = PayeeIsAutopay;
                    await _payeeRepository.UpdateAsync(payee);
                }
            }
            else
            {
                // Create new payee
                payee = new Payee
                {
                    Name = NewPayeeName.Trim(),
                    CategoryId = PayeeCategoryId,
                    PaymentUrl = PayeePaymentUrl,
                    AccountNumber = PayeeAccountNumber,
                    Notes = PayeeNotes,
                    IsAutopay = PayeeIsAutopay
                };
                await _payeeRepository.InsertAsync(payee);
            }

            // Check if status is being changed to Paid
            bool beingMarkedAsPaid = Status == PaymentStatus.Paid && _originalStatus != PaymentStatus.Paid;

            // Determine payment method values from selection
            bool isCashPayment = SelectedPaymentMethod?.IsCash ?? false;
            Guid? paymentAccountId = SelectedPaymentMethod?.AccountId;
            var normalizedColorCode = string.IsNullOrWhiteSpace(ColorCode) ? null : ColorCode.Trim();

            if (_editingBillId.HasValue)
            {
                // Update existing bill
                var bill = await _billRepository.GetByIdAsync(_editingBillId.Value);
                if (bill != null)
                {
                    bill.PayeeId = payee.Id;
                    bill.AmountDue = AmountDue;
                    bill.AmountPaid = AmountPaid;
                    bill.Balance = Balance;
                    bill.DueDate = DueDate;
                    bill.Status = Status;
                    bill.PaidDate = PaidDate;
                    bill.Frequency = Frequency;
                    bill.Notes = Notes;
                    bill.Confirmation = Confirmation;
                    bill.ColorCode = normalizedColorCode;
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
                        var carryBalance = payee.IsAccount;
                        var nextBill = bill.CreateNextRecurrence(carryBalance);
                        await _billRepository.InsertAsync(nextBill);

                        // Update the payee account balance to match the new bill's balance
                        if (carryBalance)
                        {
                            payee.Balance = nextBill.Balance;
                            await _payeeRepository.UpdateAsync(payee);
                        }
                    }
                }
            }
            else
            {
                // Create new bill
                var bill = new Bill
                {
                    PayeeId = payee.Id,
                    AmountDue = AmountDue,
                    AmountPaid = AmountPaid,
                    Balance = Balance,
                    DueDate = DueDate,
                    Status = Status,
                    PaidDate = PaidDate,
                    Frequency = Frequency,
                    Notes = Notes,
                    Confirmation = Confirmation,
                    ColorCode = normalizedColorCode,
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
                    var carryBalance = payee.IsAccount;
                    var nextBill = bill.CreateNextRecurrence(carryBalance);
                    await _billRepository.InsertAsync(nextBill);

                    // Update the payee account balance to match the new bill's balance
                    if (carryBalance)
                    {
                        payee.Balance = nextBill.Balance;
                        await _payeeRepository.UpdateAsync(payee);
                    }
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

    private static bool TryGetDrawingColor(string? hexCode, out System.Drawing.Color color)
    {
        color = System.Drawing.Color.Empty;

        if (string.IsNullOrWhiteSpace(hexCode))
        {
            return false;
        }

        var trimmed = hexCode.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 && trimmed.Length != 8)
        {
            return false;
        }

        if (!int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (trimmed.Length == 6)
        {
            var r = (value >> 16) & 0xFF;
            var g = (value >> 8) & 0xFF;
            var b = value & 0xFF;
            color = System.Drawing.Color.FromArgb(r, g, b);
            return true;
        }

        var a = (value >> 24) & 0xFF;
        var red = (value >> 16) & 0xFF;
        var green = (value >> 8) & 0xFF;
        var blue = value & 0xFF;
        color = System.Drawing.Color.FromArgb(a, red, green, blue);
        return true;
    }
}
