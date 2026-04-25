using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace QuerySpec.Core.Security;

/// <summary>
/// Classifier driven by <see cref="PiiAttribute"/> on properties and fields.
/// Reflection results are cached per <c>(Type, fieldName)</c> for the life of the classifier.
/// </summary>
/// <remarks>
/// <para>
/// This classifier ignores the value entirely — classification is a property of the schema,
/// not the data. A column declared <c>[Pii(PiiCategory.DirectIdentifier)]</c> is always PII,
/// whether the row carries <c>"123-45-6789"</c> or <c>null</c>. Conversely, a column without
/// the attribute is not PII even if its value happens to look like an SSN.
/// </para>
/// <para>
/// <strong>Trimming and AOT:</strong> reflection over a caller-supplied <see cref="Type"/>
/// cannot be statically reasoned about, so under <c>PublishTrimmed</c> or AOT a property or
/// field carrying <c>[Pii]</c> may be removed and this classifier would silently return
/// <see cref="PiiCategory.None"/> — re-introducing the leak. Consumers running trimmed
/// publishes should use <see cref="ConfiguredPiiClassifier"/> instead, or root every entity
/// type via <c>DynamicDependency</c> / a trim-roots descriptor. The class is annotated with
/// <see cref="RequiresUnreferencedCodeAttribute"/> so the trim analyzer surfaces this at
/// build time.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("AttributePiiClassifier reflects over caller-supplied entity types. Under trimming, [Pii]-annotated members may be removed and the classifier will silently return None. Use ConfiguredPiiClassifier in trimmed/AOT scenarios.")]
public sealed class AttributePiiClassifier : IPiiClassifier
{
    private const DynamicallyAccessedMemberTypes RequiredMembers =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields;

    private readonly ConcurrentDictionary<(Type Type, string Name), PiiCategory> _cache = new();

    /// <inheritdoc />
    public PiiCategory Classify(
        [DynamicallyAccessedMembers(RequiredMembers)] Type? declaringType,
        string fieldName)
    {
        if (declaringType is null) return PiiCategory.None;
        if (string.IsNullOrEmpty(fieldName)) return PiiCategory.None;

        return _cache.GetOrAdd((declaringType, fieldName), key => Resolve(key.Type, key.Name));
    }

    private static PiiCategory Resolve(
        [DynamicallyAccessedMembers(RequiredMembers)] Type type,
        string name)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var prop = type.GetProperty(name, Flags);
        var attr = prop?.GetCustomAttribute<PiiAttribute>(inherit: true);
        if (attr is not null) return attr.Category;

        var field = type.GetField(name, Flags);
        attr = field?.GetCustomAttribute<PiiAttribute>(inherit: true);
        return attr?.Category ?? PiiCategory.None;
    }
}
