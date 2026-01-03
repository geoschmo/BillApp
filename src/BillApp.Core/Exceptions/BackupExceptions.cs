namespace BillApp.Core.Exceptions;

/// <summary>
/// Base exception for backup-related errors.
/// </summary>
public class BackupException : Exception
{
    public BackupException(string message) : base(message) { }
    public BackupException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the backup password is incorrect.
/// </summary>
public class InvalidBackupPasswordException : BackupException
{
    public InvalidBackupPasswordException()
        : base("The backup password is incorrect.") { }
}

/// <summary>
/// Thrown when the backup file is corrupted or invalid.
/// </summary>
public class CorruptedBackupException : BackupException
{
    public CorruptedBackupException()
        : base("The backup file is corrupted or invalid.") { }

    public CorruptedBackupException(string details)
        : base($"The backup file is corrupted or invalid: {details}") { }
}

/// <summary>
/// Thrown when the backup version is not compatible with the current app version.
/// </summary>
public class IncompatibleBackupVersionException : BackupException
{
    public string BackupVersion { get; }
    public string CurrentVersion { get; }

    public IncompatibleBackupVersionException(string backupVersion, string currentVersion)
        : base($"Backup version {backupVersion} is not compatible with app version {currentVersion}.")
    {
        BackupVersion = backupVersion;
        CurrentVersion = currentVersion;
    }
}

/// <summary>
/// Thrown when password validation fails.
/// </summary>
public class WeakPasswordException : BackupException
{
    public WeakPasswordException(string message)
        : base(message) { }
}
