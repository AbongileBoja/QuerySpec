using System;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>
    /// Rotates the encryption key. Deprecated: every shipping implementation throws
    /// <see cref="NotSupportedException"/> because the provider does not own the persisted
    /// ciphertexts. Implement rotation at the storage layer (Azure Key Vault, AWS KMS, etc.):
    /// decrypt under the old provider, re-encrypt under the new provider.
    /// </summary>
    /// <returns>A task that completes once the rotation has been performed by the implementation.</returns>
    [Obsolete("Key rotation is a storage-layer concern; this method will be removed in 3.0. Implement rotation in the storage layer (Azure Key Vault, AWS KMS, etc.) rather than on IEncryptionProvider.", error: true)]
    Task RotateKeyAsync();
    /// <summary>
    /// Cancellation-aware overload of <see cref="RotateKeyAsync()"/>. Deprecated alongside the
    /// no-token overload; will be removed in 3.0. Implement key rotation at the storage layer.
    /// </summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O during rotation.</param>
    /// <returns>A task that completes once the rotation has been performed by the implementation.</returns>
    [Obsolete("Key rotation is a storage-layer concern; this method will be removed in 3.0. Implement rotation in the storage layer (Azure Key Vault, AWS KMS, etc.) rather than on IEncryptionProvider.", error: true)]
    Task RotateKeyAsync(CancellationToken cancellationToken)
#pragma warning disable CS0619 // Obsolete-error self-reference: the DIM delegates to the CT-less overload that is itself obsolete; both ship as a coupled deprecation pair removed together in 3.0.
        => RotateKeyAsync();
#pragma warning restore CS0619
}
