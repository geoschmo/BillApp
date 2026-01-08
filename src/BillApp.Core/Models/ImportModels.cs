using BillApp.Core.Enums;

namespace BillApp.Core.Models;

/// <summary>
/// Represents a single row from the CSV import file.
/// </summary>
public class ImportRow
{
    public int RowNumber { get; set; }

    // Payee fields (AccountName is used as payee name)
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? InterestRate { get; set; }
    public string? AccountType { get; set; }
    public string? IsActive { get; set; }
    public string? IsPaymentAccount { get; set; }

    // Bill fields
    public string? Frequency { get; set; }
    public string? AmountDue { get; set; }
    public string? AmountPaid { get; set; }
    public string? Balance { get; set; }
    public string? Confirmation { get; set; }
    public string? DueDate { get; set; }
    public string? PaidDate { get; set; }
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// Represents a payee to be imported. Can optionally be an account.
/// </summary>
public class ImportPayee
{
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public Guid? CategoryId { get; set; }

    // Account fields (optional)
    public bool IsAccount { get; set; }
    public AccountType? AccountType { get; set; }
    public decimal? InterestRate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPaymentAccount { get; set; }

    public string DedupeKey => Name.ToLowerInvariant();
}

/// <summary>
/// Represents a bill to be imported.
/// </summary>
public class ImportBill
{
    public string PayeeName { get; set; } = string.Empty;
    public RecurrenceFrequency Frequency { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public string? Confirmation { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PaymentMethodName { get; set; }
}

/// <summary>
/// Validation warning or error from import parsing.
/// </summary>
public class ImportValidationItem
{
    public int RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ImportValidationSeverity Severity { get; set; }
}

public enum ImportValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Import mode - whether to add to existing data or replace all.
/// </summary>
public enum ImportMode
{
    AddToExisting,
    ReplaceAll
}

/// <summary>
/// Result of parsing and validating an import file.
/// </summary>
public class ImportValidationResult
{
    public bool IsValid => !ValidationItems.Any(v => v.Severity == ImportValidationSeverity.Error);
    public int TotalRows { get; set; }
    public List<ImportPayee> Payees { get; set; } = new();
    public List<ImportBill> Bills { get; set; } = new();
    public List<string> PaymentAccountsToCreate { get; set; } = new();
    public List<ImportValidationItem> ValidationItems { get; set; } = new();

    public int ErrorCount => ValidationItems.Count(v => v.Severity == ImportValidationSeverity.Error);
    public int WarningCount => ValidationItems.Count(v => v.Severity == ImportValidationSeverity.Warning);
}

/// <summary>
/// Result of executing an import.
/// </summary>
public class ImportExecutionResult
{
    public bool Success { get; set; }
    public int PayeesCreated { get; set; }
    public int BillsCreated { get; set; }
    public int PaymentAccountsCreated { get; set; }
    public string? ErrorMessage { get; set; }
}
