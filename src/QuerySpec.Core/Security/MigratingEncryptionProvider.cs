using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuerySpec.Core.Security;

/// <summary>
/// <see cref="IEncryptionProvider"/> that prefixes every ciphertext it writes with a short
/// algorithm/key tag and dispatches reads to the inner provider matching the prefix.
/// </summary>
/// <remarks>
/// <para>
/// Use this provider when migrating ciphertext between algorithms or key versions without a
/// big-bang re-encrypt: register the legacy provider as a reader under its tag, register the
/// new provider as both writer and reader, and the storage layer transparently reads either
/// shape while new writes always use the new shape. Re-encryption becomes a background job
/// that can run at the consumer's pace.
/// </para>
/// <para>
/// Envelope format: <c>tag:base64envelope</c>. The <c>tag</c> is a non-empty,
/// <c>[A-Za-z0-9._-]</c>-only string up to 16 chars. Tags must not begin with <c>.</c> or
/// <c>-</c> to keep them safe to spread into CLI / file-name contexts. Tag-only ciphertexts
/// (empty body) are rejected; ciphertexts containing the separator character inside the body
/// are still safe to round-trip because only the first <c>:</c> is treated as the separator.
/// </para>
/// <para>
/// This wrapper does not perform authentication on its own — security is exactly what the
/// inner provider gives. For new data prefer <see cref="AesGcmEncryptionProvider"/> as the
/// writer; legacy <see cref="AesEncryptionProvider"/> ciphertexts can stay readable via a
/// reader registration without infecting fresh writes. Inner providers are not owned by the
/// wrapper — the caller manages their lifetime.
/// </para>
/// </remarks>
public sealed class MigratingEncryptionProvider : IEncryptionProvider
{
    internal const char Separator = ':';
    internal const int MaxTagLength = 16;

    private readonly string _writePrefix;
    private readonly IEncryptionProvider _writer;
    private readonly Dictionary<string, IEncryptionProvider> _readers;
    private readonly string[] _registeredTags;

    /// <summary>
    /// Initializes the migrating provider.
    /// </summary>
    /// <param name="writeTag">Tag prefix attached to every ciphertext written by this instance.</param>
    /// <param name="writer">Provider used for <see cref="Encrypt"/>. Also auto-registered as reader for <paramref name="writeTag"/>.</param>
    /// <param name="legacyReaders">
    /// Read-only registrations: <c>tag -&gt; provider</c>. Use to keep decrypting legacy
    /// ciphertexts after the writer has been upgraded. May be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writeTag"/> or <paramref name="writer"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown on invalid tag (empty, too long, leading <c>.</c>/<c>-</c>, illegal chars),
    /// duplicate tag registrations, or a null provider in <paramref name="legacyReaders"/>.
    /// </exception>
    public MigratingEncryptionProvider(
        string writeTag,
        IEncryptionProvider writer,
        IReadOnlyDictionary<string, IEncryptionProvider>? legacyReaders = null)
    {
        ValidateTag(writeTag, nameof(writeTag));
        ArgumentNullException.ThrowIfNull(writer);

        WriteTag = writeTag;
        _writePrefix = writeTag + Separator;
        _writer = writer;
        _readers = new Dictionary<string, IEncryptionProvider>(StringComparer.Ordinal)
        {
            [writeTag] = writer,
        };

        if (legacyReaders is not null)
        {
            foreach (var kv in legacyReaders)
            {
                ValidateTag(kv.Key, nameof(legacyReaders));
                if (kv.Value is null)
                    throw new ArgumentException($"Reader provider for tag '{kv.Key}' is null.", nameof(legacyReaders));
                if (!_readers.TryAdd(kv.Key, kv.Value))
                    throw new ArgumentException($"Duplicate reader registration for tag '{kv.Key}'. The writer's tag is registered automatically and cannot be re-registered.", nameof(legacyReaders));
            }
        }

        var keys = new string[_readers.Count];
        _readers.Keys.CopyTo(keys, 0);
        _registeredTags = keys;
    }

    /// <inheritdoc />
    /// <remarks>The output is <c>writeTag:innerCiphertext</c>.</remarks>
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return string.Concat(_writePrefix, _writer.Encrypt(plaintext));
    }

    /// <inheritdoc />
    /// <exception cref="FormatException">Thrown when <paramref name="ciphertext"/> is not in <c>tag:envelope</c> form, when the tag is empty, or when the body is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no reader is registered for the parsed tag.</exception>
    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var separatorIdx = ciphertext.AsSpan().IndexOf(Separator);
        if (separatorIdx <= 0)
            throw new FormatException($"Ciphertext is not in <tag>{Separator}<envelope> form.");
        if (separatorIdx == ciphertext.Length - 1)
            throw new FormatException("Ciphertext envelope body is empty.");

        var tag = ciphertext[..separatorIdx];
        if (!_readers.TryGetValue(tag, out var reader))
            throw new InvalidOperationException(
                $"No reader registered for ciphertext tag '{tag}'. Register the original provider for this tag, or re-encrypt under the active writer.");

        var inner = ciphertext[(separatorIdx + 1)..];
        return reader.Decrypt(inner);
    }

    /// <summary>
    /// Returns the read-only set of registered tags. The writer's tag is always present.
    /// The collection is a snapshot taken at construction time and is safe to enumerate
    /// across threads without locking.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTags => _registeredTags;

    /// <summary>The tag stamped on every ciphertext written by this instance.</summary>
    public string WriteTag { get; }

    /// <summary>
    /// Migration is meaningful only at the storage layer (decrypt under reader, re-encrypt
    /// under writer). Calling this on the wrapper is almost always a mistake — it would
    /// rotate the inner writer's key without re-encrypting any persisted ciphertexts and
    /// would invalidate the readers for tags pointing at the same physical provider.
    /// Deprecated and will be removed in 3.0.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    [Obsolete("Key rotation is a storage-layer concern; this method will be removed in 3.0. Implement rotation in the storage layer (Azure Key Vault, AWS KMS, etc.) rather than on IEncryptionProvider.", error: true)]
    public Task RotateKeyAsync() =>
        throw new NotSupportedException(
            "MigratingEncryptionProvider does not own the inner providers' keys and cannot rotate. " +
            "Construct a new instance with the new writer (and the previous writer demoted to a reader) and re-encrypt at the storage layer.");

    internal static void ValidateTag(string tag, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, paramName);
        if (tag.Length > MaxTagLength)
            throw new ArgumentException($"Tag '{tag}' exceeds the {MaxTagLength}-char limit.", paramName);

        var first = tag[0];
        if (first == '.' || first == '-')
            throw new ArgumentException($"Tag '{tag}' must not start with '.' or '-'.", paramName);

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
