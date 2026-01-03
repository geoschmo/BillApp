namespace BillApp.Core.Models;

/// <summary>
/// Metadata about a backup file. Stored as plaintext in the backup archive.
/// </summary>
public class BackupManifest
{
    public string Version { get; set; } = "1.0";
    public string AppVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string MachineName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public string EncryptionMethod { get; set; } = "AES-256-CBC-PBKDF2";

    public BackupEntityCounts EntityCounts { get; set; } = new();
}

public class BackupEntityCounts
{
    public int Bills { get; set; }
    public int Accounts { get; set; }
    public int Categories { get; set; }
}
