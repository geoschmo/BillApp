using System.Runtime.Versioning;
using System.Security.Cryptography;
using BillApp.Infrastructure;

namespace BillApp.Infrastructure.Security;

/// <summary>
/// Manages the database encryption key using Windows DPAPI.
/// The key is protected by the current Windows user account.
/// </summary>
[SupportedOSPlatform("windows")]
public class KeyManager
{
    private readonly string _keyFilePath;
    private string? _cachedKey;

    public KeyManager(string? keyFilePath = null)
    {
        _keyFilePath = keyFilePath ?? GetDefaultKeyFilePath();
    }

    /// <summary>
    /// Gets or creates the database encryption key.
    /// On first run, generates a new key and protects it with DPAPI.
    /// On subsequent runs, loads and unprotects the existing key.
    /// </summary>
    public string GetOrCreateDatabaseKey()
    {
        if (_cachedKey != null)
            return _cachedKey;

        if (File.Exists(_keyFilePath))
        {
            _cachedKey = LoadKey();
        }
        else
        {
            _cachedKey = GenerateAndSaveKey();
        }

        return _cachedKey;
    }

    /// <summary>
    /// Checks if a key file exists (indicates encryption is set up).
    /// </summary>
    public bool KeyExists => File.Exists(_keyFilePath);

    /// <summary>
    /// Gets the default key file path.
    /// </summary>
    public static string GetDefaultKeyFilePath()
    {
        return AppStoragePaths.KeyFilePath;
    }

    private string GenerateAndSaveKey()
    {
        // Generate a 32-byte (256-bit) random key
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var key = Convert.ToBase64String(keyBytes);

        // Protect the key using DPAPI (CurrentUser scope)
        var keyData = System.Text.Encoding.UTF8.GetBytes(key);
        var protectedData = ProtectedData.Protect(
            keyData,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_keyFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save the protected key
        File.WriteAllBytes(_keyFilePath, protectedData);

        return key;
    }

    private string LoadKey()
    {
        // Read the protected key
        var protectedData = File.ReadAllBytes(_keyFilePath);

        // Unprotect using DPAPI
        var keyData = ProtectedData.Unprotect(
            protectedData,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return System.Text.Encoding.UTF8.GetString(keyData);
    }
}
