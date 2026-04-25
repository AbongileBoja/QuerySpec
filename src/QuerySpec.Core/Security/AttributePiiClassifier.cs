using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace QuerySpec.Core.Security;

/// <summary>
/// Classifier driven by <see cref="PiiAttribute"/> on properties and fields.
/// Reflection results are cached per <c>(Type, fieldName)</c> for the life of the classifier.
/// </summary>
/// <remarks>
/// This classifier ignores the value entirely — classification is a property of the schema,
/// not the data. A column declared <c>[Pii(PiiCategory.DirectIdentifier)]</c> is always PII,
/// whether the row carries <c>"123-45-6789"</c> or <c>null</c>. Conversely, a column without
/// the attribute is not PII even if its value happens to look like an SSN.
/// </remarks>
public sealed class AttributePiiClassifier : IPiiClassifier
{
    private readonly ConcurrentDictionary<(Type Type, string Name), PiiCategory> _cache = new();

    /// <inheritdoc />
    public PiiCategory Classify(Type? declaringType, string fieldName)
    {
        if (declaringType is null) return PiiCategory.None;
        if (string.IsNullOrEmpty(fieldName)) return PiiCategory.None;

        return _cache.GetOrAdd((declaringType, fieldName), static key => Resolve(key.Type, key.Name));
    }

    private static PiiCategory Resolve(Type type, string name)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        var prop = type.GetProperty(name, Flags);
        var attr = prop?.GetCustomAttribute<PiiAttribute>(inherit: true);
        if (attr is not null) return attr.Category;

        var field = type.GetField(name, Flags);
        attr = field?.GetCustomAttribute<PiiAttribute>(inherit: true);
        return attr?.Category ?? PiiCategory.None;
    }
}
