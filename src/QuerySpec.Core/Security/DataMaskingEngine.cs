using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// To separate masks across tenants, pass a non-empty <c>tenantId</c> to <see cref="Mask(string, object?, string?)"/>.
/// The engine derives a tenant-scoped key via HMAC-SHA256 over the configured hash secret and
/// the tenantId, then uses that derived key to HMAC the value. Two tenants masking the same
/// value get different outputs.
/// </para>
/// <para>
/// PII detection is delegated to <see cref="IPiiClassifier"/>. Inject an
/// <see cref="AttributePiiClassifier"/>, <see cref="ConfiguredPiiClassifier"/>, or composite
/// to drive masking decisions from explicit metadata rather than name guesses.
/// </para>
/// </remarks>
public sealed class DataMaskingEngine
{
    private const int HashOutputBytes = 32;

    private const DynamicallyAccessedMemberTypes ClassifierMembers =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields;

    private readonly Dictionary<string, MaskingStrategy> _fieldMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Regex> _piiPatterns = new(StringComparer.Ordinal);
    private readonly byte[]? _hashKey;
    private readonly IPiiClassifier? _classifier;

    /// <summary>
    /// Initializes a new instance of <see cref="DataMaskingEngine"/> without a hash key.
    /// Registering <see cref="MaskingStrategy.HashMask"/> on this instance throws.
    /// </summary>
    public DataMaskingEngine() : this(hashKey: null, classifier: null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DataMaskingEngine"/> with the given hash secret.
    /// </summary>
    /// <param name="hashKey">
    /// Secret key used as the seed for HMAC-based <see cref="MaskingStrategy.HashMask"/>.
    /// Must be at least 16 bytes when supplied. Pass <c>null</c> only if no field will use
    /// <see cref="MaskingStrategy.HashMask"/>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hashKey"/> is non-null but shorter than 16 bytes.</exception>
    public DataMaskingEngine(byte[]? hashKey) : this(hashKey, classifier: null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="DataMaskingEngine"/> with the given hash
    /// secret and classifier.
    /// </summary>
    /// <param name="hashKey">
    /// Secret key used as the seed for HMAC-based <see cref="MaskingStrategy.HashMask"/>.
    /// Must be at least 16 bytes when supplied. Pass <c>null</c> only if no field will use
    /// <see cref="MaskingStrategy.HashMask"/>.
    /// </param>
    /// <param name="classifier">
    /// Classifier consulted by <see cref="IsPii(System.Type?, string)"/>. Pass <c>null</c> to
    /// opt out of structured classification; the engine then defaults to <c>None</c> for
    /// every field. The legacy name-and-regex heuristic is reachable only via the obsolete
    /// <see cref="IsPii(string, object?)"/> overload.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hashKey"/> is non-null but shorter than 16 bytes.</exception>
    public DataMaskingEngine(byte[]? hashKey, IPiiClassifier? classifier)
    {
        if (hashKey is not null && hashKey.Length < 16)
            throw new ArgumentException("hashKey must be at least 16 bytes when supplied.", nameof(hashKey));

        _hashKey = hashKey is null ? null : (byte[])hashKey.Clone();
        _classifier = classifier;
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
    /// Returns the original string when no strategy is registered — this overload has no type
    /// context and so cannot consult the configured classifier. Prefer
    /// <see cref="Mask(System.Type?, string, object?, string?)"/> when calling from a code path
    /// that knows the entity type.
    /// </summary>
    /// <param name="fieldName">Field name registered via <see cref="RegisterFieldMask"/>.</param>
    /// <param name="value">Value to mask. Null produces the literal string <c>"null"</c>.</param>
    /// <param name="tenantId">
    /// Optional tenant scope. When supplied, <see cref="MaskingStrategy.HashMask"/> derives a
    /// tenant-specific key so the same value masks differently across tenants.
    /// </param>
    /// <returns>The masked value, or the original string representation if no strategy is registered.</returns>
    public string Mask(string fieldName, object? value, string? tenantId = null)
        => MaskCore(fieldName, value, tenantId, classifierCategory: PiiCategory.None);

    /// <summary>
    /// Masks a field value with full classifier consultation. When no explicit field mask is
    /// registered, the configured <see cref="IPiiClassifier"/> is consulted via
    /// <paramref name="declaringType"/> and a category-default strategy is applied for any
    /// non-<see cref="PiiCategory.None"/> result. This closes the path where a caller annotates
    /// a property with <c>[Pii]</c> but forgets to <see cref="RegisterFieldMask"/> — the engine
    /// would otherwise return plaintext.
    /// </summary>
    /// <param name="declaringType">The type that owns <paramref name="fieldName"/>. Used to consult the classifier.</param>
    /// <param name="fieldName">Field name. Explicit registrations take precedence over classifier defaults.</param>
    /// <param name="value">Value to mask. Null produces the literal string <c>"null"</c>.</param>
    /// <param name="tenantId">Optional tenant scope for <see cref="MaskingStrategy.HashMask"/>.</param>
    /// <returns>
    /// The masked value. When no field mask is registered and the classifier returns
    /// <see cref="PiiCategory.None"/>, the original string representation is returned.
    /// </returns>
    public string Mask(
        [DynamicallyAccessedMembers(ClassifierMembers)] Type? declaringType,
        string fieldName,
        object? value,
        string? tenantId = null)
        => MaskCore(fieldName, value, tenantId, Classify(declaringType, fieldName));

    private string MaskCore(string fieldName, object? value, string? tenantId, PiiCategory classifierCategory)
    {
        if (value is null) return "null";

        var strValue = value.ToString() ?? string.Empty;

        if (_fieldMasks.TryGetValue(fieldName, out var strategy))
            return ApplyStrategy(strategy, strValue, tenantId);

        if (classifierCategory == PiiCategory.None)
            return strValue;

        return ApplyStrategy(DefaultStrategyFor(classifierCategory), strValue, tenantId);
    }

    private string ApplyStrategy(MaskingStrategy strategy, string value, string? tenantId) => strategy switch
    {
        MaskingStrategy.FullMask => MaskFull(value),
        MaskingStrategy.PartialMask => MaskPartial(value),
        MaskingStrategy.LastFourOnly => MaskLastFour(value),
        MaskingStrategy.EmailMask => MaskEmail(value),
        MaskingStrategy.HashMask => MaskHash(value, tenantId),
        _ => value
    };

    /// <summary>
    /// Default mask strategy for each <see cref="PiiCategory"/>. Used when the classifier
    /// reports a field as PII and no explicit <see cref="RegisterFieldMask"/> is in place.
    /// Errs on the side of <see cref="MaskingStrategy.FullMask"/> for unknown categories.
    /// </summary>
    private static MaskingStrategy DefaultStrategyFor(PiiCategory category) => category switch
    {
        PiiCategory.Contact => MaskingStrategy.EmailMask,
        PiiCategory.Financial => MaskingStrategy.LastFourOnly,
        _ => MaskingStrategy.FullMask
    };

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
    /// Returns the PII category for the named field, as declared by the configured
    /// <see cref="IPiiClassifier"/>. Returns <see cref="PiiCategory.None"/> when no classifier
    /// is configured.
    /// </summary>
    /// <param name="declaringType">
    /// The type that owns the field. Pass <c>null</c> only when no type context is available;
    /// type-scoped classifiers will return <see cref="PiiCategory.None"/> in that case.
    /// </param>
    /// <param name="fieldName">The field or property name to classify.</param>
    /// <returns>The category, or <see cref="PiiCategory.None"/> when no classifier is configured.</returns>
    public PiiCategory Classify(
        [DynamicallyAccessedMembers(ClassifierMembers)] Type? declaringType,
        string fieldName)
        => _classifier?.Classify(declaringType, fieldName) ?? PiiCategory.None;

    /// <summary>
    /// Returns true when the configured <see cref="IPiiClassifier"/> assigns a non-<c>None</c>
    /// category to the named field. Decisions are based on schema metadata, not on the value.
    /// </summary>
    /// <param name="declaringType">The type that owns the field.</param>
    /// <param name="fieldName">The field or property name to classify.</param>
    /// <returns><c>true</c> when classified as PII; otherwise <c>false</c>.</returns>
    public bool IsPii(
        [DynamicallyAccessedMembers(ClassifierMembers)] Type? declaringType,
        string fieldName)
        => Classify(declaringType, fieldName) != PiiCategory.None;

    /// <summary>
    /// Detects if a field name and value match a registered PII pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This heuristic combines a field-name substring match with a regex on the value.
    /// It produces false positives (a column named <c>EmailRegistrationToken</c> classified
    /// as Email) and — more dangerously — false negatives (a column named <c>Notes</c>
    /// carrying an SSN-shaped value, which the heuristic misses). Use
    /// <see cref="IsPii(System.Type?, string)"/> with an explicit <see cref="IPiiClassifier"/>.
    /// </para>
    /// <para>
    /// Promoted to a build error in <c>2.0.1</c>; scheduled for removal in <c>3.0.0</c>.
    /// </para>
    /// </remarks>
    [Obsolete("Field-name + value-regex heuristics produce false negatives that leak PII. " +
              "Annotate fields with [Pii(...)] and use IsPii(Type, string) backed by IPiiClassifier instead. " +
              "Promoted to a build error in 2.0.1; scheduled for removal in 3.0.0.",
              error: true)]
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
