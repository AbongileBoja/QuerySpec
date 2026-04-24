using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Security;

/// <summary>
/// AES-256 CBC encryption provider with key rotation support. <see cref="Aes"/> instances
/// are pooled per thread because creating one is expensive (~1 KB allocation + cryptographic
/// service provider init). Each thread gets a single reusable <see cref="Aes"/>; IVs are
/// still regenerated per call via <see cref="RandomNumberGenerator"/>.
/// </summary>
public class AesEncryptionProvider : IEncryptionProvider
{
    private byte[] _key;
    private readonly object _lockObj = new();

    /// <summary>
    /// Per-thread reusable <see cref="Aes"/>. The provider owns the instance lifecycle for
    /// the life of the thread; the OS reclaims when the thread exits. Sharing is correct —
    /// <see cref="ICryptoTransform"/> instances are created per call, not stored on the Aes.
    /// </summary>
    private static readonly ThreadLocal<Aes> AesPool = new(() =>
    {
        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    });

    private const int IvSize = 16; // AES-CBC block size in bytes

    /// <summary>Initializes the AES encryption provider with a base64-encoded key.</summary>
    public AesEncryptionProvider(string keyBase64)
    {
        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new ArgumentException("Key must be 256-bit (32 bytes)");
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-CBC with a random IV. The IV is prepended to the
    /// ciphertext (standard CBC convention) so <see cref="Decrypt"/> can recover it.
    /// </summary>
    public string Encrypt(string plaintext)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);

        var aes = AesPool.Value!;
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        byte[] cipherBytes;
        using (var encryptor = aes.CreateEncryptor(_key, iv))
        {
            cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        var result = new byte[IvSize + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, result, IvSize, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts ciphertext produced by <see cref="Encrypt"/>. Expects the IV in the first
    /// <see cref="IvSize"/> bytes.
    /// </summary>
    public string Decrypt(string ciphertext)
    {
        if (ciphertext is null) throw new ArgumentNullException(nameof(ciphertext));

        var buffer = Convert.FromBase64String(ciphertext);
        if (buffer.Length < IvSize)
            throw new ArgumentException("Ciphertext too short to contain IV.", nameof(ciphertext));

        var iv = new byte[IvSize];
        Buffer.BlockCopy(buffer, 0, iv, 0, IvSize);

        var aes = AesPool.Value!;
        byte[] plainBytes;
        using (var decryptor = aes.CreateDecryptor(_key, iv))
        {
            plainBytes = decryptor.TransformFinalBlock(buffer, IvSize, buffer.Length - IvSize);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Rotates encryption key (stub for production implementation).
    /// </summary>
    public Task RotateKeyAsync()
    {
        lock (_lockObj)
        {
            // Production implementation would:
            // 1. Generate new key
            // 2. Re-encrypt all data with new key
            // 3. Store old key for decryption of old data
            // 4. Transition to new key
        }
        return Task.CompletedTask;
    }

    /// <summary>Generates a new 256-bit encryption key.</summary>
    public static string GenerateKey()
    {
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }
    }
}
