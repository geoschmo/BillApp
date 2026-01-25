using BillApp.Core.Enums;

namespace BillApp.Core.Models;

/// <summary>
/// Represents a bill or recurring payment.
/// </summary>
public class Bill : EntityBase
{
    /// <summary>
    /// Reference to the payee for this bill
    /// </summary>
    public Guid PayeeId { get; set; }

    /// <summary>
    /// Amount due for this bill
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Amount actually paid (may differ from AmountDue)
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Remaining balance (for bills with ongoing balances like credit cards)
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Date the bill is due
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Current payment status
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// How often this bill recurs (None = one-time bill)
    /// </summary>
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.None;

    /// <summary>
    /// Optional notes specific to this bill instance
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional color code used for UI highlighting (e.g. #RRGGBB)
    /// </summary>
    public string? ColorCode { get; set; }

    /// <summary>
    /// If this is a recurring bill, the ID of the previous bill in the chain
    /// </summary>
    public Guid? PreviousBillId { get; set; }

    /// <summary>
    /// Date the bill was paid (if paid)
    /// </summary>
    public DateTime? PaidDate { get; set; }

    /// <summary>
    /// Payment confirmation number or reference
    /// </summary>
    public string? Confirmation { get; set; }

    /// <summary>
    /// Payee used to pay this bill (must have IsPaymentAccount=true)
    /// </summary>
    public Guid? PaymentAccountId { get; set; }

    /// <summary>
    /// Whether this bill was paid with cash
    /// </summary>
    public bool IsCashPayment { get; set; }

    // Navigation properties - not stored in DB, set by ViewModel
    public Payee? Payee { get; set; }
    public Payee? PaymentAccount { get; set; }

    /// <summary>
    /// Whether this bill is overdue (computed, not stored)
    /// </summary>
    public bool IsOverdue => Status == PaymentStatus.Pending && DueDate.Date < DateTime.Today;

    /// <summary>
    /// Days until due - negative if overdue (computed, not stored)
    /// </summary>
    public int DaysUntilDue => (DueDate.Date - DateTime.Today).Days;

    /// <summary>
    /// Whether this is a recurring bill
    /// </summary>
    public bool IsRecurring => Frequency != RecurrenceFrequency.None;

    /// <summary>
    /// Creates a copy of this bill for the next recurring period.
    /// </summary>
    /// <param name="carryBalance">If true, reduces balance by amount paid (for financial accounts). If false, AmountDue is set to previous AmountPaid and payment method is copied.</param>
    public Bill CreateNextRecurrence(bool carryBalance = false)
    {
        return new Bill
        {
            PayeeId = PayeeId,
            // For financial accounts, start fresh. For regular payees, assume same amount as last payment.
            AmountDue = carryBalance ? 0 : AmountPaid,
            AmountPaid = 0,
            Balance = carryBalance ? Math.Max(0, Balance - AmountPaid) : 0,
            DueDate = CalculateNextDueDate(),
            Status = PaymentStatus.Pending,
            Frequency = Frequency,
            Notes = Notes,
            ColorCode = ColorCode,
            PreviousBillId = Id,
            // For regular payees, copy the payment method from the previous bill
            IsCashPayment = carryBalance ? false : IsCashPayment,
            PaymentAccountId = carryBalance ? null : PaymentAccountId
        };
    }

    /// <summary>
    /// Calculates the next due date based on the recurrence frequency.
    /// </summary>
    private DateTime CalculateNextDueDate()
    {
        return Frequency switch
        {
            RecurrenceFrequency.Weekly => DueDate.AddDays(7),
            RecurrenceFrequency.BiWeekly => DueDate.AddDays(14),
            RecurrenceFrequency.Monthly => DueDate.AddMonths(1),
            RecurrenceFrequency.Quarterly => DueDate.AddMonths(3),
            RecurrenceFrequency.SemiAnnually => DueDate.AddMonths(6),
            RecurrenceFrequency.Annually => DueDate.AddYears(1),
            _ => DueDate
        };
    }
}
