using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Type-agnostic classifier that maps a field name to a <see cref="PiiCategory"/> regardless
/// of which type owns the field. <strong>This re-introduces the field-name heuristic mode</strong>
/// that motivated the move to explicit classification — it should be the last layer in a
/// composite, not the first.
/// </summary>
/// <remarks>
/// <para>
/// Suitable only as a backstop behind <see cref="AttributePiiClassifier"/> or
/// <see cref="ConfiguredPiiClassifier"/>. The same field name on a different type may not be
/// PII, and this classifier cannot tell — wrap it in a <see cref="CompositePiiClassifier"/>
/// with the typed classifiers in front so the schema-aware decision wins.
/// </para>
/// <para>
/// Names are matched ordinally and case-sensitively to keep the comparison deterministic
/// across platforms.
/// </para>
/// </remarks>
public sealed class NameOnlyPiiClassifier : IPiiClassifier
{
    private readonly Dictionary<string, PiiCategory> _byFieldName;

    /// <summary>
    /// Initializes the classifier from a name-only map.
    /// </summary>
    /// <param name="byFieldName">
    /// Map of field name to category. Keys are matched ordinally and case-sensitively against
    /// the <c>fieldName</c> argument of <see cref="Classify"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="byFieldName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any key is null or whitespace.</exception>
    public NameOnlyPiiClassifier(IReadOnlyDictionary<string, PiiCategory> byFieldName)
    {
        ArgumentNullException.ThrowIfNull(byFieldName);
        _byFieldName = new Dictionary<string, PiiCategory>(StringComparer.Ordinal);
        foreach (var kv in byFieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kv.Key);
            _byFieldName[kv.Key] = kv.Value;
        }
    }

    /// <inheritdoc />
    public PiiCategory Classify(Type? declaringType, string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return PiiCategory.None;
        return _byFieldName.TryGetValue(fieldName, out var category) ? category : PiiCategory.None;
    }
}
