namespace BillApp.Core.Models;

/// <summary>
/// Represents a single holding inside an imported investment snapshot.
/// </summary>
public class InvestmentHolding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Optional account name provided by the import (e.g., "George Rollover IRA").
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    public string? Symbol { get; set; }

    public string? Description { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal MarketValue { get; set; }

    public string? AssetClass { get; set; }

    public decimal? PercentOfAccount { get; set; }

    public string? SourceLine { get; set; }
}

/// <summary>
/// Represents a single import snapshot saved to the database.
/// </summary>
public class InvestmentSnapshot : EntityBase
{
    /// <summary>
    /// When the user imported this data (local time).
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional source metadata ("Fidelity CSV", "401k text import", etc.).
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// Optional notes describing the import.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Individual holdings captured during the import.
    /// </summary>
    public List<InvestmentHolding> Holdings { get; set; } = new();

    /// <summary>
    /// Computed total value of this snapshot.
    /// </summary>
    public decimal TotalValue => Holdings.Sum(h => h.MarketValue);
}

/// <summary>
/// Preview of what the parser extracted before persisting.
/// </summary>
public class InvestmentImportPreview
{
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string? SourceName { get; set; }
    public string? AccountName { get; set; }
    public List<InvestmentHolding> Holdings { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public decimal TotalValue => Holdings.Sum(h => h.MarketValue);
}
