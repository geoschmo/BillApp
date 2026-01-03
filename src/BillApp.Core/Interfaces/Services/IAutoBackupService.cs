namespace BillApp.Core.Interfaces.Services;

/// <summary>
/// Service for managing automatic scheduled backups.
/// </summary>
public interface IAutoBackupService : IDisposable
{
    /// <summary>
    /// Starts the auto-backup scheduler.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the auto-backup scheduler.
    /// </summary>
    void Stop();

    /// <summary>
    /// Checks if a backup is due and creates one if needed.
    /// </summary>
    Task CheckAndBackupIfDueAsync();
}
