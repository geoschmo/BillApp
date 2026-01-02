using System.Runtime.Versioning;
using BillApp.Infrastructure.Data;
using LiteDB;

namespace BillApp.Infrastructure.Security;

/// <summary>
/// Handles migration from unencrypted to encrypted database.
/// </summary>
[SupportedOSPlatform("windows")]
public class DatabaseMigrator
{
    private readonly KeyManager _keyManager;

    public DatabaseMigrator(KeyManager keyManager)
    {
        _keyManager = keyManager;
    }

    /// <summary>
    /// Checks if migration is needed and performs it.
    /// Migration is needed when: database exists but no key file exists.
    /// </summary>
    public void MigrateIfNeeded()
    {
        var dbPath = LiteDbContext.GetDefaultDatabasePath();
        var keyPath = KeyManager.GetDefaultKeyFilePath();

        // If no database exists, nothing to migrate
        if (!File.Exists(dbPath))
            return;

        // If key already exists, database is already encrypted (or will be)
        if (File.Exists(keyPath))
            return;

        // Database exists but no key = unencrypted database needs migration
        MigrateToEncrypted(dbPath);
    }

    private void MigrateToEncrypted(string dbPath)
    {
        var backupPath = dbPath + ".unencrypted.backup";
        var tempEncryptedPath = dbPath + ".encrypted.tmp";

        try
        {
            // Get or create the encryption key
            var password = _keyManager.GetOrCreateDatabaseKey();

            // Open the old unencrypted database
            using (var oldDb = new LiteDatabase($"Filename={dbPath};Connection=direct"))
            {
                // Create new encrypted database
                using (var newDb = new LiteDatabase($"Filename={tempEncryptedPath};Password={password};Connection=direct"))
                {
                    // Copy all collections
                    foreach (var collectionName in oldDb.GetCollectionNames())
                    {
                        var oldCollection = oldDb.GetCollection(collectionName);
                        var newCollection = newDb.GetCollection(collectionName);

                        // Copy all documents
                        foreach (var doc in oldCollection.FindAll())
                        {
                            newCollection.Insert(doc);
                        }
                    }
                }
            }

            // Backup the old database
            File.Move(dbPath, backupPath);

            // Move the new encrypted database into place
            File.Move(tempEncryptedPath, dbPath);
        }
        catch
        {
            // Clean up temp file if migration failed
            if (File.Exists(tempEncryptedPath))
                File.Delete(tempEncryptedPath);

            throw;
        }
    }
}
