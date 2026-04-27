namespace QuerySpec.Core.Security;

/// <summary>
/// Defines contract for encryption providers.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>Encrypts plaintext using the configured algorithm.</summary>
    /// <param name="plaintext">UTF-8 plaintext to encrypt.</param>
    /// <returns>Provider-defined ciphertext envelope (typically base64-encoded).</returns>
    string Encrypt(string plaintext);
    /// <summary>Decrypts ciphertext using the configured algorithm.</summary>
    /// <param name="ciphertext">Provider-defined ciphertext envelope as produced by <see cref="Encrypt"/>.</param>
    /// <returns>The recovered UTF-8 plaintext.</returns>
    string Decrypt(string ciphertext);
}
