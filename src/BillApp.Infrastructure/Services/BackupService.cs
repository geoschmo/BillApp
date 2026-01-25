using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BillApp.Core.Enums;
using BillApp.Core.Exceptions;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;

namespace BillApp.Infrastructure.Services;

/// <summary>
/// Service for creating and restoring encrypted, portable backups.
/// </summary>
public class BackupService : IBackupService
{
    private readonly IBillRepository _billRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPayeeRepository _payeeRepository;
    private readonly IEncryptionService _encryptionService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public BackupService(
        IBillRepository billRepository,
        ICategoryRepository categoryRepository,
        IPayeeRepository payeeRepository,
        IEncryptionService encryptionService)
    {
        _billRepository = billRepository;
        _categoryRepository = categoryRepository;
        _payeeRepository = payeeRepository;
        _encryptionService = encryptionService;
    }

    public async Task<string> CreateBackupAsync(string outputPath, string backupPassword)
    {
        // Validate password
        var (isValid, errorMessage) = BackupEncryptionHelper.ValidatePassword(backupPassword);
        if (!isValid)
            throw new WeakPasswordException(errorMessage!);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Export all data
        var backupData = await ExportDataAsync(backupPassword);

        // Create manifest
        var manifest = CreateManifest(backupData);

        // Serialize data to JSON
        var dataJson = JsonSerializer.Serialize(backupData, JsonOptions);
        var dataBytes = Encoding.UTF8.GetBytes(dataJson);

        // Encrypt data with backup password
        var encryptedData = BackupEncryptionHelper.EncryptBytesWithPassword(dataBytes, backupPassword);

        // Compute checksum
        var checksum = BackupEncryptionHelper.ComputeChecksum(encryptedData);

        // Create ZIP archive
        using var fileStream = new FileStream(outputPath, FileMode.Create);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        // Add manifest (plaintext)
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var manifestStream = manifestEntry.Open())
        {
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
            await manifestStream.WriteAsync(manifestBytes);
        }

        // Add encrypted data
        var dataEntry = archive.CreateEntry("data.enc");
        using (var dataStream = dataEntry.Open())
        {
            await dataStream.WriteAsync(encryptedData);
        }

        // Add checksum
        var checksumEntry = archive.CreateEntry("checksum.sha256");
        using (var checksumStream = checksumEntry.Open())
        {
            var checksumBytes = Encoding.UTF8.GetBytes(checksum);
            await checksumStream.WriteAsync(checksumBytes);
        }

