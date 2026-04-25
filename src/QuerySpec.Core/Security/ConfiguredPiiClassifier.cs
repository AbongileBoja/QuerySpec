using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Classifier driven by an explicit <c>(Type, fieldName) -> PiiCategory</c> map supplied at
/// construction time. Use when annotation is impractical — third-party DTOs, generated
/// entities, or runtime-shaped data.
/// </summary>
/// <remarks>
/// Field names are matched ordinally and case-sensitively. Two registrations on the same
/// <c>(Type, fieldName)</c> key throw at construction; ambiguous configuration must be
/// resolved by the caller, not silently overwritten.
/// </remarks>
public sealed class ConfiguredPiiClassifier : IPiiClassifier
{
    private readonly Dictionary<(Type Type, string Name), PiiCategory> _map;
    private readonly Dictionary<string, PiiCategory>? _typeAgnostic;

    /// <summary>
    /// Initializes the classifier with explicit type-scoped registrations.
    /// </summary>
    /// <param name="registrations">
    /// Sequence of <c>(declaringType, fieldName, category)</c> tuples. Empty is allowed and
    /// produces a classifier that returns <see cref="PiiCategory.None"/> for everything.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registrations"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown on duplicate <c>(Type, fieldName)</c> registrations.</exception>
    public ConfiguredPiiClassifier(IEnumerable<(Type DeclaringType, string FieldName, PiiCategory Category)> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _map = new Dictionary<(Type, string), PiiCategory>();
        foreach (var (type, name, category) in registrations)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (!_map.TryAdd((type, name), category))
                throw new ArgumentException($"Duplicate PII classification for {type.FullName}.{name}.", nameof(registrations));
        }
    }

    /// <summary>
    /// Initializes the classifier with type-agnostic field-name registrations. Use sparingly:
    /// this loses the schema-level guarantee that the same field name on a different type may
    /// not be PII. Prefer the typed overload.
    /// </summary>
    /// <param name="byFieldName">
    /// Map of field name to category. Keys are matched ordinally and case-sensitively against
    /// the <c>fieldName</c> argument of <see cref="Classify"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="byFieldName"/> is null.</exception>
    public ConfiguredPiiClassifier(IReadOnlyDictionary<string, PiiCategory> byFieldName)
    {
        ArgumentNullException.ThrowIfNull(byFieldName);
        _map = new Dictionary<(Type, string), PiiCategory>();
        _typeAgnostic = new Dictionary<string, PiiCategory>(StringComparer.Ordinal);
        foreach (var kv in byFieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kv.Key);
            _typeAgnostic[kv.Key] = kv.Value;
        }
    }

    /// <inheritdoc />
    public PiiCategory Classify(Type? declaringType, string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return PiiCategory.None;

        if (declaringType is not null && _map.TryGetValue((declaringType, fieldName), out var typed))
            return typed;

        if (_typeAgnostic is not null && _typeAgnostic.TryGetValue(fieldName, out var agnostic))
            return agnostic;

        return PiiCategory.None;
    }
}
