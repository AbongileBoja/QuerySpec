using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace QuerySpec.Core.Security;

/// <summary>
/// Classifier driven by an explicit <c>(Type, fieldName) -&gt; PiiCategory</c> map supplied at
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

    /// <inheritdoc />
    public PiiCategory Classify(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.NonPublicProperties
            | DynamicallyAccessedMemberTypes.PublicFields
            | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type? declaringType,
        string fieldName)
    {
        if (declaringType is null) return PiiCategory.None;
        if (string.IsNullOrEmpty(fieldName)) return PiiCategory.None;

        return _map.TryGetValue((declaringType, fieldName), out var category)
            ? category
            : PiiCategory.None;
    }
}
