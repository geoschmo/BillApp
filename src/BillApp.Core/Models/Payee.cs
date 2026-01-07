namespace BillApp.Core.Models;

/// <summary>
/// Represents a payee (vendor/company) for bills.
/// Examples: "Electric Company", "Netflix", "Landlord"
/// </summary>
public class Payee : EntityBase
{
    /// <summary>
    /// Name of the payee
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category ID for organization
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// URL for online payment (optional)
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Account number or reference with this payee (optional)
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Notes about this payee
    /// </summary>
    public string? Notes { get; set; }

    // Navigation property - not stored in DB, set by ViewModel
    public Category? Category { get; set; }
}
