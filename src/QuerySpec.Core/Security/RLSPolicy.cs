using System;
using System.Linq.Expressions;

namespace QuerySpec.Core.Security;

/// <summary>
/// Row-level security policy definition. A policy declares either a parameterized SQL fragment
/// (<see cref="FilterGenerator"/>) or a strongly-typed predicate (<see cref="PredicateFactory"/>)
/// — or both — for a given <see cref="ResourceType"/>.
/// </summary>
public class RLSPolicy
{
    /// <summary>Resource type this policy applies to.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Generates a parameterized SQL fragment for the policy. Defaults to a fail-closed
    /// generator that returns <see cref="RLSFilter.DenyAll"/> so that a policy registered with
    /// only a <see cref="PredicateFactory"/> (or with no SQL configured at all) does not
    /// silently authorise unrestricted access on the SQL path. Callers that legitimately want
    /// allow-all behaviour for a resource should call
    /// <see cref="RowLevelSecurityEngine.RegisterUnrestricted{T}(string)"/>, which is explicit
    /// and surfaces in code review.
    /// </summary>
    public Func<RLSContext, RLSFilter> FilterGenerator { get; set; } = _ => RLSFilter.DenyAll;

    /// <summary>
    /// Strongly-typed predicate factory for use with IQueryable. Prefer <see cref="SetPredicate{T}"/>
    /// over assigning to this property directly.
    /// </summary>
    public Delegate? PredicateFactory { get; set; }

    /// <summary>
    /// Sets <see cref="PredicateFactory"/> to a strongly-typed factory bound to entity type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type the predicate applies to.</typeparam>
    /// <param name="factory">Factory that produces a predicate expression from an <see cref="RLSContext"/>.</param>
    /// <returns>The current policy, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public RLSPolicy SetPredicate<T>(Func<RLSContext, Expression<Func<T, bool>>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        PredicateFactory = factory;
        return this;
    }

    /// <summary>Whether to apply this policy hierarchically to related entities.</summary>
    public bool ApplyHierarchically { get; set; }
}
