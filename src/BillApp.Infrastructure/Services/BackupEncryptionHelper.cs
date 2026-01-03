using System.Security.Cryptography;
using System.Text;

namespace BillApp.Infrastructure.Services;

/// <summary>
/// Provides password-based encryption for backup files.
/// Uses PBKDF2 for key derivation and AES-256-CBC for encryption.
/// </summary>
public static class BackupEncryptionHelper
{
    private const int SaltSize = 32;
    private const int KeySize = 32; // 256 bits
    private const int IvSize = 16;  // 128 bits
    private const int Iterations = 100000; // PBKDF2 iterations

    /// <summary>
    /// Derives an encryption key from a password using PBKDF2.
    /// </summary>
    public static (byte[] Key, byte[] Salt) DeriveKeyFromPassword(string password, byte[]? existingSalt = null)
    {
        var salt = existingSalt ?? RandomNumberGenerator.GetBytes(SaltSize);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        return (pbkdf2.GetBytes(KeySize), salt);
    }

    /// <summary>
    /// Encrypts data using AES-256-CBC with a password-derived key.
    /// Returns Base64(Salt + IV + Ciphertext).
    /// </summary>
    public static string EncryptWithPassword(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var (key, salt) = DeriveKeyFromPassword(password);
        var iv = RandomNumberGenerator.GetBytes(IvSize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine: Salt (32) + IV (16) + Ciphertext
        var result = new byte[salt.Length + iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length + iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts data that was encrypted with EncryptWithPassword.
    /// Expects Base64(Salt + IV + Ciphertext).
    /// </summary>
    public static string DecryptWithPassword(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var fullData = Convert.FromBase64String(cipherText);

        if (fullData.Length < SaltSize + IvSize + 1)
            throw new CryptographicException("Invalid encrypted data format.");

        // Extract: Salt (32) + IV (16) + Ciphertext
        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var cipherBytes = new byte[fullData.Length - SaltSize - IvSize];

        Buffer.BlockCopy(fullData, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(fullData, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(fullData, SaltSize + IvSize, cipherBytes, 0, cipherBytes.Length);

        var (key, _) = DeriveKeyFromPassword(password, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Encrypts a byte array using AES-256-CBC with a password-derived key.
    /// Returns Salt + IV + Ciphertext as byte array.
    /// </summary>
    public static byte[] EncryptBytesWithPassword(byte[] plainBytes, string password)
    {
        var (key, salt) = DeriveKeyFromPassword(password);
        var iv = RandomNumberGenerator.GetBytes(IvSize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine: Salt (32) + IV (16) + Ciphertext
        var result = new byte[salt.Length + iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length + iv.Length, cipherBytes.Length);

        return result;
    }

    /// <summary>
    /// Decrypts a byte array that was encrypted with EncryptBytesWithPassword.
    /// </summary>
    public static byte[] DecryptBytesWithPassword(byte[] encryptedBytes, string password)
    {
        if (encryptedBytes.Length < SaltSize + IvSize + 1)
            throw new CryptographicException("Invalid encrypted data format.");

        // Extract: Salt (32) + IV (16) + Ciphertext
        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var cipherBytes = new byte[encryptedBytes.Length - SaltSize - IvSize];

        Buffer.BlockCopy(encryptedBytes, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(encryptedBytes, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedBytes, SaltSize + IvSize, cipherBytes, 0, cipherBytes.Length);

        var (key, _) = DeriveKeyFromPassword(password, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }

    /// <summary>
    /// Computes SHA-256 checksum of data.
    /// </summary>
    public static string ComputeChecksum(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Validates password strength.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "Password is required.");

        if (password.Length < 8)
            return (false, "Password must be at least 8 characters long.");

        if (!password.Any(char.IsLetter))
            return (false, "Password must contain at least one letter.");

        if (!password.Any(char.IsDigit))
            return (false, "Password must contain at least one number.");

        return (true, null);
    }
}
