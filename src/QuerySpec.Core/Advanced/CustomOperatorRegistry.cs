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
    /// <summary>Name of the custom operator.</summary>
    string Name { get; }
    /// <summary>Description of the custom operator.</summary>
    string Description { get; }
    /// <summary>Types supported by this operator.</summary>
    Type[] SupportedTypes { get; }
    /// <summary>Executes the custom operator.</summary>
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
    /// Registers a custom operator.
    /// </summary>
    public void Register(ICustomOperator op)
    {
        _operators.TryAdd(op.Name, op);
    }

    /// <summary>
    /// Gets a custom operator by name.
    /// </summary>
    public ICustomOperator? Get(string name)
    {
        return _operators.TryGetValue(name, out var op) ? op : null;
    }

    /// <summary>
    /// Gets all registered operators.
    /// </summary>
    public IEnumerable<ICustomOperator> GetAll() => _operators.Values;
}
