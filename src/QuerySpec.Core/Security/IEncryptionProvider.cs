using System;
using System.Threading.Tasks;

namespace QuerySpec.Core.Security;

/// <summary>
/// Defines contract for encryption providers.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>Encrypts plaintext using the configured algorithm.</summary>
    string Encrypt(string plaintext);
    /// <summary>Decrypts ciphertext using the configured algorithm.</summary>
    string Decrypt(string ciphertext);
    /// <summary>Rotates the encryption key for enhanced security.</summary>
    Task RotateKeyAsync();
}
