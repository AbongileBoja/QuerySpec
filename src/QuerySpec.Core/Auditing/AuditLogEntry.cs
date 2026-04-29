using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Audit log entry for compliance and forensics.
/// </summary>
/// <remarks>
/// All non-integrity fields are <c>init</c>-only and must be supplied at construction.
/// Integrity fields (<see cref="Hash"/>, <see cref="PreviousHash"/>) are set exactly
/// once by <see cref="Seal"/>, which an <see cref="IAuditLogger"/> implementation calls
/// under its write lock with the previous entry's hash. After sealing, the entry is
/// effectively immutable; subsequent calls to <see cref="Seal"/> throw.
/// </remarks>
public class AuditLogEntry
{
    /// <summary>Unique identifier for the audit entry.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    /// <summary>Timestamp when the operation occurred.</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;
    /// <summary>User identifier.</summary>
    public string UserId { get; init; } = string.Empty;
    /// <summary>Request identifier.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Type of resource being accessed.</summary>
    public string ResourceType { get; init; } = string.Empty;
    /// <summary>Operation performed (Query, Insert, Update, Delete).</summary>
    public string Operation { get; init; } = string.Empty;
    /// <summary>Filters applied to the query.</summary>
    public Dictionary<string, object> Filters { get; init; } = new();
    /// <summary>Fields projected in the query.</summary>
    public List<string> ProjectedFields { get; init; } = new();

    /// <summary>Execution time in milliseconds.</summary>
    public long ExecutionTimeMs { get; init; }
    /// <summary>Number of records affected.</summary>
    public int RecordsAffected { get; init; }
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Error message if operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether data was encrypted.</summary>
    public bool WasEncrypted { get; init; }
    /// <summary>Whether data was masked.</summary>
    public bool WasMasked { get; init; }
    /// <summary>List of sensitive fields accessed.</summary>
    public List<string> AccessedSensitiveFields { get; init; } = new();
    /// <summary>List of fields access was denied for.</summary>
    public List<string> DeniedFields { get; init; } = new();

    /// <summary>SHA-256 hash for integrity verification. Set once by <see cref="Seal"/>.</summary>
    public string Hash { get; private set; } = string.Empty;
    /// <summary>Hash of the previous entry in the chain. Set once by <see cref="Seal"/>; <c>null</c> for the first entry.</summary>
    public string? PreviousHash { get; private set; }

    private bool _sealed;

    /// <summary>Validates that required fields are populated.</summary>
    /// <exception cref="ArgumentException">Thrown when TenantId, UserId, or Operation is null or whitespace.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
            throw new ArgumentException("TenantId cannot be empty", nameof(TenantId));

        if (string.IsNullOrWhiteSpace(UserId))
            throw new ArgumentException("UserId cannot be empty", nameof(UserId));

        if (string.IsNullOrWhiteSpace(Operation))
            throw new ArgumentException("Operation cannot be empty", nameof(Operation));
    }

    /// <summary>
    /// Seals this entry into the audit chain by setting <see cref="PreviousHash"/> to
    /// <paramref name="previousHash"/> and computing <see cref="Hash"/>. The entry must
    /// not have been sealed previously. <see cref="IAuditLogger"/> implementations call
    /// this under their write lock with the most recent entry's hash.
    /// </summary>
    /// <param name="previousHash">Hash of the previous entry in the chain, or <c>null</c> if this is the first entry.</param>
    /// <exception cref="InvalidOperationException">Thrown when the entry has already been sealed.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Seal(string? previousHash)
    {
        if (_sealed)
            throw new InvalidOperationException("AuditLogEntry has already been sealed and cannot be re-sealed.");

        Validate();
        PreviousHash = previousHash;
        Hash = ComputeHashCore(this, previousHash);
        _sealed = true;
    }

    /// <summary>
    /// Computes the integrity hash. Equivalent to <c>Seal(PreviousHash)</c>; preserved
    /// for source compatibility with pre-2.0 callers.
    /// </summary>
    [Obsolete("Use Seal(previousHash) instead. ComputeHash does not establish chain linkage when called directly.")]
    public void ComputeHash() => Seal(PreviousHash);

    /// <summary>
    /// Verifies that this entry's <see cref="Hash"/> matches a freshly-computed hash for the
    /// supplied <paramref name="expectedPreviousHash"/>. Comparison is fixed-time. Does not
    /// mutate the entry.
    /// </summary>
    /// <param name="expectedPreviousHash">The previous-entry hash this entry was sealed against, per the caller's chain reconstruction.</param>
    /// <returns><c>true</c> when the hashes match; <c>false</c> otherwise (including unsealed entries).</returns>
    public bool VerifyIntegrity(string? expectedPreviousHash)
    {
        if (!_sealed || string.IsNullOrEmpty(Hash)) return false;

        var expected = ComputeHashCore(this, expectedPreviousHash);
        var expectedBytes = Convert.FromBase64String(expected);
        var actualBytes = Convert.FromBase64String(Hash);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Verifies a chain of audit entries: each entry's <see cref="PreviousHash"/> must equal
    /// the prior entry's <see cref="Hash"/>, and each entry's <see cref="Hash"/> must
    /// recompute to its current value. Returns the index of the first broken link, or -1 if the chain is intact.
    /// </summary>
    /// <param name="entries">Chronologically-ordered audit entries.</param>
    /// <returns>The zero-based index of the first broken entry; <c>-1</c> if the entire chain is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    public static int VerifyChain(IReadOnlyList<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        for (var i = 0; i < entries.Count; i++)
        {
            var expectedPrevious = i == 0 ? null : entries[i - 1].Hash;
            if (!ChainLinkMatches(entries[i].PreviousHash, expectedPrevious))
                return i;
            if (!entries[i].VerifyIntegrity(expectedPrevious))
                return i;
        }
        return -1;
    }

    private static bool ChainLinkMatches(string? actual, string? expected)
    {
        if (actual is null && expected is null) return true;
        if (actual is null || expected is null) return false;

        Span<byte> actualBuf = stackalloc byte[64];
        Span<byte> expectedBuf = stackalloc byte[64];
        if (!Convert.TryFromBase64String(actual, actualBuf, out var actualLen)) return false;
        if (!Convert.TryFromBase64String(expected, expectedBuf, out var expectedLen)) return false;
        if (actualLen != expectedLen) return false;

        return CryptographicOperations.FixedTimeEquals(actualBuf[..actualLen], expectedBuf[..expectedLen]);
    }

    private static string ComputeHashCore(AuditLogEntry e, string? previousHash)
    {
        var data = $"{e.Timestamp:O}|{e.TenantId}|{e.UserId}|{e.Operation}|{e.RecordsAffected}|{e.Success}|{previousHash ?? string.Empty}";
        Span<byte> hash = stackalloc byte[32];
        var written = SHA256.HashData(Encoding.UTF8.GetBytes(data), hash);
        if (written != 32)
            throw new CryptographicException("Unexpected SHA-256 output length.");
        return Convert.ToBase64String(hash);
    }
}
