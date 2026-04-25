using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace QuerySpec.Core.Security;

/// <summary>
/// <see cref="IAuthenticatedEncryptionProvider"/> variant of <see cref="MigratingEncryptionProvider"/>.
/// Dispatches AEAD reads/writes by ciphertext tag and binds the tag into the authenticated
/// associated data so a tag swap on persisted ciphertext fails decryption.
/// </summary>
/// <remarks>
/// <para>
/// Tag binding closes the downgrade vector where an attacker rewrites a ciphertext's prefix
/// (e.g. swap <c>v2:</c> for <c>v1:</c>) to coerce decryption with a different — potentially
/// weaker — key. The wrapper folds the ASCII tag bytes plus a single <c>0x00</c> separator
/// into the front of the AAD that is forwarded to the inner provider, so a swapped tag
/// produces a different AAD and AEAD authentication fails.
/// </para>
/// <para>
/// Caller-supplied AAD is appended after the tag-bind prefix and is otherwise unmodified —
/// existing AAD semantics (record id, tenant id, version) are preserved.
/// </para>
/// </remarks>
public sealed class MigratingAuthenticatedEncryptionProvider : IAuthenticatedEncryptionProvider
{
    private const char Separator = MigratingEncryptionProvider.Separator;
    private const byte AadTagTerminator = 0x00;
    private const int StackBindBudget = 64;

    private readonly string _writePrefix;
    private readonly byte[] _writeTagBindBytes;
    private readonly IAuthenticatedEncryptionProvider _writer;
    private readonly Dictionary<string, ReaderRegistration> _readers;
    private readonly string[] _registeredTags;

    /// <summary>Initializes the migrating AEAD provider.</summary>
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
    public MigratingAuthenticatedEncryptionProvider(
        string writeTag,
        IAuthenticatedEncryptionProvider writer,
        IReadOnlyDictionary<string, IAuthenticatedEncryptionProvider>? legacyReaders = null)
    {
        MigratingEncryptionProvider.ValidateTag(writeTag, nameof(writeTag));
        ArgumentNullException.ThrowIfNull(writer);

        WriteTag = writeTag;
        _writePrefix = writeTag + Separator;
        _writeTagBindBytes = TagBindBytes(writeTag);
        _writer = writer;
        _readers = new Dictionary<string, ReaderRegistration>(StringComparer.Ordinal)
        {
            [writeTag] = new(writer, _writeTagBindBytes),
        };

        if (legacyReaders is not null)
        {
            foreach (var kv in legacyReaders)
            {
                MigratingEncryptionProvider.ValidateTag(kv.Key, nameof(legacyReaders));
                if (kv.Value is null)
                    throw new ArgumentException($"Reader provider for tag '{kv.Key}' is null.", nameof(legacyReaders));
                if (!_readers.TryAdd(kv.Key, new ReaderRegistration(kv.Value, TagBindBytes(kv.Key))))
                    throw new ArgumentException($"Duplicate reader registration for tag '{kv.Key}'. The writer's tag is registered automatically and cannot be re-registered.", nameof(legacyReaders));
            }
        }

        var keys = new string[_readers.Count];
        _readers.Keys.CopyTo(keys, 0);
        _registeredTags = keys;
    }

    /// <inheritdoc />
    /// <remarks>The output is <c>writeTag:innerCiphertext</c>. The tag is bound into the AAD passed to the inner provider.</remarks>
    public string Encrypt(string plaintext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return string.Concat(_writePrefix, EncryptWithBinding(_writer, plaintext, _writeTagBindBytes, associatedData));
    }

    /// <inheritdoc />
    /// <exception cref="FormatException">Thrown when <paramref name="ciphertext"/> is not in <c>tag:envelope</c> form, when the tag is empty, or when the body is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no reader is registered for the parsed tag.</exception>
    public string Decrypt(string ciphertext, ReadOnlySpan<byte> associatedData)
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
        return DecryptWithBinding(reader.Provider, inner, reader.TagBindBytes, associatedData);
    }

    /// <summary>The tag stamped on every ciphertext written by this instance.</summary>
    public string WriteTag { get; }

    /// <summary>
    /// Returns the read-only set of registered tags. The writer's tag is always present.
    /// Snapshot taken at construction time.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTags => _registeredTags;

    private static byte[] TagBindBytes(string tag)
    {
        var bytes = new byte[Encoding.ASCII.GetByteCount(tag) + 1];
        Encoding.ASCII.GetBytes(tag, bytes);
        bytes[^1] = AadTagTerminator;
        return bytes;
    }

    private static string EncryptWithBinding(
        IAuthenticatedEncryptionProvider provider,
        string plaintext,
        byte[] bind,
        ReadOnlySpan<byte> callerAad)
    {
        var total = bind.Length + callerAad.Length;
        if (total <= StackBindBudget)
        {
            Span<byte> buf = stackalloc byte[StackBindBudget];
            buf = buf[..total];
            bind.CopyTo(buf);
            callerAad.CopyTo(buf[bind.Length..]);
            return provider.Encrypt(plaintext, buf);
        }

        var rented = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            bind.CopyTo(rented);
            callerAad.CopyTo(rented.AsSpan(bind.Length));
            return provider.Encrypt(plaintext, rented.AsSpan(0, total));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static string DecryptWithBinding(
        IAuthenticatedEncryptionProvider provider,
        string ciphertext,
        byte[] bind,
        ReadOnlySpan<byte> callerAad)
    {
        var total = bind.Length + callerAad.Length;
        if (total <= StackBindBudget)
        {
            Span<byte> buf = stackalloc byte[StackBindBudget];
            buf = buf[..total];
            bind.CopyTo(buf);
            callerAad.CopyTo(buf[bind.Length..]);
            return provider.Decrypt(ciphertext, buf);
        }

        var rented = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            bind.CopyTo(rented);
            callerAad.CopyTo(rented.AsSpan(bind.Length));
            return provider.Decrypt(ciphertext, rented.AsSpan(0, total));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private readonly record struct ReaderRegistration(IAuthenticatedEncryptionProvider Provider, byte[] TagBindBytes);
}
