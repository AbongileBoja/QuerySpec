using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Parameterized SQL filter produced by an <see cref="RLSPolicy"/>. Use named parameters
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
    /// <param name="sql">SQL fragment. Must not be null or whitespace.</param>
    /// <param name="parameters">Optional parameter map. Defaults to empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
    public RLSFilter(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL fragment must not be empty.", nameof(sql));
        Sql = sql;
        Parameters = parameters ?? new Dictionary<string, object?>();
    }
}
