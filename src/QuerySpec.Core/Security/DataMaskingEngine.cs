using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Security;

/// <summary>
/// PII (Personally Identifiable Information) data masking engine.
/// Supports multiple masking strategies for different data types.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MaskingStrategy.HashMask"/> requires a per-instance keyed hash secret to be
/// supplied to the constructor. Without it, registering <see cref="MaskingStrategy.HashMask"/>
/// throws and the engine falls back to a deny path. This is intentional: an unkeyed hash on
/// low-cardinality PII (SSN, phone, email) is brute-forceable in seconds and produces
/// cross-tenant correlation oracles.
/// </para>
/// <para>
/// To separate masks across tenants, pass a non-empty <c>tenantId</c> to <see cref="Mask"/>.
/// The engine derives a tenant-scoped key via HMAC-SHA256 over the configured hash secret and
/// the tenantId, then uses that derived key to HMAC the value. Two tenants masking the same
/// value get different outputs.
/// </para>
/// </remarks>
public class DataMaskingEngine
{
    private const int HashOutputBytes = 32;

    private readonly Dictionary<string, MaskingStrategy> _fieldMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Regex> _piiPatterns = new(StringComparer.Ordinal);
    private readonly byte[]? _hashKey;

    /// <summary>
    /// Initializes a new instance of <see cref="DataMaskingEngine"/> without a hash key.
    /// Registering <see cref="MaskingStrategy.HashMask"/> on this instance throws.
    /// </summary>
    public DataMaskingEngine() : this(hashKey: null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DataMaskingEngine"/> with the given hash secret.
    /// </summary>
    /// <param name="hashKey">
    /// Secret key used as the seed for HMAC-based <see cref="MaskingStrategy.HashMask"/>.
    /// Must be at least 16 bytes when supplied. Pass <c>null</c> only if no field will use
    /// <see cref="MaskingStrategy.HashMask"/>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hashKey"/> is non-null but shorter than 16 bytes.</exception>
    public DataMaskingEngine(byte[]? hashKey)
    {
        if (hashKey is not null && hashKey.Length < 16)
            throw new ArgumentException("hashKey must be at least 16 bytes when supplied.", nameof(hashKey));

        _hashKey = hashKey is null ? null : (byte[])hashKey.Clone();
        InitializeDefaultPiiPatterns();
    }

    private void InitializeDefaultPiiPatterns()
    {
        _piiPatterns["Email"] = new Regex(@"^[^\@]+@[^\@]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        _piiPatterns["SSN"] = new Regex(@"^\d{3}-\d{2}-\d{4}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        _piiPatterns["Phone"] = new Regex(@"^\+?1?\d{9,15}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        _piiPatterns["CreditCard"] = new Regex(@"^\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Registers a masking strategy for the named field.
    /// </summary>
    /// <param name="fieldName">The field name to register a mask for. Must not be null or whitespace.</param>
    /// <param name="strategy">The masking strategy to apply.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fieldName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="strategy"/> is <see cref="MaskingStrategy.HashMask"/> but the engine was constructed without a hash key.
    /// </exception>
    public void RegisterFieldMask(string fieldName, MaskingStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (strategy == MaskingStrategy.HashMask && _hashKey is null)
        {
            throw new InvalidOperationException(
                $"Cannot register field '{fieldName}' for HashMask: this DataMaskingEngine was constructed without a hash key. " +
                "Pass a per-application secret (>= 16 bytes) to the DataMaskingEngine(byte[]) constructor.");
        }

        _fieldMasks[fieldName] = strategy;
    }

    /// <summary>
    /// Masks a field value using the strategy registered for <paramref name="fieldName"/>.
    /// </summary>
    /// <param name="fieldName">Field name registered via <see cref="RegisterFieldMask"/>.</param>
    /// <param name="value">Value to mask. Null produces the literal string <c>"null"</c>.</param>
    /// <param name="tenantId">
    /// Optional tenant scope. When supplied, <see cref="MaskingStrategy.HashMask"/> derives a
    /// tenant-specific key so the same value masks differently across tenants.
    /// </param>
    /// <returns>The masked value, or the original string representation if no strategy is registered.</returns>
    public string Mask(string fieldName, object? value, string? tenantId = null)
    {
        if (value is null) return "null";

        var strValue = value.ToString() ?? string.Empty;

        if (!_fieldMasks.TryGetValue(fieldName, out var strategy))
            return strValue;

        return strategy switch
        {
            MaskingStrategy.FullMask => MaskFull(strValue),
            MaskingStrategy.PartialMask => MaskPartial(strValue),
            MaskingStrategy.LastFourOnly => MaskLastFour(strValue),
            MaskingStrategy.EmailMask => MaskEmail(strValue),
            MaskingStrategy.HashMask => MaskHash(strValue, tenantId),
            _ => strValue
        };
    }

    private static string MaskFull(string value) => new('*', value.Length);
    private static string MaskPartial(string value) => value.Length <= 2 ? MaskFull(value) : value[..2] + new string('*', value.Length - 2);
    private static string MaskLastFour(string value) => value.Length <= 4 ? MaskFull(value) : new string('*', value.Length - 4) + value[^4..];

    private static string MaskEmail(string value)
    {
        var parts = value.Split('@');
        if (parts.Length != 2) return MaskFull(value);
        return parts[0][..Math.Min(1, parts[0].Length)] + new string('*', Math.Max(0, parts[0].Length - 1)) + "@" + parts[1];
    }

    private string MaskHash(string value, string? tenantId)
    {
        if (_hashKey is null)
            throw new InvalidOperationException("HashMask requires a hash key configured on the DataMaskingEngine constructor.");

        var key = string.IsNullOrEmpty(tenantId)
            ? _hashKey
            : DeriveTenantKey(_hashKey, tenantId);

        Span<byte> hash = stackalloc byte[HashOutputBytes];
        using var hmac = new HMACSHA256(key);
        var written = hmac.TryComputeHash(Encoding.UTF8.GetBytes(value), hash, out var bytesWritten);
        if (!written || bytesWritten != HashOutputBytes)
            throw new CryptographicException("Unexpected HMAC output length.");

        return Convert.ToBase64String(hash);
    }

    private static byte[] DeriveTenantKey(byte[] rootKey, string tenantId)
    {
        using var hmac = new HMACSHA256(rootKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(tenantId));
    }

    /// <summary>
    /// Detects if a field name and value match a registered PII pattern.
    /// </summary>
    /// <remarks>
    /// This heuristic combines a field-name substring match with a regex on the value.
    /// It is convenient for ad-hoc scanning but produces false positives (e.g. a column
    /// named "EmailRegistrationToken" classified as Email) and false negatives (e.g. a
    /// column named "Notes" carrying a SSN-shaped value). Treat the result as advisory,
    /// not authoritative.
    /// </remarks>
    public bool IsPii(string fieldName, object? value)
    {
        if (value is null) return false;
        if (string.IsNullOrEmpty(fieldName)) return false;

        var strValue = value.ToString() ?? string.Empty;

        foreach (var (pattern, regex) in _piiPatterns)
        {
            if (fieldName.Contains(pattern, StringComparison.OrdinalIgnoreCase) && regex.IsMatch(strValue))
                return true;
        }

        return false;
    }
}