        return outputPath;
    }

    public async Task RestoreBackupAsync(string backupPath, string backupPassword)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup file not found.", backupPath);

        // Extract and validate archive
        using var fileStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        // Read manifest
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new CorruptedBackupException("Missing manifest.json");

        BackupManifest manifest;
        using (var manifestStream = manifestEntry.Open())
        using (var reader = new StreamReader(manifestStream))
        {
            var manifestJson = await reader.ReadToEndAsync();
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson)
                ?? throw new CorruptedBackupException("Invalid manifest.json");
        }

        // Validate version compatibility
        ValidateBackupVersion(manifest);

        // Read encrypted data
        var dataEntry = archive.GetEntry("data.enc")
            ?? throw new CorruptedBackupException("Missing data.enc");

        byte[] encryptedData;
        using (var dataStream = dataEntry.Open())
        using (var memoryStream = new MemoryStream())
        {
            await dataStream.CopyToAsync(memoryStream);
            encryptedData = memoryStream.ToArray();
        }

        // Read and verify checksum
        var checksumEntry = archive.GetEntry("checksum.sha256")
            ?? throw new CorruptedBackupException("Missing checksum.sha256");

        string expectedChecksum;
        using (var checksumStream = checksumEntry.Open())
        using (var reader = new StreamReader(checksumStream))
        {
            expectedChecksum = (await reader.ReadToEndAsync()).Trim();
        }

        var actualChecksum = BackupEncryptionHelper.ComputeChecksum(encryptedData);
        if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
            throw new CorruptedBackupException("Checksum verification failed");

        // Decrypt data
        byte[] decryptedBytes;
        try
        {
            decryptedBytes = BackupEncryptionHelper.DecryptBytesWithPassword(encryptedData, backupPassword);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidBackupPasswordException();
        }

        // Deserialize data - handle both v1.0 and v2.0 formats
        var dataJson = Encoding.UTF8.GetString(decryptedBytes);

        if (manifest.Version == "1.0")
        {
            // Parse as v1.0 format with Accounts array
            var v1Data = JsonSerializer.Deserialize<BackupDataV1>(dataJson)
                ?? throw new CorruptedBackupException("Invalid backup data");
            await ImportDataV1Async(v1Data, backupPassword);
        }
        else
        {
            // Parse as v2.0 format
            var backupData = JsonSerializer.Deserialize<BackupData>(dataJson)
                ?? throw new CorruptedBackupException("Invalid backup data");
            await ImportDataAsync(backupData, backupPassword);
        }
    }

    /// <summary>
    /// Reads the manifest from a backup file without decrypting the data.
    /// </summary>
    public static async Task<BackupManifest> ReadManifestAsync(string backupPath)
    {
        using var fileStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new CorruptedBackupException("Missing manifest.json");

        using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestJson = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<BackupManifest>(manifestJson)
            ?? throw new CorruptedBackupException("Invalid manifest.json");
    }

    private async Task<BackupData> ExportDataAsync(string backupPassword)
    {
        var backupData = new BackupData();

        // Export bills
        var bills = await _billRepository.GetAllAsync();
        foreach (var bill in bills)
        {
            backupData.Bills.Add(new BillBackupDto
            {
                Id = bill.Id,
                CreatedAt = bill.CreatedAt,
                UpdatedAt = bill.UpdatedAt,
                PayeeId = bill.PayeeId,
                AmountDue = bill.AmountDue,
                AmountPaid = bill.AmountPaid,
                Balance = bill.Balance,
                DueDate = bill.DueDate,
                Status = (int)bill.Status,
                PaidDate = bill.PaidDate,
                Frequency = (int)bill.Frequency,
                Notes = bill.Notes,
                ColorCode = bill.ColorCode,
                PreviousBillId = bill.PreviousBillId,
                Confirmation = bill.Confirmation,
                PaymentAccountId = bill.PaymentAccountId,
                IsCashPayment = bill.IsCashPayment
            });
        }

        // Export categories
        var categories = await _categoryRepository.GetAllAsync();
        foreach (var category in categories)
        {
            backupData.Categories.Add(new CategoryBackupDto
            {
                Id = category.Id,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                Name = category.Name,
                Description = category.Description,
                Color = category.Color,
                IsDefault = category.IsDefault
            });
        }

        // Export payees (with account fields, re-encrypt sensitive fields with backup password)
        var payees = await _payeeRepository.GetAllAsync();
        foreach (var payee in payees)
        {
            backupData.Payees.Add(new PayeeBackupDto
            {
                Id = payee.Id,
                CreatedAt = payee.CreatedAt,
                UpdatedAt = payee.UpdatedAt,
                Name = payee.Name,
                CategoryId = payee.CategoryId,
                PaymentUrl = payee.PaymentUrl,
                AccountNumber = ReEncryptForBackup(payee.AccountNumber, backupPassword),
                Notes = payee.Notes,
                IsAccount = payee.IsAccount,
                AccountType = payee.AccountType.HasValue ? (int)payee.AccountType.Value : null,
                Balance = payee.Balance,
                CreditLimit = payee.CreditLimit,
                InterestRate = payee.InterestRate,
                Institution = payee.Institution,
                Username = ReEncryptForBackup(payee.Username, backupPassword),
                Password = ReEncryptForBackup(payee.Password, backupPassword),
                IsActive = payee.IsActive,
                IsPaymentAccount = payee.IsPaymentAccount
            });
        }

        return backupData;
    }

    private async Task ImportDataAsync(BackupData backupData, string backupPassword)
    {
        // Clear existing data (in reverse order of dependencies)
        var existingBills = (await _billRepository.GetAllAsync()).ToList();
        foreach (var bill in existingBills)
            await _billRepository.DeleteAsync(bill.Id);

        var existingPayees = (await _payeeRepository.GetAllAsync()).ToList();
        foreach (var payee in existingPayees)
            await _payeeRepository.DeleteAsync(payee.Id);

        var existingCategories = (await _categoryRepository.GetAllAsync()).ToList();
        foreach (var category in existingCategories)
            await _categoryRepository.DeleteAsync(category.Id);

        // Import categories first
        foreach (var dto in backupData.Categories)
        {
            var category = new Category
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                Description = dto.Description,
                Color = dto.Color,
                IsDefault = dto.IsDefault
            };
            await _categoryRepository.InsertAsync(category);
        }

        // Import payees (with account fields, re-encrypt sensitive fields with local key)
        foreach (var dto in backupData.Payees)
        {
            var payee = new Payee
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                CategoryId = dto.CategoryId,
                PaymentUrl = dto.PaymentUrl,
                AccountNumber = ReEncryptFromBackup(dto.AccountNumber, backupPassword),
                Notes = dto.Notes,
                IsAccount = dto.IsAccount,
                AccountType = dto.AccountType.HasValue ? (AccountType)dto.AccountType.Value : null,
                Balance = dto.Balance,
                CreditLimit = dto.CreditLimit,
                InterestRate = dto.InterestRate,
                Institution = dto.Institution,
                Username = ReEncryptFromBackup(dto.Username, backupPassword),
                Password = ReEncryptFromBackup(dto.Password, backupPassword),
                IsActive = dto.IsActive,
                IsPaymentAccount = dto.IsPaymentAccount
            };
            await _payeeRepository.InsertAsync(payee);
        }

        // Import bills
        foreach (var dto in backupData.Bills)
        {
            var bill = new Bill
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                PayeeId = dto.PayeeId,
                AmountDue = dto.AmountDue,
                AmountPaid = dto.AmountPaid,
                Balance = dto.Balance,
                DueDate = dto.DueDate,
                Status = (PaymentStatus)dto.Status,
                PaidDate = dto.PaidDate,
                Frequency = (RecurrenceFrequency)dto.Frequency,
                Notes = dto.Notes,
                ColorCode = dto.ColorCode,
                PreviousBillId = dto.PreviousBillId,
                Confirmation = dto.Confirmation,
                PaymentAccountId = dto.PaymentAccountId,
                IsCashPayment = dto.IsCashPayment
            };
            await _billRepository.InsertAsync(bill);
        }
    }

    /// <summary>
    /// Imports data from v1.0 backup format, migrating Accounts into Payees.
    /// </summary>
    private async Task ImportDataV1Async(BackupDataV1 backupData, string backupPassword)
    {
        // Clear existing data
        var existingBills = (await _billRepository.GetAllAsync()).ToList();
        foreach (var bill in existingBills)
            await _billRepository.DeleteAsync(bill.Id);

        var existingPayees = (await _payeeRepository.GetAllAsync()).ToList();
        foreach (var payee in existingPayees)
            await _payeeRepository.DeleteAsync(payee.Id);

        var existingCategories = (await _categoryRepository.GetAllAsync()).ToList();
        foreach (var category in existingCategories)
            await _categoryRepository.DeleteAsync(category.Id);

        // Import categories first
        foreach (var dto in backupData.Categories)
        {
            var category = new Category
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                Description = dto.Description,
                Color = dto.Color,
                IsDefault = dto.IsDefault
            };
            await _categoryRepository.InsertAsync(category);
        }

        // Map old Account IDs to new Payee IDs
        var accountToPayeeMap = new Dictionary<Guid, Guid>();
        var payeesByName = backupData.Payees.ToDictionary(p => p.Name.ToLower(), p => p);

        // Import payees first
        foreach (var dto in backupData.Payees)
        {
            var payee = new Payee
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                CategoryId = dto.CategoryId,
                PaymentUrl = dto.PaymentUrl,
                AccountNumber = dto.AccountNumber,
                Notes = dto.Notes,
                IsAccount = false,
                IsActive = true,
                IsPaymentAccount = false
            };
            await _payeeRepository.InsertAsync(payee);
        }

        // Now process accounts - merge with existing payees or create new ones
        foreach (var accountDto in backupData.Accounts)
        {
            if (payeesByName.TryGetValue(accountDto.Name.ToLower(), out var existingPayeeDto))
            {
                // Merge account data into existing payee
                var payee = await _payeeRepository.GetByIdAsync(existingPayeeDto.Id);
                if (payee != null)
                {
                    payee.IsAccount = true;
                    payee.AccountType = (AccountType)accountDto.AccountType;
                    payee.Balance = accountDto.Balance;
                    payee.CreditLimit = accountDto.CreditLimit;
                    payee.InterestRate = accountDto.InterestRate;
                    payee.Institution = accountDto.Institution;
                    payee.Username = ReEncryptFromBackup(accountDto.Username, backupPassword);
                    payee.Password = ReEncryptFromBackup(accountDto.Password, backupPassword);
                    payee.IsActive = accountDto.IsActive;
                    payee.IsPaymentAccount = accountDto.IsPaymentAccount;

                    if (string.IsNullOrEmpty(payee.PaymentUrl))
                        payee.PaymentUrl = accountDto.LoginUrl;

                    if (string.IsNullOrEmpty(payee.AccountNumber))
                        payee.AccountNumber = ReEncryptFromBackup(accountDto.AccountNumber, backupPassword);

                    await _payeeRepository.UpdateAsync(payee);
                    accountToPayeeMap[accountDto.Id] = payee.Id;
                }
            }
            else
            {
                // Create new payee from account
                var newPayee = new Payee
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = accountDto.CreatedAt,
                    UpdatedAt = accountDto.UpdatedAt,
                    Name = accountDto.Name,
                    IsAccount = true,
                    AccountType = (AccountType)accountDto.AccountType,
                    AccountNumber = ReEncryptFromBackup(accountDto.AccountNumber, backupPassword),
                    Balance = accountDto.Balance,
                    CreditLimit = accountDto.CreditLimit,
                    InterestRate = accountDto.InterestRate,
                    Institution = accountDto.Institution,
                    PaymentUrl = accountDto.LoginUrl,
                    Username = ReEncryptFromBackup(accountDto.Username, backupPassword),
                    Password = ReEncryptFromBackup(accountDto.Password, backupPassword),
                    Notes = accountDto.Notes,
                    IsActive = accountDto.IsActive,
                    IsPaymentAccount = accountDto.IsPaymentAccount
                };
                await _payeeRepository.InsertAsync(newPayee);
                accountToPayeeMap[accountDto.Id] = newPayee.Id;
            }
        }

        // Import bills (update PaymentAccountId to new Payee IDs)
        foreach (var dto in backupData.Bills)
        {
            var bill = new Bill
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                PayeeId = dto.PayeeId,
                AmountDue = dto.AmountDue,
                AmountPaid = dto.AmountPaid,
                Balance = dto.Balance,
                DueDate = dto.DueDate,
                Status = (PaymentStatus)dto.Status,
                PaidDate = dto.PaidDate,
                Frequency = (RecurrenceFrequency)dto.Frequency,
                Notes = dto.Notes,
                PreviousBillId = dto.PreviousBillId,
                Confirmation = dto.Confirmation,
                IsCashPayment = dto.IsCashPayment
            };

            // Map PaymentAccountId from old Account ID to new Payee ID
            if (dto.PaymentAccountId.HasValue && accountToPayeeMap.TryGetValue(dto.PaymentAccountId.Value, out var newPayeeId))
            {
                bill.PaymentAccountId = newPayeeId;
            }

            await _billRepository.InsertAsync(bill);
        }
    }

    private string? ReEncryptForBackup(string? encryptedValue, string backupPassword)
    {
        if (string.IsNullOrEmpty(encryptedValue))
            return encryptedValue;

        try
        {
            // Decrypt with machine key
            var plainText = _encryptionService.Decrypt(encryptedValue);

            // Re-encrypt with backup password
            return BackupEncryptionHelper.EncryptWithPassword(plainText, backupPassword);
        }
        catch
        {
            // If decryption fails, value might not be encrypted
            return encryptedValue;
        }
    }

    private string? ReEncryptFromBackup(string? backupEncryptedValue, string backupPassword)
    {
        if (string.IsNullOrEmpty(backupEncryptedValue))
            return backupEncryptedValue;

        try
        {
            // Decrypt with backup password
            var plainText = BackupEncryptionHelper.DecryptWithPassword(backupEncryptedValue, backupPassword);

            // Re-encrypt with machine key
            return _encryptionService.Encrypt(plainText);
        }
        catch
        {
            // If decryption fails, return as-is
            return backupEncryptedValue;
        }
    }

    private BackupManifest CreateManifest(BackupData backupData)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;

        return new BackupManifest
        {
            Version = "2.0",
            AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0",
            CreatedAt = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            EntityCounts = new BackupEntityCounts
            {
                Bills = backupData.Bills.Count,
                Categories = backupData.Categories.Count,
                Payees = backupData.Payees.Count
            }
        };
    }

    private void ValidateBackupVersion(BackupManifest manifest)
    {
        // Support both v1.0 and v2.0
        if (manifest.Version != "1.0" && manifest.Version != "2.0")
        {
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            throw new IncompatibleBackupVersionException(
                manifest.Version,
                currentVersion?.ToString() ?? "2.0.0");
        }
    }
}

/// <summary>
/// V1.0 backup data format with separate Accounts collection.
/// Used for backward compatibility when restoring old backups.
/// </summary>
internal class BackupDataV1
{
    public List<BillBackupDtoV1> Bills { get; set; } = new();
    public List<AccountBackupDtoV1> Accounts { get; set; } = new();
    public List<CategoryBackupDto> Categories { get; set; } = new();
    public List<PayeeBackupDtoV1> Payees { get; set; } = new();
}

internal class BillBackupDtoV1
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

internal class AccountBackupDtoV1
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AccountType { get; set; }
    public string? AccountNumber { get; set; }
    public string? Institution { get; set; }
    public decimal Balance { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? InterestRate { get; set; }
    public string? LoginUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaymentAccount { get; set; }
}

internal class PayeeBackupDtoV1
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
