using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Combines multiple <see cref="IPiiClassifier"/> instances. The first classifier to return
/// a non-<see cref="PiiCategory.None"/> result wins; later classifiers are not consulted.
/// Use to layer attribute-based classification over an explicit configuration map.
/// </summary>
public sealed class CompositePiiClassifier : IPiiClassifier
{
    private readonly IPiiClassifier[] _classifiers;

    /// <summary>Initializes the composite with the supplied classifiers in priority order.</summary>
    /// <param name="classifiers">Classifiers consulted left to right. Must not be null or contain nulls.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="classifiers"/> is null or any element is null.</exception>
    public CompositePiiClassifier(params IPiiClassifier[] classifiers)
    {
        ArgumentNullException.ThrowIfNull(classifiers);
        foreach (var c in classifiers)
            ArgumentNullException.ThrowIfNull(c, nameof(classifiers));
        _classifiers = (IPiiClassifier[])classifiers.Clone();
    }

    /// <summary>Initializes the composite from an enumerable of classifiers.</summary>
    /// <param name="classifiers">Classifiers consulted in iteration order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="classifiers"/> is null or any element is null.</exception>
    public CompositePiiClassifier(IEnumerable<IPiiClassifier> classifiers)
    {
        ArgumentNullException.ThrowIfNull(classifiers);
        var list = new List<IPiiClassifier>();
        foreach (var c in classifiers)
        {
            ArgumentNullException.ThrowIfNull(c, nameof(classifiers));
            list.Add(c);
        }
        _classifiers = list.ToArray();
    }

    /// <inheritdoc />
    public PiiCategory Classify(Type? declaringType, string fieldName)
    {
        foreach (var c in _classifiers)
        {
            var result = c.Classify(declaringType, fieldName);
            if (result != PiiCategory.None) return result;
        }
        return PiiCategory.None;
    }
}
