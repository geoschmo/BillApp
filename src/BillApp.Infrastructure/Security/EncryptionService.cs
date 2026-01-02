using System.Runtime.Versioning;
using System.Security.Cryptography;
using BillApp.Core.Interfaces.Services;

namespace BillApp.Infrastructure.Security;

/// <summary>
/// Provides AES encryption/decryption for sensitive string data.
/// Used for field-level encryption and backup file encryption.
/// </summary>
[SupportedOSPlatform("windows")]
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(KeyManager keyManager)
    {
        // Derive a key from the database key for field-level encryption
        var databaseKey = keyManager.GetOrCreateDatabaseKey();
        _key = DeriveKey(databaseKey);
    }

    /// <summary>
    /// Encrypts a plaintext string using AES-256-CBC.
    /// Returns a Base64-encoded string containing IV + ciphertext.
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine IV + ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext string.
    /// Expects the format: IV (16 bytes) + ciphertext.
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        // Extract IV (first 16 bytes)
        var iv = new byte[16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        aes.IV = iv;

        // Extract ciphertext
        var cipherBytes = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Derives a 256-bit key from the database key using SHA-256.
    /// </summary>
    private static byte[] DeriveKey(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
    }
}
