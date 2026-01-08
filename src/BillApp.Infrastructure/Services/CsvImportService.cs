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
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPayeeRepository _payeeRepository;

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
        ICategoryRepository categoryRepository,
        IPayeeRepository payeeRepository)
    {
        _billRepository = billRepository;
        _categoryRepository = categoryRepository;
        _payeeRepository = payeeRepository;
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

        // Get existing payees for lookups
        // In ReplaceAll mode, we ignore existing since they'll be deleted
        var existingPayees = mode == ImportMode.ReplaceAll
            ? new List<Payee>()
            : (await _payeeRepository.GetAllAsync()).ToList();
        var existingPaymentAccounts = existingPayees
            .Where(p => p.IsPaymentAccount)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

        // Track payees and payment methods we'll need to create
        var payeesToCreate = new Dictionary<string, ImportPayee>(StringComparer.OrdinalIgnoreCase);
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

            // Parse and validate payee (with optional account fields)
            var payee = ParsePayee(row, result);
            if (payee != null)
            {
                // Track unique payees
                if (!payeesToCreate.ContainsKey(payee.DedupeKey))
                {
                    // Check if payee already exists in database
                    var existingPayee = existingPayees.FirstOrDefault(p =>
                        p.Name.Equals(payee.Name, StringComparison.OrdinalIgnoreCase));

                    if (existingPayee != null)
                    {
                        result.ValidationItems.Add(new ImportValidationItem
                        {
                            RowNumber = row.RowNumber,
                            Field = "Payee",
                            Message = $"Payee '{payee.Name}' already exists in database. Bills will be linked to existing payee.",
                            Severity = ImportValidationSeverity.Info
                        });
                    }
                    else
                    {
                        payeesToCreate[payee.DedupeKey] = payee;
                    }
                }
            }

            // Parse and validate bill
            var bill = ParseBill(row, result);
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
                        !payeesToCreate.Values.Any(p => p.Name.Equals(bill.PaymentMethodName, StringComparison.OrdinalIgnoreCase) && p.IsPaymentAccount) &&
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

        result.Payees = payeesToCreate.Values.ToList();
        result.PaymentAccountsToCreate = paymentAccountsToCreate.ToList();

        // Add summary info
        if (result.Payees.Count > 0)
        {
            result.ValidationItems.Insert(0, new ImportValidationItem
            {
                RowNumber = 0,
                Field = "Summary",
                Message = $"{result.Payees.Count} new payee(s) will be created.",
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

                var existingPayees = (await _payeeRepository.GetAllAsync()).ToList();
                foreach (var payee in existingPayees)
                    await _payeeRepository.DeleteAsync(payee.Id);
            }

            // Get existing payees for lookups (after potential deletion)
            var existingPayees2 = (await _payeeRepository.GetAllAsync()).ToList();
            var payeeLookup = new Dictionary<string, Payee>(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingPayees2)
            {
                payeeLookup[existing.Name.ToLowerInvariant()] = existing;
            }

            // Create payment accounts first (as Payees with IsPaymentAccount=true)
            foreach (var paymentAccountName in validationResult.PaymentAccountsToCreate)
            {
                var paymentAccount = new Payee
                {
                    Name = paymentAccountName,
                    IsAccount = true,
                    AccountType = AccountType.Other,
                    IsActive = true,
                    IsPaymentAccount = true
                };
                await _payeeRepository.InsertAsync(paymentAccount);
                result.PaymentAccountsCreated++;
                payeeLookup[paymentAccountName.ToLowerInvariant()] = paymentAccount;
            }

            // Create payees (which may also be accounts)
            foreach (var importPayee in validationResult.Payees)
            {
                var payee = new Payee
                {
                    Name = importPayee.Name,
                    AccountNumber = importPayee.AccountNumber,
                    CategoryId = importPayee.CategoryId,
                    IsAccount = importPayee.IsAccount,
                    AccountType = importPayee.AccountType,
                    InterestRate = importPayee.InterestRate,
                    IsActive = importPayee.IsActive,
                    IsPaymentAccount = importPayee.IsPaymentAccount
                };
                await _payeeRepository.InsertAsync(payee);
                result.PayeesCreated++;
                payeeLookup[importPayee.DedupeKey] = payee;
            }

            // Build payment account lookup by name
            var paymentAccountLookup = (await _payeeRepository.GetPaymentAccountsAsync())
                .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

            // Create bills
            foreach (var importBill in validationResult.Bills)
            {
                // Find the payee for this bill
                Payee? linkedPayee = null;
                if (payeeLookup.TryGetValue(importBill.PayeeName.ToLowerInvariant(), out var payee))
                {
                    linkedPayee = payee;
                }

                if (linkedPayee == null)
                {
                    // This shouldn't happen if validation passed, but create one just in case
                    linkedPayee = new Payee { Name = importBill.PayeeName };
                    await _payeeRepository.InsertAsync(linkedPayee);
                    payeeLookup[importBill.PayeeName.ToLowerInvariant()] = linkedPayee;
                    result.PayeesCreated++;
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
                    PayeeId = linkedPayee.Id,
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

    private ImportPayee ParsePayee(ImportRow row, ImportValidationResult result)
    {
        var payee = new ImportPayee
        {
            Name = row.AccountName!,
            AccountNumber = row.AccountNumber
        };

        // Check if this row has account-specific fields
        bool hasAccountFields = !string.IsNullOrWhiteSpace(row.AccountType) ||
                                !string.IsNullOrWhiteSpace(row.InterestRate) ||
                                !string.IsNullOrWhiteSpace(row.IsPaymentAccount);

        if (hasAccountFields)
        {
            payee.IsAccount = true;

            // Parse account type
            if (!string.IsNullOrWhiteSpace(row.AccountType))
            {
                if (AccountTypeMap.TryGetValue(row.AccountType, out var accountType))
                {
                    payee.AccountType = accountType;
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
                    payee.AccountType = AccountType.Other;
                }
            }

            // Parse interest rate
            if (!string.IsNullOrWhiteSpace(row.InterestRate))
            {
                if (decimal.TryParse(row.InterestRate.TrimEnd('%'), out var rate))
                {
                    payee.InterestRate = rate;
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

            // Parse IsActive
            if (!string.IsNullOrWhiteSpace(row.IsActive))
            {
                payee.IsActive = ParseBool(row.IsActive, true);
            }

            // Parse IsPaymentAccount
            if (!string.IsNullOrWhiteSpace(row.IsPaymentAccount))
            {
                payee.IsPaymentAccount = ParseBool(row.IsPaymentAccount, false);
            }
        }

        return payee;
    }

    private ImportBill? ParseBill(ImportRow row, ImportValidationResult result)
    {
        var bill = new ImportBill
        {
            PayeeName = row.AccountName!,
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
