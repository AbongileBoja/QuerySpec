using System;

namespace QuerySpec.Core.Security;

/// <summary>
/// Marks a property or field as personally identifiable, with an explicit
/// <see cref="PiiCategory"/>. Read by <see cref="AttributePiiClassifier"/> at runtime.
/// </summary>
/// <remarks>
/// Annotation is the source of truth for PII classification. Field-name heuristics produce
/// false negatives that silently leak PII (a column named <c>Notes</c> carrying an SSN) and
/// false positives that mask innocuous columns (an <c>EmailRegistrationToken</c> column).
/// Use this attribute to make classification a first-class compile-time concern.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PiiAttribute : Attribute
{
    /// <summary>Initializes the attribute with the given category.</summary>
    /// <param name="category">The PII category for the annotated member. Must not be <see cref="PiiCategory.None"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="category"/> is <see cref="PiiCategory.None"/> or not a defined enum value.</exception>
    public PiiAttribute(PiiCategory category)
    {
        if (category == PiiCategory.None)
            throw new ArgumentOutOfRangeException(nameof(category), "[Pii(PiiCategory.None)] is meaningless. Omit the attribute instead.");
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown PiiCategory value.");

        Category = category;
    }

    /// <summary>The PII category assigned to the annotated member.</summary>
    public PiiCategory Category { get; }
}
