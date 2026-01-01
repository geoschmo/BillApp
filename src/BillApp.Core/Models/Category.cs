namespace BillApp.Core.Models;

/// <summary>
/// Represents a category for organizing bills and transactions.
/// Examples: Utilities, Insurance, Subscriptions, etc.
/// </summary>
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#607D8B"; // Default gray-blue
    public bool IsDefault { get; set; } // System-provided categories
}
