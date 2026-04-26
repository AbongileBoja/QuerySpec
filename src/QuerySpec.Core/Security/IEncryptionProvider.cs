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
    string Encrypt(string plaintext);
    /// <summary>Decrypts ciphertext using the configured algorithm.</summary>
    string Decrypt(string ciphertext);

    /// <summary>Rotates the encryption key for enhanced security.</summary>
    Task RotateKeyAsync();
    /// <summary>Rotates the encryption key for enhanced security with cancellation support.</summary>
    Task RotateKeyAsync(CancellationToken cancellationToken)
        => RotateKeyAsync();
}
