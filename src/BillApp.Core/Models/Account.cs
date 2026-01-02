using BillApp.Core.Enums;

namespace BillApp.Core.Models;

/// <summary>
/// Represents a financial account (bank, credit card, loan, investment, etc.)
/// </summary>
public class Account : EntityBase
{
    /// <summary>
    /// Display name for the account (e.g., "Chase Checking", "Visa Credit Card")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of account
    /// </summary>
    public AccountType AccountType { get; set; } = AccountType.Checking;

    /// <summary>
    /// Account number (stored encrypted, can be full or last 4 digits)
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Financial institution name (e.g., "Chase", "Bank of America")
    /// </summary>
    public string? Institution { get; set; }

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
    /// URL for online banking login
    /// </summary>
    public string? LoginUrl { get; set; }

    /// <summary>
    /// Username for online banking (stored encrypted)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for online banking (stored encrypted)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Additional notes about the account
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether the account is active or closed
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation property - bills linked to this account (not stored in DB)
    public List<Bill> Bills { get; set; } = new();

    /// <summary>
    /// Available credit for credit cards (CreditLimit - Balance)
    /// </summary>
    public decimal? AvailableCredit =>
        AccountType == AccountType.CreditCard && CreditLimit.HasValue
            ? CreditLimit.Value - Balance
            : null;

    /// <summary>
    /// Whether this account is an asset (positive net worth) or liability (negative)
    /// </summary>
    public bool IsAsset => AccountType is AccountType.Checking
        or AccountType.Savings
        or AccountType.Investment;

    /// <summary>
    /// Whether this account is a liability (debt)
    /// </summary>
    public bool IsLiability => AccountType is AccountType.CreditCard
        or AccountType.Loan;
}
