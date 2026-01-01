using LiteDB;

namespace BillApp.Infrastructure.Data;

/// <summary>
/// Manages the LiteDB database connection.
/// Similar to Entity Framework's DbContext but for LiteDB.
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly string _connectionString;
    private LiteDatabase? _database;
    private bool _disposed;

    public LiteDbContext(string? databasePath = null)
    {
        // Default path: %LocalAppData%\BillApp\billapp.db
        var dbPath = databasePath ?? GetDefaultDatabasePath();

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Connection string with shared connection mode (allows multiple reads)
        // Note: Password encryption will be added in Phase 3 (Security)
        _connectionString = $"Filename={dbPath};Connection=shared";
    }

    /// <summary>
    /// Gets the LiteDB database instance, creating it if needed.
    /// </summary>
    public LiteDatabase Database
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LiteDbContext));

            return _database ??= new LiteDatabase(_connectionString);
        }
    }

    /// <summary>
    /// Gets a typed collection from the database.
    /// Similar to DbSet&lt;T&gt; in Entity Framework.
    /// </summary>
    public ILiteCollection<T> GetCollection<T>(string? name = null)
    {
        return Database.GetCollection<T>(name ?? typeof(T).Name);
    }

    /// <summary>
    /// Gets the default database path in the user's local app data folder.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BillApp",
            "billapp.db");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _database?.Dispose();
            _database = null;
        }

        _disposed = true;
    }
}
