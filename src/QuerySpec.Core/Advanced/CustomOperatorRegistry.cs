using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Custom operator interface for plugin-based extensions.
/// </summary>
public interface ICustomOperator
{
    /// <summary>Name of the custom operator. Used as the registry key.</summary>
    string Name { get; }
    /// <summary>Human-readable description of the custom operator.</summary>
    string Description { get; }
    /// <summary>Types supported by this operator.</summary>
    Type[] SupportedTypes { get; }
    /// <summary>Executes the custom operator.</summary>
    /// <param name="value">Field value being inspected.</param>
    /// <param name="filterValue">Filter value supplied alongside the operator.</param>
    /// <returns>The operator's evaluation result; the shape is operator-defined.</returns>
    object? Execute(object value, object filterValue);
}

/// <summary>
/// Manages custom operators for extensibility.
/// </summary>
public class CustomOperatorRegistry
{
    private readonly ConcurrentDictionary<string, ICustomOperator> _operators = new();

    /// <summary>Initializes a new custom operator registry.</summary>
    public CustomOperatorRegistry() { }

    /// <summary>
    /// Registers a custom operator. The first registration wins; subsequent registrations
    /// for the same <see cref="ICustomOperator.Name"/> are ignored.
    /// </summary>
    /// <param name="op">Operator to register. <see cref="ICustomOperator.Name"/> is used as the key.</param>
    public void Register(ICustomOperator op)
    {
        _operators.TryAdd(op.Name, op);
    }

    /// <summary>
    /// Gets a custom operator by name.
    /// </summary>
    /// <param name="name">Name of the operator to look up.</param>
    /// <returns>The registered operator, or <c>null</c> when no operator with that name has been registered.</returns>
    public ICustomOperator? Get(string name)
    {
        return _operators.TryGetValue(name, out var op) ? op : null;
    }

    /// <summary>
    /// Gets all registered operators.
    /// </summary>
    /// <returns>Every <see cref="ICustomOperator"/> currently registered, in arbitrary order.</returns>
    public IEnumerable<ICustomOperator> GetAll() => _operators.Values;
}
