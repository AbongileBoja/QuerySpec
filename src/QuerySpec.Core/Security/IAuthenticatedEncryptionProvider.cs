using System;

namespace QuerySpec.Core.Security;

/// <summary>
/// Authenticated encryption provider supporting optional Additional Authenticated Data (AAD).
/// Implementations MUST use an AEAD construction (e.g. AES-GCM) so a tampered ciphertext
/// or AAD fails decryption rather than producing silent plaintext corruption.
/// </summary>
public interface IAuthenticatedEncryptionProvider
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and binds it to the supplied <paramref name="associatedData"/>.
    /// The same AAD must be supplied to <see cref="Decrypt"/> for decryption to succeed.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="associatedData">Authenticated-but-not-encrypted context (e.g. record id, tenant id, version). Pass empty span when not needed.</param>
    /// <returns>Base64-encoded envelope containing nonce, tag, and ciphertext.</returns>
    string Encrypt(string plaintext, ReadOnlySpan<byte> associatedData);

    /// <summary>
    /// Decrypts an envelope produced by <see cref="Encrypt"/>. Throws when the AAD does not
    /// match the encryption-time AAD or when the tag fails to authenticate the ciphertext.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded envelope.</param>
    /// <param name="associatedData">The same AAD passed at encryption time.</param>
    /// <returns>The decrypted plaintext.</returns>
    string Decrypt(string ciphertext, ReadOnlySpan<byte> associatedData);
}
