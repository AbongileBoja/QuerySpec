using System.Diagnostics.CodeAnalysis;

namespace QuerySpec.Core.Security;

/// <summary>
/// Classifies fields as personally identifiable. Implementations MUST be deterministic
/// for a given (declaringType, fieldName) pair and SHOULD NOT inspect values — value-based
/// classification is unreliable and turns masking into a heuristic again.
/// </summary>
public interface IPiiClassifier
{
    /// <summary>
    /// Returns the PII category for the named field on the given declaring type.
    /// </summary>
    /// <param name="declaringType">
    /// The type that owns the field. Pass <see langword="null"/> when the field is being
    /// classified outside a type context (rare; prefer the typed overload).
    /// Annotated with <see cref="DynamicallyAccessedMembersAttribute"/> so reflection-driven
    /// implementations (notably <see cref="AttributePiiClassifier"/>) preserve the
    /// public/non-public properties and fields they inspect under trimming.
    /// </param>
    /// <param name="fieldName">The field or property name to classify.</param>
    /// <returns>The category. <see cref="PiiCategory.None"/> means "not PII".</returns>
    PiiCategory Classify(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.NonPublicProperties
            | DynamicallyAccessedMemberTypes.PublicFields
            | DynamicallyAccessedMemberTypes.NonPublicFields)]
        System.Type? declaringType,
        string fieldName);
}
