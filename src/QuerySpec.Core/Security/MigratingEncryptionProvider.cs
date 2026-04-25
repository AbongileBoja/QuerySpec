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
/// <c>[A-Za-z0-9._-]</c>-only string up to 16 chars. Tag-only ciphertexts (empty body) and
/// ciphertexts containing the separator character inside the body are still safe to round-trip
/// because the body is the inner provider's own opaque output.
/// </para>
/// <para>
/// This wrapper does not perform authentication on its own — security is exactly what the
/// inner provider gives. For new data prefer <see cref="AesGcmEncryptionProvider"/> as the
/// writer; legacy <see cref="AesEncryptionProvider"/> ciphertexts can stay readable via a
/// reader registration without infecting fresh writes.
/// </para>
/// </remarks>
public sealed class MigratingEncryptionProvider : IEncryptionProvider
{
    private const char Separator = ':';
    private const int MaxTagLength = 16;

    private readonly string _writeTag;
    private readonly IEncryptionProvider _writer;
    private readonly Dictionary<string, IEncryptionProvider> _readers;

    /// <summary>
    /// Initializes the migrating provider.
    /// </summary>
    /// <param name="writeTag">Tag prefix attached to every ciphertext written by this instance.</param>
    /// <param name="writer">Provider used for <see cref="Encrypt"/>. Also auto-registered as reader for <paramref name="writeTag"/>.</param>
    /// <param name="readers">
    /// Additional read-only registrations: <c>tag -&gt; provider</c>. Use to keep decrypting
    /// legacy ciphertexts after the writer has been upgraded. May be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writeTag"/> or <paramref name="writer"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown on invalid tag (empty, too long, illegal chars) or duplicate tag registrations.</exception>
    public MigratingEncryptionProvider(
        string writeTag,
        IEncryptionProvider writer,
        IReadOnlyDictionary<string, IEncryptionProvider>? readers = null)
    {
        ValidateTag(writeTag, nameof(writeTag));
        ArgumentNullException.ThrowIfNull(writer);

        _writeTag = writeTag;
        _writer = writer;
        _readers = new Dictionary<string, IEncryptionProvider>(StringComparer.Ordinal)
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
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var inner = _writer.Encrypt(plaintext);
        return string.Concat(_writeTag, Separator.ToString(), inner);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
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
        return reader.Decrypt(inner);
    }

    /// <summary>
    /// Returns the read-only set of registered tags. The writer's tag is always present.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTags => _readers.Keys;

    /// <summary>The tag stamped on every ciphertext written by this instance.</summary>
    public string WriteTag => _writeTag;

    /// <summary>
    /// Migration is meaningful only at the storage layer (decrypt under reader, re-encrypt
    /// under writer). Calling this on the wrapper is almost always a mistake — it would
    /// rotate the inner writer's key without re-encrypting any persisted ciphertexts and
    /// would invalidate the readers for tags pointing at the same physical provider.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public Task RotateKeyAsync() =>
        throw new NotSupportedException(
            "MigratingEncryptionProvider does not own the inner providers' keys and cannot rotate. " +
            "Construct a new instance with the new writer (and the previous writer demoted to a reader) and re-encrypt at the storage layer.");

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
