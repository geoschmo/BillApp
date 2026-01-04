using BillApp.Core.Enums;

namespace BillApp.Core.Models;

/// <summary>
/// Represents a bill or recurring payment.
/// </summary>
public class Bill : EntityBase
{
    /// <summary>
    /// Name of the payee (e.g., "Electric Company", "Netflix")
    /// </summary>
    public string Payee { get; set; } = string.Empty;

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
    /// Category ID for organization
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Optional notes about this bill
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// URL for online payment (optional)
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Account number or reference (optional, not encrypted in Phase 2)
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// If this is a recurring bill, the ID of the previous bill in the chain
    /// </summary>
    public Guid? PreviousBillId { get; set; }

    /// <summary>
    /// Date the bill was paid (if paid)
    /// </summary>
    public DateTime? PaidDate { get; set; }

    /// <summary>
    /// Account ID for the payment account (optional)
    /// </summary>
    public Guid? AccountId { get; set; }

    // Navigation properties - not stored in DB, set by ViewModel
    public Category? Category { get; set; }
    public Account? Account { get; set; }

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
    /// New balance is reduced by the amount paid on this bill.
    /// </summary>
    public Bill CreateNextRecurrence()
    {
        return new Bill
        {
            Payee = Payee,
            AmountDue = 0, // Start fresh, user will set the new amount due
            AmountPaid = 0,
            Balance = Balance - AmountPaid, // Reduce balance by payment
            DueDate = CalculateNextDueDate(),
            Status = PaymentStatus.Pending,
            Frequency = Frequency,
            CategoryId = CategoryId,
            AccountId = AccountId,
            Notes = Notes,
            PaymentUrl = PaymentUrl,
            AccountNumber = AccountNumber,
            PreviousBillId = Id
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
