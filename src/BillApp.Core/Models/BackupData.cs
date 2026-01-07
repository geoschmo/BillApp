namespace BillApp.Core.Models;

/// <summary>
/// Container for all data to be backed up/restored.
/// </summary>
public class BackupData
{
    public List<BillBackupDto> Bills { get; set; } = new();
    public List<AccountBackupDto> Accounts { get; set; } = new();
    public List<CategoryBackupDto> Categories { get; set; } = new();
    public List<PayeeBackupDto> Payees { get; set; } = new();
}

/// <summary>
/// Bill data for backup. Matches Bill model but serializable.
/// </summary>
public class BillBackupDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid PayeeId { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public DateTime DueDate { get; set; }
    public int Status { get; set; }
    public DateTime? PaidDate { get; set; }
    public int Frequency { get; set; }
    public Guid? AccountId { get; set; }
    public string? Notes { get; set; }
    public Guid? PreviousBillId { get; set; }
    public string? Confirmation { get; set; }
    public Guid? PaymentAccountId { get; set; }
    public bool IsCashPayment { get; set; }
}

/// <summary>
/// Account data for backup. Encrypted fields are re-encrypted with backup password.
/// </summary>
public class AccountBackupDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AccountType { get; set; }
    public string? AccountNumber { get; set; } // Re-encrypted with backup password
    public string? Institution { get; set; }
    public decimal Balance { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? InterestRate { get; set; }
    public string? LoginUrl { get; set; }
    public string? Username { get; set; } // Re-encrypted with backup password
    public string? Password { get; set; } // Re-encrypted with backup password
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaymentAccount { get; set; }
}

/// <summary>
/// Category data for backup.
/// </summary>
public class CategoryBackupDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#607D8B";
    public bool IsDefault { get; set; }
}

/// <summary>
/// Payee data for backup.
/// </summary>
public class PayeeBackupDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? PaymentUrl { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
}
