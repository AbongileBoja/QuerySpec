using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QuerySpec.Core.Security;

/// <summary>
/// AES-256-GCM authenticated encryption provider.
/// </summary>
/// <remarks>
/// <para>
/// Envelope format (binary, then base64-encoded for the public API):
/// <c>nonce(12) || tag(16) || ciphertext</c>. Total length = 28 + len(plaintext).
/// </para>
/// <para>
/// Each call to <c>Encrypt</c> generates a fresh 96-bit nonce via
/// <see cref="RandomNumberGenerator"/>. Reusing a (key, nonce) pair under GCM is catastrophic
/// — it leaks the authentication subkey — so this class never derives a deterministic nonce
/// and never accepts one from the caller. Different envelopes for the same plaintext are
/// expected and correct.
/// </para>
/// <para>
/// AAD is bound to the ciphertext: a tampered AAD on decrypt produces
/// <see cref="CryptographicException"/>, not corrupt plaintext.
/// </para>
/// </remarks>
public class AesGcmEncryptionProvider : IAuthenticatedEncryptionProvider, IEncryptionProvider
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;

    /// <summary>Initializes the provider with a base64-encoded 256-bit key.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyBase64"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the decoded key is not 32 bytes.</exception>
    public AesGcmEncryptionProvider(string keyBase64)
    {
        ArgumentNullException.ThrowIfNull(keyBase64);
        var key = Convert.FromBase64String(keyBase64);
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be 256-bit ({KeySize} bytes); got {key.Length} bytes.", nameof(keyBase64));
        _key = key;
    }

    /// <summary>Initializes the provider with the supplied 32-byte key.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is not 32 bytes.</exception>
    public AesGcmEncryptionProvider(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
            throw new ArgumentException($"Key must be 256-bit ({KeySize} bytes); got {key.Length} bytes.", nameof(key));
        _key = (byte[])key.Clone();
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext) => Encrypt(plaintext, ReadOnlySpan<byte>.Empty);

    /// <inheritdoc />
    public string Decrypt(string ciphertext) => Decrypt(ciphertext, ReadOnlySpan<byte>.Empty);

    /// <inheritdoc />
    public string Encrypt(string plaintext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var envelope = new byte[NonceSize + TagSize + plainBytes.Length];

        var nonce = envelope.AsSpan(0, NonceSize);
        var tag = envelope.AsSpan(NonceSize, TagSize);
        var cipher = envelope.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var gcm = new AesGcm(_key, TagSize);
        gcm.Encrypt(nonce, plainBytes, cipher, tag, associatedData);

        return Convert.ToBase64String(envelope);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var envelope = Convert.FromBase64String(ciphertext);
        if (envelope.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext envelope is shorter than nonce+tag.");

        var nonce = envelope.AsSpan(0, NonceSize);
        var tag = envelope.AsSpan(NonceSize, TagSize);
        var cipher = envelope.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var gcm = new AesGcm(_key, TagSize);
        gcm.Decrypt(nonce, cipher, tag, plain, associatedData);

        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>Generates a new 256-bit AES key, base64-encoded.</summary>
    public static string GenerateKey()
    {
        Span<byte> key = stackalloc byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
