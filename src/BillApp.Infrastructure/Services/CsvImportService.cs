using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;

namespace BillApp.Infrastructure.Services;

/// <summary>
/// Service for importing data from CSV files.
/// </summary>
public class CsvImportService : IImportService
{
    private readonly IBillRepository _billRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;

    // Valid frequency values (case-insensitive)
    private static readonly Dictionary<string, RecurrenceFrequency> FrequencyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "none", RecurrenceFrequency.None },
        { "", RecurrenceFrequency.None },
        { "weekly", RecurrenceFrequency.Weekly },
        { "biweekly", RecurrenceFrequency.BiWeekly },
        { "bi-weekly", RecurrenceFrequency.BiWeekly },
        { "every 2 weeks", RecurrenceFrequency.BiWeekly },
        { "monthly", RecurrenceFrequency.Monthly },
        { "quarterly", RecurrenceFrequency.Quarterly },
        { "annually", RecurrenceFrequency.Annually },
        { "annual", RecurrenceFrequency.Annually },
        { "yearly", RecurrenceFrequency.Annually }
    };

    // Valid account type values (case-insensitive)
    private static readonly Dictionary<string, AccountType> AccountTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "checking", AccountType.Checking },
        { "savings", AccountType.Savings },
        { "creditcard", AccountType.CreditCard },
        { "credit card", AccountType.CreditCard },
        { "credit", AccountType.CreditCard },
        { "loan", AccountType.Loan },
        { "other", AccountType.Other },
        { "", AccountType.Other }
    };

    public CsvImportService(
        IBillRepository billRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository)
    {
        _billRepository = billRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<ImportValidationResult> ValidateFileAsync(string filePath, ImportMode mode)
    {
        var result = new ImportValidationResult();

        if (!File.Exists(filePath))
        {
            result.ValidationItems.Add(new ImportValidationItem
            {
                RowNumber = 0,
                Field = "File",
                Message = "File not found.",
                Severity = ImportValidationSeverity.Error
            });
            return result;
        }

        // Parse CSV file
        var rows = await ParseCsvAsync(filePath);
        result.TotalRows = rows.Count;

        if (rows.Count == 0)
        {
            result.ValidationItems.Add(new ImportValidationItem
            {
                RowNumber = 0,
                Field = "File",
                Message = "No data rows found in file.",
                Severity = ImportValidationSeverity.Error
            });
            return result;
        }

        // Get existing accounts and payment accounts for lookups
        // In ReplaceAll mode, we ignore existing since they'll be deleted
        var existingAccounts = mode == ImportMode.ReplaceAll
            ? new List<Account>()
            : (await _accountRepository.GetAllAsync()).ToList();
        var existingPaymentAccounts = existingAccounts
            .Where(a => a.IsPaymentAccount)
            .ToDictionary(a => a.Name.ToLowerInvariant(), a => a);

        // Track accounts and payment methods we'll need to create
        var accountsToCreate = new Dictionary<string, ImportAccount>(StringComparer.OrdinalIgnoreCase);
        var paymentAccountsToCreate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Process each row
        foreach (var row in rows)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(row.AccountName))
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "AccountName",
                    Message = "AccountName is required.",
                    Severity = ImportValidationSeverity.Error
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.DueDate))
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "DueDate",
                    Message = "DueDate is required.",
                    Severity = ImportValidationSeverity.Error
                });
                continue;
            }

            // Parse and validate account
            var account = ParseAccount(row, result);
            if (account != null)
            {
                // Track unique accounts
                if (!accountsToCreate.ContainsKey(account.DedupeKey))
                {
                    // Check if account already exists in database (only matters in AddToExisting mode)
                    var existingAccount = existingAccounts.FirstOrDefault(a =>
                        a.Name.Equals(account.Name, StringComparison.OrdinalIgnoreCase) &&
                        (a.AccountNumber ?? "").Equals(account.AccountNumber ?? "", StringComparison.OrdinalIgnoreCase));

                    if (existingAccount != null)
                    {
                        result.ValidationItems.Add(new ImportValidationItem
                        {
                            RowNumber = row.RowNumber,
                            Field = "Account",
                            Message = $"Account '{account.Name}' already exists in database. Bills will be linked to existing account.",
                            Severity = ImportValidationSeverity.Info
                        });
                    }
                    else
                    {
                        accountsToCreate[account.DedupeKey] = account;
                    }
                }
            }

            // Parse and validate bill
            var bill = ParseBill(row, account, result);
            if (bill != null)
            {
                result.Bills.Add(bill);

                // Check payment method
                if (!string.IsNullOrWhiteSpace(bill.PaymentMethodName) &&
                    !bill.PaymentMethodName.Equals("cash", StringComparison.OrdinalIgnoreCase))
                {
                    var paymentMethodLower = bill.PaymentMethodName.ToLowerInvariant();

                    // Check if it exists or will be created
                    if (!existingPaymentAccounts.ContainsKey(paymentMethodLower) &&
                        !accountsToCreate.Values.Any(a => a.Name.Equals(bill.PaymentMethodName, StringComparison.OrdinalIgnoreCase) && a.IsPaymentAccount) &&
                        !paymentAccountsToCreate.Contains(bill.PaymentMethodName))
                    {
                        paymentAccountsToCreate.Add(bill.PaymentMethodName);
                        result.ValidationItems.Add(new ImportValidationItem
                        {
                            RowNumber = row.RowNumber,
                            Field = "PaymentMethod",
                            Message = $"Payment account '{bill.PaymentMethodName}' will be created.",
                            Severity = ImportValidationSeverity.Warning
                        });
                    }
                }
            }
        }

        result.Accounts = accountsToCreate.Values.ToList();
        result.PaymentAccountsToCreate = paymentAccountsToCreate.ToList();

        // Add summary info
        if (result.Accounts.Count > 0)
        {
            result.ValidationItems.Insert(0, new ImportValidationItem
            {
                RowNumber = 0,
                Field = "Summary",
                Message = $"{result.Accounts.Count} new account(s) will be created.",
                Severity = ImportValidationSeverity.Info
            });
        }

        if (result.Bills.Count > 0)
        {
            result.ValidationItems.Insert(0, new ImportValidationItem
            {
                RowNumber = 0,
                Field = "Summary",
                Message = $"{result.Bills.Count} bill(s) will be imported.",
                Severity = ImportValidationSeverity.Info
            });
        }

        return result;
    }

    public async Task<ImportExecutionResult> ExecuteImportAsync(ImportValidationResult validationResult, ImportMode mode)
    {
        var result = new ImportExecutionResult();

        try
        {
            // If replacing all, delete existing data
            if (mode == ImportMode.ReplaceAll)
            {
                var existingBills = (await _billRepository.GetAllAsync()).ToList();
                foreach (var bill in existingBills)
                    await _billRepository.DeleteAsync(bill.Id);

                var existingAccounts = (await _accountRepository.GetAllAsync()).ToList();
                foreach (var account in existingAccounts)
                    await _accountRepository.DeleteAsync(account.Id);
            }

            // Get existing accounts for lookups (after potential deletion)
            var existingAccounts2 = (await _accountRepository.GetAllAsync()).ToList();
            var accountLookup = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingAccounts2)
            {
                var key = $"{existing.Name}|{existing.AccountNumber ?? ""}".ToLowerInvariant();
                accountLookup[key] = existing;
            }

            // Create payment accounts first
            foreach (var paymentAccountName in validationResult.PaymentAccountsToCreate)
            {
                var paymentAccount = new Account
                {
                    Name = paymentAccountName,
                    AccountType = AccountType.Other,
                    IsActive = true,
                    IsPaymentAccount = true
                };
                await _accountRepository.InsertAsync(paymentAccount);
                result.PaymentAccountsCreated++;

                var key = $"{paymentAccount.Name}|".ToLowerInvariant();
                accountLookup[key] = paymentAccount;
            }

            // Create accounts
            foreach (var importAccount in validationResult.Accounts)
            {
                var account = new Account
                {
                    Name = importAccount.Name,
                    AccountNumber = importAccount.AccountNumber,
                    InterestRate = importAccount.InterestRate,
                    AccountType = importAccount.AccountType,
                    IsActive = importAccount.IsActive,
                    IsPaymentAccount = importAccount.IsPaymentAccount
                };
                await _accountRepository.InsertAsync(account);
                result.AccountsCreated++;

                accountLookup[importAccount.DedupeKey] = account;
            }

            // Build payment account lookup by name
            var paymentAccountLookup = (await _accountRepository.GetAllAsync())
                .Where(a => a.IsPaymentAccount)
                .ToDictionary(a => a.Name.ToLowerInvariant(), a => a);

            // Create bills
            foreach (var importBill in validationResult.Bills)
            {
                // Find the account for this bill
                Account? linkedAccount = null;
                if (!string.IsNullOrEmpty(importBill.AccountDedupeKey) &&
                    accountLookup.TryGetValue(importBill.AccountDedupeKey, out var acct))
                {
                    linkedAccount = acct;
                }

                // Determine payment method
                Guid? paymentAccountId = null;
                bool isCashPayment = false;

                if (!string.IsNullOrWhiteSpace(importBill.PaymentMethodName))
                {
                    if (importBill.PaymentMethodName.Equals("cash", StringComparison.OrdinalIgnoreCase))
                    {
                        isCashPayment = true;
                    }
                    else if (paymentAccountLookup.TryGetValue(importBill.PaymentMethodName.ToLowerInvariant(), out var paymentAccount))
                    {
                        paymentAccountId = paymentAccount.Id;
                    }
                }

                var bill = new Bill
                {
                    Payee = importBill.Payee,
                    AccountId = linkedAccount?.Id,
                    Frequency = importBill.Frequency,
                    AmountDue = importBill.AmountDue,
                    AmountPaid = importBill.AmountPaid,
                    Balance = importBill.Balance,
                    Confirmation = importBill.Confirmation,
                    DueDate = importBill.DueDate,
                    PaidDate = importBill.PaidDate,
                    Status = importBill.Status,
                    PaymentAccountId = paymentAccountId,
                    IsCashPayment = isCashPayment
                };
                await _billRepository.InsertAsync(bill);
                result.BillsCreated++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<List<ImportRow>> ParseCsvAsync(string filePath)
    {
        var rows = new List<ImportRow>();
        var lines = await File.ReadAllLinesAsync(filePath);

        if (lines.Length < 2) // Need at least header + 1 data row
            return rows;

        // Parse header to get column indices
        var header = ParseCsvLine(lines[0]);
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < header.Length; i++)
        {
            var columnName = header[i].Trim();
            if (!string.IsNullOrEmpty(columnName))
                columnMap[columnName] = i;
        }

        // Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = ParseCsvLine(line);
            var row = new ImportRow { RowNumber = i + 1 }; // 1-based row numbers

            row.AccountName = GetValue(values, columnMap, "AccountName");
            row.AccountNumber = GetValue(values, columnMap, "AccountNumber");
            row.InterestRate = GetValue(values, columnMap, "InterestRate");
            row.AccountType = GetValue(values, columnMap, "AccountType");
            row.IsActive = GetValue(values, columnMap, "IsActive");
            row.IsPaymentAccount = GetValue(values, columnMap, "IsPaymentAccount");
            row.Frequency = GetValue(values, columnMap, "Frequency");
            row.AmountDue = GetValue(values, columnMap, "AmountDue");
            row.AmountPaid = GetValue(values, columnMap, "AmountPaid");
            row.Balance = GetValue(values, columnMap, "Balance");
            row.Confirmation = GetValue(values, columnMap, "Confirmation");
            row.DueDate = GetValue(values, columnMap, "DueDate");
            row.PaidDate = GetValue(values, columnMap, "PaidDate");
            row.PaymentMethod = GetValue(values, columnMap, "PaymentMethod");

            rows.Add(row);
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = "";
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }

        values.Add(current.Trim());
        return values.ToArray();
    }

    private static string? GetValue(string[] values, Dictionary<string, int> columnMap, string columnName)
    {
        if (columnMap.TryGetValue(columnName, out int index) && index < values.Length)
        {
            var value = values[index].Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        return null;
    }

    private ImportAccount? ParseAccount(ImportRow row, ImportValidationResult result)
    {
        var account = new ImportAccount
        {
            Name = row.AccountName!,
            AccountNumber = row.AccountNumber
        };

        // Parse interest rate
        if (!string.IsNullOrWhiteSpace(row.InterestRate))
        {
            if (decimal.TryParse(row.InterestRate.TrimEnd('%'), out var rate))
            {
                account.InterestRate = rate;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "InterestRate",
                    Message = $"Invalid interest rate '{row.InterestRate}'. Using null.",
                    Severity = ImportValidationSeverity.Warning
                });
            }
        }

        // Parse account type
        if (!string.IsNullOrWhiteSpace(row.AccountType))
        {
            if (AccountTypeMap.TryGetValue(row.AccountType, out var accountType))
            {
                account.AccountType = accountType;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "AccountType",
                    Message = $"Invalid account type '{row.AccountType}'. Using 'Other'.",
                    Severity = ImportValidationSeverity.Warning
                });
                account.AccountType = AccountType.Other;
            }
        }

        // Parse IsActive
        if (!string.IsNullOrWhiteSpace(row.IsActive))
        {
            account.IsActive = ParseBool(row.IsActive, true);
        }

        // Parse IsPaymentAccount
        if (!string.IsNullOrWhiteSpace(row.IsPaymentAccount))
        {
            account.IsPaymentAccount = ParseBool(row.IsPaymentAccount, false);
        }

        return account;
    }

    private ImportBill? ParseBill(ImportRow row, ImportAccount? account, ImportValidationResult result)
    {
        var bill = new ImportBill
        {
            Payee = row.AccountName!,
            AccountDedupeKey = account?.DedupeKey,
            PaymentMethodName = row.PaymentMethod,
            Confirmation = row.Confirmation
        };

        // Parse frequency
        var frequencyStr = row.Frequency ?? "";
        if (FrequencyMap.TryGetValue(frequencyStr, out var frequency))
        {
            bill.Frequency = frequency;
        }
        else
        {
            result.ValidationItems.Add(new ImportValidationItem
            {
                RowNumber = row.RowNumber,
                Field = "Frequency",
                Message = $"Invalid frequency '{row.Frequency}'. Using 'None'.",
                Severity = ImportValidationSeverity.Warning
            });
            bill.Frequency = RecurrenceFrequency.None;
        }

        // Parse AmountDue
        if (!string.IsNullOrWhiteSpace(row.AmountDue))
        {
            if (decimal.TryParse(row.AmountDue.TrimStart('$').Replace(",", ""), out var amountDue))
            {
                bill.AmountDue = amountDue;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "AmountDue",
                    Message = $"Invalid amount '{row.AmountDue}'. Using 0.",
                    Severity = ImportValidationSeverity.Warning
                });
            }
        }

        // Parse AmountPaid
        if (!string.IsNullOrWhiteSpace(row.AmountPaid))
        {
            if (decimal.TryParse(row.AmountPaid.TrimStart('$').Replace(",", ""), out var amountPaid))
            {
                bill.AmountPaid = amountPaid;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "AmountPaid",
                    Message = $"Invalid amount '{row.AmountPaid}'. Using 0.",
                    Severity = ImportValidationSeverity.Warning
                });
            }
        }

        // Parse Balance
        if (!string.IsNullOrWhiteSpace(row.Balance))
        {
            if (decimal.TryParse(row.Balance.TrimStart('$').Replace(",", ""), out var balance))
            {
                bill.Balance = balance;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "Balance",
                    Message = $"Invalid balance '{row.Balance}'. Using 0.",
                    Severity = ImportValidationSeverity.Warning
                });
            }
        }

        // Parse DueDate
        if (DateTime.TryParse(row.DueDate, out var dueDate))
        {
            bill.DueDate = dueDate;
        }
        else
        {
            result.ValidationItems.Add(new ImportValidationItem
            {
                RowNumber = row.RowNumber,
                Field = "DueDate",
                Message = $"Invalid date '{row.DueDate}'.",
                Severity = ImportValidationSeverity.Error
            });
            return null;
        }

        // Parse PaidDate and determine status
        if (!string.IsNullOrWhiteSpace(row.PaidDate))
        {
            if (DateTime.TryParse(row.PaidDate, out var paidDate))
            {
                bill.PaidDate = paidDate;
                bill.Status = PaymentStatus.Paid;
            }
            else
            {
                result.ValidationItems.Add(new ImportValidationItem
                {
                    RowNumber = row.RowNumber,
                    Field = "PaidDate",
                    Message = $"Invalid date '{row.PaidDate}'. Bill will be marked as Pending.",
                    Severity = ImportValidationSeverity.Warning
                });
                bill.Status = PaymentStatus.Pending;
            }
        }
        else
        {
            bill.Status = PaymentStatus.Pending;
        }

        return bill;
    }

    private static bool ParseBool(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var lower = value.ToLowerInvariant();
        return lower switch
        {
            "true" or "yes" or "1" or "active" => true,
            "false" or "no" or "0" or "inactive" => false,
            _ => defaultValue
        };
    }
}
