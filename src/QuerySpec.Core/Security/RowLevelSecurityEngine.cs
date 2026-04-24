using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Security;

/// <summary>
/// Row-Level Security (RLS) engine for multi-tenant applications.
/// Provides policy-based access control at the data row level.
/// </summary>
/// <remarks>
/// The engine supports two policy styles:
/// <list type="bullet">
///   <item><description><see cref="RLSPolicy.PredicateFactory"/> — strongly-typed LINQ expressions (recommended; composable with IQueryable and safe by construction).</description></item>
///   <item><description><see cref="RLSPolicy.FilterGenerator"/> — SQL fragment generator used when raw SQL is unavoidable. All identifiers and literals are validated/escaped to prevent injection.</description></item>
/// </list>
/// </remarks>
public class RowLevelSecurityEngine
{
    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Context information for RLS evaluation.
    /// </summary>
    public class RLSContext
    {
        /// <summary>User ID for RLS evaluation.</summary>
        public string UserId { get; set; } = string.Empty;
        /// <summary>Department for department-based filtering.</summary>
        public string Department { get; set; } = string.Empty;
        /// <summary>Region for region-based filtering.</summary>
        public string Region { get; set; } = string.Empty;
        /// <summary>List of allowed tenant IDs.</summary>
        public List<string> AllowedTenants { get; set; } = new();
        /// <summary>Custom attributes for custom filtering logic.</summary>
        public Dictionary<string, object> CustomAttributes { get; set; } = new();
    }

    /// <summary>
    /// Row-level security policy definition.
    /// </summary>
    public class RLSPolicy
    {
        /// <summary>Resource type this policy applies to.</summary>
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>
        /// Generates a parameterized SQL fragment for the policy. Implementations MUST use
        /// <see cref="RowLevelSecurityEngine.ValidateIdentifier"/> for any column name they emit
        /// and <see cref="RowLevelSecurityEngine.EscapeSqlLiteral"/> for any inlined string literal,
        /// or preferably emit placeholders and populate <see cref="RLSFilter.Parameters"/>.
        /// </summary>
        public Func<RLSContext, RLSFilter> FilterGenerator { get; set; } =
            _ => RLSFilter.AllowAll;

        /// <summary>
        /// Strongly-typed predicate factory for use with IQueryable. Preferred over
        /// <see cref="FilterGenerator"/> because the expression tree is translated safely by the
        /// underlying provider (e.g. EF Core) and cannot be injected into.
        /// </summary>
        public Delegate? PredicateFactory { get; set; }

        /// <summary>Whether to apply this policy hierarchically to related entities.</summary>
        public bool ApplyHierarchically { get; set; } = false;
    }

    /// <summary>
    /// Parameterized SQL filter produced by a policy. Use named parameters
    /// (e.g. <c>@p0</c>) in <see cref="Sql"/> and supply values in <see cref="Parameters"/>.
    /// </summary>
    public sealed class RLSFilter
    {
        /// <summary>Tautology filter that applies no restriction.</summary>
        public static readonly RLSFilter AllowAll = new("1=1");
        /// <summary>Contradiction filter that blocks all rows (fail-closed default).</summary>
        public static readonly RLSFilter DenyAll = new("1=0");

        /// <summary>The SQL fragment (must only reference parameter placeholders).</summary>
        public string Sql { get; }

        /// <summary>Named parameter values keyed by parameter name (without <c>@</c>).</summary>
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        /// <summary>Initializes a new RLS filter.</summary>
        public RLSFilter(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL fragment must not be empty.", nameof(sql));
            Sql = sql;
            Parameters = parameters ?? new Dictionary<string, object?>();
        }
    }

    private readonly ConcurrentDictionary<string, RLSPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an RLS policy. Replaces any previously registered policy for the same
    /// <see cref="RLSPolicy.ResourceType"/>.
    /// </summary>
    public void RegisterPolicy(RLSPolicy policy)
    {
        if (policy is null) throw new ArgumentNullException(nameof(policy));
        if (string.IsNullOrWhiteSpace(policy.ResourceType))
            throw new ArgumentException("Policy ResourceType must be specified.", nameof(policy));
        _policies[policy.ResourceType] = policy;
    }

    /// <summary>
    /// Generates a parameterized filter for the given resource. Returns <see cref="RLSFilter.AllowAll"/>
    /// only when no policy is registered; to fail-closed, register a deny-all policy explicitly.
    /// </summary>
    public RLSFilter GenerateFilter(string resourceType, RLSContext context)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type must be specified.", nameof(resourceType));
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!_policies.TryGetValue(resourceType, out var policy))
            return RLSFilter.AllowAll;

        return policy.FilterGenerator(context) ?? RLSFilter.DenyAll;
    }

    /// <summary>
    /// Resolves a strongly-typed predicate for the given resource, or <c>null</c> if the
    /// registered policy does not define one. Callers should fall back to
    /// <see cref="GenerateFilter"/> or treat a null return as deny-all depending on policy.
    /// </summary>
    public Expression<Func<T, bool>>? GetPredicate<T>(string resourceType, RLSContext context)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type must be specified.", nameof(resourceType));
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!_policies.TryGetValue(resourceType, out var policy) || policy.PredicateFactory is null)
            return null;

        if (policy.PredicateFactory is Func<RLSContext, Expression<Func<T, bool>>> factory)
            return factory(context);

        throw new InvalidOperationException(
            $"PredicateFactory for resource '{resourceType}' is not compatible with entity type '{typeof(T).FullName}'.");
    }

    /// <summary>
    /// Validates that <paramref name="identifier"/> is a simple column/table identifier
    /// (letters, digits, underscores; optionally a single dotted qualifier). Throws otherwise.
    /// Use this before interpolating any identifier into a SQL fragment.
    /// </summary>
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
