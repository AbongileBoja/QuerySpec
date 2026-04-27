using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Security;

/// <summary>
/// Behavior the <see cref="RowLevelSecurityEngine"/> applies when no policy is registered for a
/// requested resource type. The engine fails closed by default: a missing registration is treated
/// as a configuration bug and surfaces as an exception.
/// </summary>
public enum RLSDefaultBehavior
{
    /// <summary>
    /// Throw <see cref="InvalidOperationException"/> when no policy is registered for the resource
    /// type. This is the default and the most defensive option: a forgotten registration is
    /// surfaced loudly instead of silently denying or allowing access.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Return a deny-all filter (<see cref="RLSFilter.DenyAll"/>) or a
    /// constant-false predicate when no policy is registered. Fails closed without throwing.
    /// </summary>
    DenyAll = 1,

    /// <summary>
    /// Return an allow-all filter (<see cref="RLSFilter.AllowAll"/>) or a
    /// <c>null</c> predicate (signalling "no predicate to apply") when no policy is registered.
    /// This is an explicit opt-in for legitimate "this resource has no RLS" scenarios; it must be
    /// set deliberately on the constructor and is never the default.
    /// </summary>
    AllowAll = 2,
}

/// <summary>
/// Row-Level Security (RLS) engine for multi-tenant applications.
/// Provides policy-based access control at the data row level.
/// </summary>
/// <remarks>
/// <para>
/// The engine supports two policy styles:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="RLSPolicy.PredicateFactory"/> — strongly-typed LINQ expressions (recommended; composable with IQueryable and safe by construction).</description></item>
///   <item><description><see cref="RLSPolicy.FilterGenerator"/> — SQL fragment generator used when raw SQL is unavoidable. All identifiers and literals are validated/escaped to prevent injection.</description></item>
/// </list>
/// <para>
/// <b>Fail-closed by default.</b> When no policy is registered for a requested resource type the
/// engine throws <see cref="InvalidOperationException"/>. Configure an alternative behavior via the
/// <see cref="RowLevelSecurityEngine(RLSDefaultBehavior)"/> constructor, or register an explicit
/// "no restriction" entry with <see cref="RegisterUnrestricted{T}(string)"/> when a resource is
/// genuinely public.
/// </para>
/// </remarks>
public class RowLevelSecurityEngine
{
    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, RLSPolicy> _policies = new(StringComparer.Ordinal);
    private readonly RLSDefaultBehavior _defaultBehavior;

    /// <summary>
    /// Initializes a new <see cref="RowLevelSecurityEngine"/>.
    /// </summary>
    /// <param name="defaultBehavior">
    /// Behavior applied when no policy is registered for a requested resource type. Defaults to
    /// <see cref="RLSDefaultBehavior.Throw"/> so missing registrations are surfaced loudly. Pass
    /// <see cref="RLSDefaultBehavior.DenyAll"/> for silent fail-closed semantics, or
    /// <see cref="RLSDefaultBehavior.AllowAll"/> only when the calling code intentionally treats
    /// unregistered resources as unrestricted.
    /// </param>
    public RowLevelSecurityEngine(RLSDefaultBehavior defaultBehavior = RLSDefaultBehavior.Throw)
    {
        _defaultBehavior = defaultBehavior;
    }

    /// <summary>
    /// Registers an RLS policy. Replaces any previously registered policy for the same
    /// <see cref="RLSPolicy.ResourceType"/>.
    /// </summary>
    /// <param name="policy">Policy to register. Must not be null and must specify a non-empty <see cref="RLSPolicy.ResourceType"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policy"/>.<see cref="RLSPolicy.ResourceType"/> is null, empty, or whitespace.</exception>
    public void RegisterPolicy(RLSPolicy policy)
    {
        if (policy is null) throw new ArgumentNullException(nameof(policy));
        if (string.IsNullOrWhiteSpace(policy.ResourceType))
            throw new ArgumentException("Policy ResourceType must be specified.", nameof(policy));
        _policies[policy.ResourceType] = policy;
    }

    /// <summary>
    /// Registers an explicit "no restriction" policy for <paramref name="resourceType"/>. Use this
    /// to opt a specific resource out of RLS without weakening the engine-wide
    /// <see cref="RLSDefaultBehavior"/>. The registration is loud and intentional, which makes
    /// review easier than relying on a permissive default.
    /// </summary>
    /// <typeparam name="T">Entity type the predicate applies to.</typeparam>
    /// <param name="resourceType">Resource type identifier.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceType"/> is null, empty, or whitespace.</exception>
    public void RegisterUnrestricted<T>(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type must be specified.", nameof(resourceType));

        var parameter = Expression.Parameter(typeof(T), "x");
        Func<RLSContext, Expression<Func<T, bool>>> factory =
            _ => Expression.Lambda<Func<T, bool>>(Expression.Constant(true), parameter);

        _policies[resourceType] = new RLSPolicy
        {
            ResourceType = resourceType,
            FilterGenerator = _ => RLSFilter.AllowAll,
            PredicateFactory = factory,
        };
    }

