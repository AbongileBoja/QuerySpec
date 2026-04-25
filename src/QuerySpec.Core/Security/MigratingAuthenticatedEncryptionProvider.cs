using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// <see cref="IAuthenticatedEncryptionProvider"/> variant of <see cref="MigratingEncryptionProvider"/>.
/// Dispatches AEAD reads/writes by ciphertext tag while preserving the AAD contract.
/// </summary>
/// <remarks>
/// AAD is forwarded unchanged to the inner provider — the wrapper does not include the tag
/// in the AAD. This means a reader registered against an AAD-equivalent ciphertext under a
/// different tag will still authenticate; if you want tag-bound AAD, prepend the tag bytes
/// to your application-supplied AAD before calling <see cref="Encrypt"/>.
/// </remarks>
public sealed class MigratingAuthenticatedEncryptionProvider : IAuthenticatedEncryptionProvider
{
    private const char Separator = ':';
    private const int MaxTagLength = 16;

    private readonly string _writeTag;
    private readonly IAuthenticatedEncryptionProvider _writer;
    private readonly Dictionary<string, IAuthenticatedEncryptionProvider> _readers;

    /// <summary>Initializes the migrating AEAD provider.</summary>
    /// <param name="writeTag">Tag prefix attached to every ciphertext written by this instance.</param>
    /// <param name="writer">Provider used for <see cref="Encrypt"/>. Also auto-registered as reader for <paramref name="writeTag"/>.</param>
    /// <param name="readers">
    /// Additional read-only registrations: <c>tag -&gt; provider</c>. Use to keep decrypting
    /// legacy ciphertexts after the writer has been upgraded. May be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writeTag"/> or <paramref name="writer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown on invalid tag or duplicate tag registrations.</exception>
    public MigratingAuthenticatedEncryptionProvider(
        string writeTag,
        IAuthenticatedEncryptionProvider writer,
        IReadOnlyDictionary<string, IAuthenticatedEncryptionProvider>? readers = null)
    {
        ValidateTag(writeTag, nameof(writeTag));
        ArgumentNullException.ThrowIfNull(writer);

        _writeTag = writeTag;
        _writer = writer;
        _readers = new Dictionary<string, IAuthenticatedEncryptionProvider>(StringComparer.Ordinal)
        {
            [writeTag] = writer,
        };

        if (readers is null) return;

        foreach (var kv in readers)
        {
            ValidateTag(kv.Key, nameof(readers));
            ArgumentNullException.ThrowIfNull(kv.Value, nameof(readers));
            if (!_readers.TryAdd(kv.Key, kv.Value))
                throw new ArgumentException($"Duplicate reader registration for tag '{kv.Key}'.", nameof(readers));
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var inner = _writer.Encrypt(plaintext, associatedData);
        return string.Concat(_writeTag, Separator.ToString(), inner);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var separatorIdx = ciphertext.IndexOf(Separator);
        if (separatorIdx <= 0)
            throw new FormatException($"Ciphertext is not in <tag>{Separator}<envelope> form.");

        var tag = ciphertext[..separatorIdx];
        if (!_readers.TryGetValue(tag, out var reader))
            throw new InvalidOperationException(
                $"No reader registered for ciphertext tag '{tag}'. Register the original provider for this tag, or re-encrypt under the active writer.");

        var inner = ciphertext[(separatorIdx + 1)..];
        return reader.Decrypt(inner, associatedData);
    }

    /// <summary>The tag stamped on every ciphertext written by this instance.</summary>
    public string WriteTag => _writeTag;

    /// <summary>Returns the read-only set of registered tags. The writer's tag is always present.</summary>
    public IReadOnlyCollection<string> RegisteredTags => _readers.Keys;

    private static void ValidateTag(string tag, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, paramName);
        if (tag.Length > MaxTagLength)
            throw new ArgumentException($"Tag '{tag}' exceeds the {MaxTagLength}-char limit.", paramName);

        for (var i = 0; i < tag.Length; i++)
        {
            var c = tag[i];
            var ok = (c >= 'A' && c <= 'Z')
                  || (c >= 'a' && c <= 'z')
                  || (c >= '0' && c <= '9')
                  || c == '-' || c == '_' || c == '.';
            if (!ok)
                throw new ArgumentException($"Tag '{tag}' contains illegal character '{c}'. Allowed: [A-Za-z0-9._-].", paramName);
        }
    }
}
