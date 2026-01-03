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
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEncryptionService _encryptionService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public BackupService(
        IBillRepository billRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IEncryptionService encryptionService)
    {
        _billRepository = billRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
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

        // Deserialize data
        var dataJson = Encoding.UTF8.GetString(decryptedBytes);
        var backupData = JsonSerializer.Deserialize<BackupData>(dataJson)
            ?? throw new CorruptedBackupException("Invalid backup data");

        // Import data
        await ImportDataAsync(backupData, backupPassword);
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
                Payee = bill.Payee,
                AmountDue = bill.AmountDue,
                AmountPaid = bill.AmountPaid,
                Balance = bill.Balance,
                DueDate = bill.DueDate,
                Status = (int)bill.Status,
                PaidDate = bill.PaidDate,
                Frequency = (int)bill.Frequency,
                CategoryId = bill.CategoryId,
                AccountId = bill.AccountId,
                Notes = bill.Notes,
                PaymentUrl = bill.PaymentUrl,
                AccountNumber = bill.AccountNumber,
                PreviousBillId = bill.PreviousBillId
            });
        }

        // Export accounts (re-encrypt sensitive fields with backup password)
        var accounts = await _accountRepository.GetAllAsync();
        foreach (var account in accounts)
        {
            backupData.Accounts.Add(new AccountBackupDto
            {
                Id = account.Id,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                Name = account.Name,
                AccountType = (int)account.AccountType,
                AccountNumber = ReEncryptForBackup(account.AccountNumber, backupPassword),
                Institution = account.Institution,
                Balance = account.Balance,
                CreditLimit = account.CreditLimit,
                InterestRate = account.InterestRate,
                LoginUrl = account.LoginUrl,
                Username = ReEncryptForBackup(account.Username, backupPassword),
                Password = ReEncryptForBackup(account.Password, backupPassword),
                Notes = account.Notes,
                IsActive = account.IsActive
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

        return backupData;
    }

    private async Task ImportDataAsync(BackupData backupData, string backupPassword)
    {
        // Clear existing data (in reverse order of dependencies)
        // Use ToList() to materialize the enumerable before iterating,
        // as LiteDB returns lazy enumerables that can't be modified during iteration
        var existingBills = (await _billRepository.GetAllAsync()).ToList();
        foreach (var bill in existingBills)
            await _billRepository.DeleteAsync(bill.Id);

        var existingAccounts = (await _accountRepository.GetAllAsync()).ToList();
        foreach (var account in existingAccounts)
            await _accountRepository.DeleteAsync(account.Id);

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

        // Import accounts (re-encrypt sensitive fields with local key)
        foreach (var dto in backupData.Accounts)
        {
            var account = new Account
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                AccountType = (AccountType)dto.AccountType,
                AccountNumber = ReEncryptFromBackup(dto.AccountNumber, backupPassword),
                Institution = dto.Institution,
                Balance = dto.Balance,
                CreditLimit = dto.CreditLimit,
                InterestRate = dto.InterestRate,
                LoginUrl = dto.LoginUrl,
                Username = ReEncryptFromBackup(dto.Username, backupPassword),
                Password = ReEncryptFromBackup(dto.Password, backupPassword),
                Notes = dto.Notes,
                IsActive = dto.IsActive
            };
            await _accountRepository.InsertAsync(account);
        }

        // Import bills
        foreach (var dto in backupData.Bills)
        {
            var bill = new Bill
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Payee = dto.Payee,
                AmountDue = dto.AmountDue,
                AmountPaid = dto.AmountPaid,
                Balance = dto.Balance,
                DueDate = dto.DueDate,
                Status = (PaymentStatus)dto.Status,
                PaidDate = dto.PaidDate,
                Frequency = (RecurrenceFrequency)dto.Frequency,
                CategoryId = dto.CategoryId,
                AccountId = dto.AccountId,
                Notes = dto.Notes,
                PaymentUrl = dto.PaymentUrl,
                AccountNumber = dto.AccountNumber,
                PreviousBillId = dto.PreviousBillId
            };
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
            Version = "1.0",
            AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0",
            CreatedAt = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            EntityCounts = new BackupEntityCounts
            {
                Bills = backupData.Bills.Count,
                Accounts = backupData.Accounts.Count,
                Categories = backupData.Categories.Count
            }
        };
    }

    private void ValidateBackupVersion(BackupManifest manifest)
    {
        // Currently we only support version 1.0
        if (manifest.Version != "1.0")
        {
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            throw new IncompatibleBackupVersionException(
                manifest.Version,
                currentVersion?.ToString() ?? "1.0.0");
        }
    }
}