    /// <summary>
    /// Generates a parameterized filter for the given resource.
    /// </summary>
    /// <remarks>
    /// <b>Fail-closed by default.</b> When no policy is registered the behavior follows the
    /// <see cref="RLSDefaultBehavior"/> configured on the constructor:
    /// <list type="bullet">
    ///   <item><description><see cref="RLSDefaultBehavior.Throw"/> (default) — throws <see cref="InvalidOperationException"/>.</description></item>
    ///   <item><description><see cref="RLSDefaultBehavior.DenyAll"/> — returns <see cref="RLSFilter.DenyAll"/>.</description></item>
    ///   <item><description><see cref="RLSDefaultBehavior.AllowAll"/> — returns <see cref="RLSFilter.AllowAll"/>; opt-in only.</description></item>
    /// </list>
    /// To allow a specific resource without changing the default, call
    /// <see cref="RegisterUnrestricted{T}(string)"/>.
    /// </remarks>
    /// <param name="resourceType">Resource type the filter is being requested for.</param>
    /// <param name="context">Caller context consulted by the policy's <see cref="RLSPolicy.FilterGenerator"/>.</param>
    /// <returns>An <see cref="RLSFilter"/> describing the SQL fragment and parameters to apply, never null.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceType"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no policy is registered and the engine was constructed with <see cref="RLSDefaultBehavior.Throw"/>.</exception>
    public RLSFilter GenerateFilter(string resourceType, RLSContext context)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type must be specified.", nameof(resourceType));
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!_policies.TryGetValue(resourceType, out var policy))
        {
            return _defaultBehavior switch
            {
                RLSDefaultBehavior.AllowAll => RLSFilter.AllowAll,
                RLSDefaultBehavior.DenyAll => RLSFilter.DenyAll,
                _ => throw new InvalidOperationException(
                    $"No row-level security policy is registered for resource type '{resourceType}'. " +
                    "Register a policy via RegisterPolicy(...), or call RegisterUnrestricted<T>(...) for resources that have no RLS, " +
                    "or construct the engine with RLSDefaultBehavior.DenyAll/AllowAll if a non-throwing default is desired."),
            };
        }

        return policy.FilterGenerator(context) ?? RLSFilter.DenyAll;
    }

    /// <summary>
    /// Resolves a strongly-typed predicate for the given resource.
    /// </summary>
    /// <remarks>
    /// <b>Fail-closed by default.</b> When no policy is registered, or the registered policy has no
    /// <see cref="RLSPolicy.PredicateFactory"/>, the behavior follows the
    /// <see cref="RLSDefaultBehavior"/> configured on the constructor:
    /// <list type="bullet">
    ///   <item><description><see cref="RLSDefaultBehavior.Throw"/> (default) — throws <see cref="InvalidOperationException"/>.</description></item>
    ///   <item><description><see cref="RLSDefaultBehavior.DenyAll"/> — returns a constant-false predicate (<c>x =&gt; false</c>) so the caller's <c>Where</c> short-circuits to no rows.</description></item>
    ///   <item><description><see cref="RLSDefaultBehavior.AllowAll"/> — returns <c>null</c>, signalling "no predicate to apply"; opt-in only.</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">Entity type the predicate applies to.</typeparam>
    /// <param name="resourceType">Resource type the predicate is being requested for.</param>
    /// <param name="context">Caller context consulted by the policy's <see cref="RLSPolicy.PredicateFactory"/>.</param>
    /// <returns>A LINQ predicate, or <c>null</c> when no predicate should be applied (only under <see cref="RLSDefaultBehavior.AllowAll"/>).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceType"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no policy is registered and the engine was constructed with <see cref="RLSDefaultBehavior.Throw"/>, or when the registered <see cref="RLSPolicy.PredicateFactory"/> is incompatible with <typeparamref name="T"/>.</exception>
    public Expression<Func<T, bool>>? GetPredicate<T>(string resourceType, RLSContext context)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type must be specified.", nameof(resourceType));
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!_policies.TryGetValue(resourceType, out var policy) || policy.PredicateFactory is null)
        {
            return _defaultBehavior switch
            {
                RLSDefaultBehavior.AllowAll => null,
                RLSDefaultBehavior.DenyAll => ConstantFalse<T>(),
                _ => throw new InvalidOperationException(
                    $"No row-level security predicate is registered for resource type '{resourceType}' and entity type '{typeof(T).FullName}'. " +
                    "Register a policy with a PredicateFactory, or call RegisterUnrestricted<T>(...) for resources that have no RLS, " +
                    "or construct the engine with RLSDefaultBehavior.DenyAll/AllowAll if a non-throwing default is desired."),
            };
        }

        if (policy.PredicateFactory is Func<RLSContext, Expression<Func<T, bool>>> factory)
            return factory(context);

        throw new InvalidOperationException(
            $"PredicateFactory for resource '{resourceType}' is not compatible with entity type '{typeof(T).FullName}'.");
    }

    private static Expression<Func<T, bool>> ConstantFalse<T>()
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, bool>>(Expression.Constant(false), parameter);
    }

    /// <summary>
    /// Validates that <paramref name="identifier"/> is a simple column/table identifier
    /// (letters, digits, underscores; optionally a single dotted qualifier). Throws otherwise.
    /// Use this before interpolating any identifier into a SQL fragment.
    /// </summary>
    /// <param name="identifier">Candidate identifier. Must match <c>[A-Za-z_][A-Za-z0-9_]*</c> with an optional single dotted qualifier and be at most 128 characters.</param>
    /// <returns>The identifier unchanged when valid; suitable for direct interpolation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> is empty, exceeds 128 characters, or fails the identifier pattern.</exception>
    public static string ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier must not be empty.", nameof(identifier));
        if (identifier.Length > 128)
            throw new ArgumentException("Identifier exceeds 128 characters.", nameof(identifier));
        if (!IdentifierPattern.IsMatch(identifier))
            throw new ArgumentException(
                $"Invalid SQL identifier: '{identifier}'. Identifiers must match [A-Za-z_][A-Za-z0-9_]* with an optional single dotted qualifier.",
                nameof(identifier));
        return identifier;
    }

    /// <summary>
    /// Escapes a value for safe inclusion as a SQL string literal (single-quote doubling).
    /// Rejects null characters which terminate strings on some drivers. Prefer parameterized
    /// placeholders in <see cref="RLSFilter.Parameters"/> over literal escaping.
    /// </summary>
    /// <param name="value">The value to escape. <c>null</c> produces the literal <c>NULL</c>.</param>
    /// <returns>The single-quoted, single-quote-doubled SQL string literal, or <c>NULL</c> when <paramref name="value"/> is <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> contains a null character.</exception>
    public static string EscapeSqlLiteral(string? value)
    {
        if (value is null) return "NULL";
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("Null characters are not permitted in SQL literals.", nameof(value));
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('\'');
        foreach (var c in value)
        {
            if (c == '\'') sb.Append('\'');
            sb.Append(c);
        }
        sb.Append('\'');
        return sb.ToString();
    }

    /// <summary>
    /// Creates a department-based RLS policy. The department column name is validated; the
    /// runtime department value is passed as a parameter to prevent injection.
    /// </summary>
    /// <param name="deptFieldName">SQL column name holding the department value. Must pass <see cref="ValidateIdentifier"/>.</param>
    /// <returns>A policy whose filter restricts rows to <c>RLSContext.Department</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deptFieldName"/> is not a valid identifier.</exception>
    public static RLSPolicy CreateDepartmentBased(string deptFieldName)
    {
        var column = ValidateIdentifier(deptFieldName);
        return new RLSPolicy
        {
            FilterGenerator = ctx => new RLSFilter(
                $"{column} = @rls_dept",
                new Dictionary<string, object?> { ["rls_dept"] = ctx.Department })
        };
    }

    /// <summary>
    /// Creates a tenant-based RLS policy. The column is validated; tenant ids are passed as
    /// parameters (one per allowed tenant) to prevent injection. Empty allow-lists fail closed.
    /// </summary>
    /// <param name="tenantFieldName">SQL column name holding the tenant identifier. Must pass <see cref="ValidateIdentifier"/>.</param>
    /// <returns>A policy whose filter restricts rows to <c>RLSContext.AllowedTenants</c>; produces <see cref="RLSFilter.DenyAll"/> when the allow-list is empty.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tenantFieldName"/> is not a valid identifier.</exception>
    public static RLSPolicy CreateTenantBased(string tenantFieldName)
    {
        var column = ValidateIdentifier(tenantFieldName);
        return new RLSPolicy
        {
            FilterGenerator = ctx =>
            {
                if (ctx.AllowedTenants is null || ctx.AllowedTenants.Count == 0)
                    return RLSFilter.DenyAll;

                var parameters = new Dictionary<string, object?>(ctx.AllowedTenants.Count);
                var placeholders = new string[ctx.AllowedTenants.Count];
                for (var i = 0; i < ctx.AllowedTenants.Count; i++)
                {
                    var name = $"rls_tenant_{i}";
                    parameters[name] = ctx.AllowedTenants[i];
                    placeholders[i] = "@" + name;
                }
                return new RLSFilter($"{column} IN ({string.Join(",", placeholders)})", parameters);
            }
        };
    }

    /// <summary>
    /// Creates an owner-based RLS policy. The owner column is validated; the user id is
    /// parameterized.
    /// </summary>
    /// <param name="ownerFieldName">SQL column name holding the owning user identifier. Must pass <see cref="ValidateIdentifier"/>.</param>
    /// <returns>A policy whose filter restricts rows to <c>RLSContext.UserId</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ownerFieldName"/> is not a valid identifier.</exception>
    public static RLSPolicy CreateOwnerBased(string ownerFieldName)
    {
        var column = ValidateIdentifier(ownerFieldName);
        return new RLSPolicy
        {
            FilterGenerator = ctx => new RLSFilter(
                $"{column} = @rls_owner",
                new Dictionary<string, object?> { ["rls_owner"] = ctx.UserId })
        };
    }
}
