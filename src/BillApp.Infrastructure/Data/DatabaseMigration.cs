using BillApp.Core.Enums;
using BillApp.Core.Models;
using LiteDB;

namespace BillApp.Infrastructure.Data;

/// <summary>
/// Handles database migrations when upgrading between schema versions.
/// </summary>
public class DatabaseMigration
{
    private readonly LiteDbContext _context;
    private const int CurrentSchemaVersion = 2;

    public DatabaseMigration(LiteDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Runs any pending migrations.
    /// </summary>
    public void MigrateIfNeeded()
    {
        var version = GetSchemaVersion();

        if (version < 2)
        {
            MigrateToV2();
        }

        SetSchemaVersion(CurrentSchemaVersion);
    }

    private int GetSchemaVersion()
    {
        var meta = _context.Database.GetCollection<BsonDocument>("_meta");
        var doc = meta.FindById("schema");
        return doc?["version"].AsInt32 ?? 1;
    }

    private void SetSchemaVersion(int version)
    {
        var meta = _context.Database.GetCollection<BsonDocument>("_meta");
        meta.Upsert(new BsonDocument
        {
            ["_id"] = "schema",
            ["version"] = version
        });
    }

    /// <summary>
    /// Migrates from v1 (separate Account model) to v2 (unified Payee model).
    /// </summary>
    private void MigrateToV2()
    {
        // Get raw collections
        var db = _context.Database;
        var accountsCollection = db.GetCollection("Account");
        var payeesCollection = db.GetCollection<Payee>("Payee");
        var billsCollection = db.GetCollection<BsonDocument>("Bill");

        // Check if Account collection exists and has data
        if (!db.CollectionExists("Account"))
            return;

        var accounts = accountsCollection.FindAll().ToList();
        if (accounts.Count == 0)
        {
            // No accounts to migrate, just clean up
            db.DropCollection("Account");
            RemoveAccountIdFromBills(billsCollection);
            return;
        }

        // Map old Account IDs to new Payee IDs
        var accountToPayeeMap = new Dictionary<Guid, Guid>();

        foreach (var account in accounts)
        {
            var accountId = account["_id"].AsGuid;
            var accountName = account["Name"].AsString;

            // Check if a payee with this name already exists
            var existingPayee = payeesCollection.FindOne(p => p.Name.ToLower() == accountName.ToLower());

            if (existingPayee != null)
            {
                // Merge account data into existing payee
                existingPayee.IsAccount = true;
                existingPayee.AccountType = (AccountType)account["AccountType"].AsInt32;
                existingPayee.Balance = account["Balance"].AsDecimal;
                existingPayee.CreditLimit = GetNullableDecimal(account, "CreditLimit");
                existingPayee.InterestRate = GetNullableDecimal(account, "InterestRate");
                existingPayee.Institution = GetNullableString(account, "Institution");
                existingPayee.Username = GetNullableString(account, "Username");
                existingPayee.Password = GetNullableString(account, "Password");
                existingPayee.IsActive = account["IsActive"].AsBoolean;
                existingPayee.IsPaymentAccount = GetBoolean(account, "IsPaymentAccount");

                // Merge PaymentUrl from Account's LoginUrl if payee doesn't have one
                if (string.IsNullOrEmpty(existingPayee.PaymentUrl))
                {
                    existingPayee.PaymentUrl = GetNullableString(account, "LoginUrl");
                }

                // Merge notes
                var accountNotes = GetNullableString(account, "Notes");
                if (!string.IsNullOrEmpty(accountNotes) && string.IsNullOrEmpty(existingPayee.Notes))
                {
                    existingPayee.Notes = accountNotes;
                }

                payeesCollection.Update(existingPayee);
                accountToPayeeMap[accountId] = existingPayee.Id;
            }
            else
            {
                // Create new payee from account
                var newPayee = new Payee
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = GetDateTime(account, "CreatedAt"),
                    UpdatedAt = GetNullableDateTime(account, "UpdatedAt"),
                    Name = accountName,
                    IsAccount = true,
                    AccountType = (AccountType)account["AccountType"].AsInt32,
                    AccountNumber = GetNullableString(account, "AccountNumber"),
                    Balance = account["Balance"].AsDecimal,
                    CreditLimit = GetNullableDecimal(account, "CreditLimit"),
                    InterestRate = GetNullableDecimal(account, "InterestRate"),
                    Institution = GetNullableString(account, "Institution"),
                    PaymentUrl = GetNullableString(account, "LoginUrl"),
                    Username = GetNullableString(account, "Username"),
                    Password = GetNullableString(account, "Password"),
                    Notes = GetNullableString(account, "Notes"),
                    IsActive = account["IsActive"].AsBoolean,
                    IsPaymentAccount = GetBoolean(account, "IsPaymentAccount")
                };

                payeesCollection.Insert(newPayee);
                accountToPayeeMap[accountId] = newPayee.Id;
            }
        }

        // Update bills to use new Payee IDs for PaymentAccountId
        foreach (var bill in billsCollection.FindAll())
        {
            var modified = false;

            // Update PaymentAccountId if it maps to a migrated account
            if (bill.ContainsKey("PaymentAccountId") && !bill["PaymentAccountId"].IsNull)
            {
                var oldPaymentAccountId = bill["PaymentAccountId"].AsGuid;
                if (accountToPayeeMap.TryGetValue(oldPaymentAccountId, out var newPayeeId))
                {
                    bill["PaymentAccountId"] = newPayeeId;
                    modified = true;
                }
            }

            // Remove AccountId field
            if (bill.ContainsKey("AccountId"))
            {
                bill.Remove("AccountId");
                modified = true;
            }

            if (modified)
            {
                billsCollection.Update(bill);
            }
        }

        // Drop the old Account collection
        db.DropCollection("Account");
    }

    private void RemoveAccountIdFromBills(ILiteCollection<BsonDocument> billsCollection)
    {
        foreach (var bill in billsCollection.FindAll())
        {
            if (bill.ContainsKey("AccountId"))
            {
                bill.Remove("AccountId");
                billsCollection.Update(bill);
            }
        }
    }

    private static string? GetNullableString(BsonDocument doc, string key)
    {
        return doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsString : null;
    }

    private static decimal? GetNullableDecimal(BsonDocument doc, string key)
    {
        return doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsDecimal : null;
    }

    private static bool GetBoolean(BsonDocument doc, string key)
    {
        return doc.ContainsKey(key) && doc[key].AsBoolean;
    }

    private static DateTime GetDateTime(BsonDocument doc, string key)
    {
        return doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsDateTime : DateTime.UtcNow;
    }

    private static DateTime? GetNullableDateTime(BsonDocument doc, string key)
    {
        return doc.ContainsKey(key) && !doc[key].IsNull ? doc[key].AsDateTime : null;
    }
}
