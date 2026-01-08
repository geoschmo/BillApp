using BillApp.Core.Enums;
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
    /// Checks if this is a fresh database with no data.
    /// </summary>
    public bool IsFreshDatabase()
    {
        var categories = _context.GetCollection<Category>();
        var bills = _context.GetCollection<Bill>();
        var payees = _context.GetCollection<Payee>();

        return categories.Count() == 0 && bills.Count() == 0 && payees.Count() == 0;
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

        var defaultCategories = GetDefaultCategories();
        categories.InsertBulk(defaultCategories);
    }

    /// <summary>
    /// Seeds the database with sample data for demonstration purposes.
    /// </summary>
    public void SeedSampleData()
    {
        var categories = _context.GetCollection<Category>();
        var payees = _context.GetCollection<Payee>();
        var bills = _context.GetCollection<Bill>();

        // Seed categories first
        var defaultCategories = GetDefaultCategories();
        categories.InsertBulk(defaultCategories);

        // Get category IDs
        var utilitiesCategory = defaultCategories.First(c => c.Name == "Utilities");
        var housingCategory = defaultCategories.First(c => c.Name == "Housing");
        var subscriptionsCategory = defaultCategories.First(c => c.Name == "Subscriptions");
        var phoneCategory = defaultCategories.First(c => c.Name == "Phone & Internet");
        var transportationCategory = defaultCategories.First(c => c.Name == "Transportation");
        var creditCardsCategory = defaultCategories.First(c => c.Name == "Credit Cards");
        var insuranceCategory = defaultCategories.First(c => c.Name == "Insurance");

        // Create payees - some are also accounts
        var checkingAccount = new Payee
        {
            Name = "Main Checking",
            IsAccount = true,
            AccountType = AccountType.Checking,
            Institution = "First National Bank",
            Balance = 2500.00m,
            IsActive = true,
            IsPaymentAccount = true,
            Notes = "Primary checking account for bill payments"
        };

        var creditCard = new Payee
        {
            Name = "Rewards Visa",
            CategoryId = creditCardsCategory.Id,
            IsAccount = true,
            AccountType = AccountType.CreditCard,
            Institution = "Capital One",
            Balance = 1250.75m,
            CreditLimit = 5000.00m,
            InterestRate = 19.99m,
            IsActive = true,
            IsPaymentAccount = true,
            Notes = "2% cash back on all purchases"
        };

        var savingsAccount = new Payee
        {
            Name = "Emergency Savings",
            IsAccount = true,
            AccountType = AccountType.Savings,
            Institution = "First National Bank",
            Balance = 10000.00m,
            InterestRate = 4.5m,
            IsActive = true,
            IsPaymentAccount = false
        };

        var carLoan = new Payee
        {
            Name = "Credit Union Auto Loan",
            CategoryId = transportationCategory.Id,
            IsAccount = true,
            AccountType = AccountType.Loan,
            Institution = "Credit Union",
            Balance = 15420.00m,
            InterestRate = 5.9m,
            IsActive = true,
            Notes = "2022 Honda Civic - Payment 24 of 48"
        };

        // Non-account payees
        var oakwoodApartments = new Payee
        {
            Name = "Oakwood Apartments",
            CategoryId = housingCategory.Id,
            Notes = "Apartment rent - Unit 204"
        };

        var cityPower = new Payee
        {
            Name = "City Power & Light",
            CategoryId = utilitiesCategory.Id,
            PaymentUrl = "https://citypowerlight.com/pay"
        };

        var spectrumInternet = new Payee
        {
            Name = "Spectrum Internet",
            CategoryId = phoneCategory.Id,
            Notes = "200 Mbps plan"
        };

        var verizonWireless = new Payee
        {
            Name = "Verizon Wireless",
            CategoryId = phoneCategory.Id
        };

        var netflix = new Payee
        {
            Name = "Netflix",
            CategoryId = subscriptionsCategory.Id
        };

        var spotifyPremium = new Payee
        {
            Name = "Spotify Premium",
            CategoryId = subscriptionsCategory.Id
        };

        var geico = new Payee
        {
            Name = "GEICO",
            CategoryId = insuranceCategory.Id
        };

        var cityWater = new Payee
        {
            Name = "City Water Department",
            CategoryId = utilitiesCategory.Id
        };

        payees.InsertBulk(new[]
        {
            checkingAccount, creditCard, savingsAccount, carLoan,
            oakwoodApartments, cityPower, spectrumInternet, verizonWireless,
            netflix, spotifyPremium, geico, cityWater
        });

        var today = DateTime.Today;
        var thisMonth = new DateTime(today.Year, today.Month, 1);

        // Create sample bills - mix of pending and paid
        var sampleBills = new List<Bill>
        {
            // Rent - paid this month
            new()
            {
                PayeeId = oakwoodApartments.Id,
                AmountDue = 1450.00m,
                AmountPaid = 1450.00m,
                Balance = 0,
                DueDate = thisMonth.AddDays(1),
                Status = PaymentStatus.Paid,
                PaidDate = thisMonth.AddDays(1),
                Frequency = RecurrenceFrequency.Monthly,
                PaymentAccountId = checkingAccount.Id
            },

            // Electric - pending
            new()
            {
                PayeeId = cityPower.Id,
                AmountDue = 125.50m,
                AmountPaid = 0,
                Balance = 125.50m,
                DueDate = today.AddDays(10),
                Status = PaymentStatus.Pending,
                Frequency = RecurrenceFrequency.Monthly
            },

            // Internet - pending
            new()
            {
                PayeeId = spectrumInternet.Id,
                AmountDue = 79.99m,
                AmountPaid = 0,
                Balance = 79.99m,
                DueDate = today.AddDays(5),
                Status = PaymentStatus.Pending,
                Frequency = RecurrenceFrequency.Monthly
            },

            // Phone - paid
            new()
            {
                PayeeId = verizonWireless.Id,
                AmountDue = 85.00m,
                AmountPaid = 85.00m,
                Balance = 0,
                DueDate = thisMonth.AddDays(14),
                Status = PaymentStatus.Paid,
                PaidDate = thisMonth.AddDays(12),
                Frequency = RecurrenceFrequency.Monthly,
                PaymentAccountId = checkingAccount.Id
            },

            // Netflix - pending
            new()
            {
                PayeeId = netflix.Id,
                AmountDue = 15.99m,
                AmountPaid = 0,
                Balance = 15.99m,
                DueDate = today.AddDays(3),
                Status = PaymentStatus.Pending,
                Frequency = RecurrenceFrequency.Monthly
            },

            // Spotify - paid with credit card
            new()
            {
                PayeeId = spotifyPremium.Id,
                AmountDue = 10.99m,
                AmountPaid = 10.99m,
                Balance = 0,
                DueDate = thisMonth.AddDays(7),
                Status = PaymentStatus.Paid,
                PaidDate = thisMonth.AddDays(7),
                Frequency = RecurrenceFrequency.Monthly,
                PaymentAccountId = creditCard.Id
            },

            // Car payment - pending (payee is also the loan account)
            new()
            {
                PayeeId = carLoan.Id,
                AmountDue = 385.00m,
                AmountPaid = 0,
                Balance = 15420.00m,
                DueDate = today.AddDays(8),
                Status = PaymentStatus.Pending,
                Frequency = RecurrenceFrequency.Monthly
            },

            // Credit card payment - pending (payee is also the credit card account)
            new()
            {
                PayeeId = creditCard.Id,
                AmountDue = 150.00m,
                AmountPaid = 0,
                Balance = 1250.75m,
                DueDate = today.AddDays(12),
                Status = PaymentStatus.Pending,
                Frequency = RecurrenceFrequency.Monthly
            },

            // Car insurance - paid
            new()
            {
                PayeeId = geico.Id,
                AmountDue = 142.00m,
                AmountPaid = 142.00m,
                Balance = 0,
                DueDate = thisMonth.AddDays(20),
                Status = PaymentStatus.Paid,
                PaidDate = thisMonth.AddDays(18),
                Frequency = RecurrenceFrequency.Monthly,
                PaymentAccountId = checkingAccount.Id
            },

            // Water bill - overdue
            new()
            {
                PayeeId = cityWater.Id,
                AmountDue = 45.00m,
                AmountPaid = 0,
                Balance = 45.00m,
                DueDate = today.AddDays(-3),
                Status = PaymentStatus.Pending, // Will show as overdue due to date
                Frequency = RecurrenceFrequency.Monthly
            }
        };

        bills.InsertBulk(sampleBills);
    }

    private List<Category> GetDefaultCategories()
    {
        return new List<Category>
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
    }
}
