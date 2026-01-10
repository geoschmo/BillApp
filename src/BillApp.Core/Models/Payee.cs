using BillApp.Core.Enums;

namespace BillApp.Core.Models;

/// <summary>
/// Represents a payee (vendor/company) for bills.
/// Can optionally be a financial account (credit card, bank account, loan).
/// Examples: "Electric Company", "Netflix", "Chase Visa"
/// </summary>
public class Payee : EntityBase
{
    // ===== Core Payee Fields =====

    /// <summary>
    /// Name of the payee
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category ID for organization
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// URL for online payment/login (optional)
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Account number or reference with this payee (stored encrypted)
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Notes about this payee
    /// </summary>
    public string? Notes { get; set; }

    // ===== Account-Specific Fields (optional) =====

    /// <summary>
    /// Whether this payee is also a financial account
    /// </summary>
    public bool IsAccount { get; set; }

    /// <summary>
    /// Type of account (if IsAccount is true)
    /// </summary>
    public AccountType? AccountType { get; set; }

    /// <summary>
    /// Current balance (positive for assets, positive for debt on credit/loans)
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Credit limit for credit cards
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Interest rate (APR) for loans and credit cards
    /// </summary>
    public decimal? InterestRate { get; set; }

    /// <summary>
    /// Financial institution name (e.g., "Chase", "Bank of America")
    /// </summary>
    public string? Institution { get; set; }

    /// <summary>
    /// Username for online banking (stored encrypted)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for online banking (stored encrypted)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether the account is active or closed
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this payee/account can be used as a payment method for bills
    /// </summary>
    public bool IsPaymentAccount { get; set; }

    // ===== Navigation Properties =====

    /// <summary>
    /// Category navigation property - not stored in DB, set by ViewModel
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Last date a bill was paid for this payee - not stored in DB, set by ViewModel
    /// </summary>
    public DateTime? LastPaidDate { get; set; }

    // ===== Computed Properties =====

    /// <summary>
    /// Available credit for credit cards (CreditLimit - Balance)
    /// </summary>
    public decimal? AvailableCredit =>
        IsAccount && AccountType == Enums.AccountType.CreditCard && CreditLimit.HasValue
            ? CreditLimit.Value - Balance
            : null;

    /// <summary>
    /// Whether this account is an asset (positive net worth)
    /// </summary>
    public bool IsAsset => IsAccount && AccountType is Enums.AccountType.Checking
        or Enums.AccountType.Savings
        or Enums.AccountType.Investment;

    /// <summary>
    /// Whether this account is a liability (debt)
    /// </summary>
    public bool IsLiability => IsAccount && AccountType is Enums.AccountType.CreditCard
        or Enums.AccountType.Loan;
}
