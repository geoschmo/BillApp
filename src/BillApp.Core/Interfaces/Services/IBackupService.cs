namespace BillApp.Core.Interfaces.Services;

/// <summary>
/// Service for creating and restoring encrypted backups.
/// </summary>
public interface IBackupService
{
    Task<string> CreateBackupAsync(string outputPath, string backupPassword);
    Task RestoreBackupAsync(string backupPath, string backupPassword);
}
