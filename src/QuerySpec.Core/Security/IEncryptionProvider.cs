using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Security;

/// <summary>
/// Defines contract for encryption providers.
/// </summary>
/// <remarks>
/// <see cref="RotateKeyAsync()"/> has a paired <see cref="CancellationToken"/>-accepting overload
/// added in 2.1. The CT-less overload is preserved for source compatibility and delegates to the
/// CT overload with <see cref="CancellationToken.None"/>.
/// </remarks>
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

    /// <summary>Rotates the encryption key for enhanced security.</summary>
    /// <returns>A task that completes once the rotation has been performed by the implementation.</returns>
    Task RotateKeyAsync();
    /// <summary>Rotates the encryption key for enhanced security with cancellation support.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O during rotation.</param>
    /// <returns>A task that completes once the rotation has been performed by the implementation.</returns>
    Task RotateKeyAsync(CancellationToken cancellationToken)
        => RotateKeyAsync();
}
