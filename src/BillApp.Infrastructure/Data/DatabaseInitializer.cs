using BillApp.Core.Models;

namespace BillApp.Infrastructure.Data;

/// <summary>
/// Initializes the database with default data.
/// </summary>
public class DatabaseInitializer
{
    private readonly LiteDbContext _context;

    public DatabaseInitializer(LiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Seeds the database with default categories if they don't exist.
    /// </summary>
    public void SeedDefaultCategories()
    {
        var categories = _context.GetCollection<Category>();

        // Only seed if no categories exist
        if (categories.Count() > 0)
            return;

        var defaultCategories = new List<Category>
        {
            new() { Name = "Utilities", Color = "#FF9800", IsDefault = true, Description = "Electric, gas, water, etc." },
            new() { Name = "Housing", Color = "#4CAF50", IsDefault = true, Description = "Rent, mortgage, HOA" },
            new() { Name = "Insurance", Color = "#2196F3", IsDefault = true, Description = "Health, auto, home insurance" },
            new() { Name = "Subscriptions", Color = "#9C27B0", IsDefault = true, Description = "Streaming, software, memberships" },
            new() { Name = "Phone & Internet", Color = "#00BCD4", IsDefault = true, Description = "Mobile, landline, internet" },
            new() { Name = "Transportation", Color = "#795548", IsDefault = true, Description = "Car payment, gas, transit" },
            new() { Name = "Healthcare", Color = "#F44336", IsDefault = true, Description = "Medical bills, prescriptions" },
            new() { Name = "Credit Cards", Color = "#607D8B", IsDefault = true, Description = "Credit card payments" },
            new() { Name = "Loans", Color = "#3F51B5", IsDefault = true, Description = "Student loans, personal loans" },
            new() { Name = "Other", Color = "#9E9E9E", IsDefault = true, Description = "Miscellaneous bills" }
        };

        categories.InsertBulk(defaultCategories);
    }
}
