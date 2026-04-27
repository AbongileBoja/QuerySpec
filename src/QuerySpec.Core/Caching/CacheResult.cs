using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Result of a cache lookup that distinguishes a hit on <see langword="default"/> from a miss
/// without paying a heap allocation for a reference-typed <c>Optional&lt;T&gt;</c> wrapper. Modeled as
/// a <see langword="readonly"/> <see langword="struct"/> for zero-allocation hot paths.
/// </summary>
/// <typeparam name="T">The cached value type. May be a reference or value type.</typeparam>
/// <remarks>
/// Use <see cref="HasValue"/> to discriminate: a true return guarantees <see cref="Value"/>
/// reflects the stored value (which may itself be <see langword="null"/> or
/// <see langword="default"/>). The <see cref="Miss"/> sentinel is the <c>default</c> instance,
/// so an uninitialised field is also a miss.
/// </remarks>
public readonly struct CacheResult<T> : IEquatable<CacheResult<T>>
{
    /// <summary><see langword="true"/> when the cache held an entry for the requested key.</summary>
    public bool HasValue { get; }

    /// <summary>
    /// The cached value when <see cref="HasValue"/> is <see langword="true"/>; otherwise
    /// <see langword="default"/>. Reading this on a miss does not throw — callers should branch
    /// on <see cref="HasValue"/> first or use <see cref="GetValueOrDefault()"/>.
    /// </summary>
    public T Value { get; }

    private CacheResult(bool hasValue, T value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <summary>Creates a hit result wrapping <paramref name="value"/>.</summary>
    /// <param name="value">The cached value to return; may be <see langword="null"/> for reference types.</param>
    /// <returns>A <see cref="CacheResult{T}"/> with <see cref="HasValue"/> set.</returns>
    public static CacheResult<T> Hit(T value) => new(true, value);

    /// <summary>The miss singleton (the <c>default</c> instance).</summary>
    public static CacheResult<T> Miss => default;

    /// <summary>
    /// Returns <see cref="Value"/> on a hit; otherwise <see langword="default"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The cached value or <see langword="default"/>.</returns>
    public T GetValueOrDefault() => HasValue ? Value : default!;

    /// <summary>
    /// Returns <see cref="Value"/> on a hit; otherwise <paramref name="fallback"/>.
    /// </summary>
    /// <param name="fallback">The value to return on miss.</param>
    /// <returns>The cached value or <paramref name="fallback"/>.</returns>
    public T GetValueOrDefault(T fallback) => HasValue ? Value : fallback;

    /// <summary>
    /// Value equality: two miss instances are equal; two hit instances are equal iff their
    /// underlying values are equal under <see cref="EqualityComparer{T}.Default"/>; a hit and
    /// a miss are never equal.
    /// </summary>
    /// <param name="other">The other result to compare.</param>
    /// <returns><see langword="true"/> when both results are equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(CacheResult<T> other)
    {
        if (HasValue != other.HasValue) return false;
        if (!HasValue) return true;
        return EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CacheResult<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (!HasValue) return 0;
        return EqualityComparer<T>.Default.GetHashCode(Value!);
    }

    /// <summary>Equality operator forwarding to <see cref="Equals(CacheResult{T})"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(CacheResult<T> left, CacheResult<T> right) => left.Equals(right);

    /// <summary>Inequality operator forwarding to <see cref="Equals(CacheResult{T})"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when not equal; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(CacheResult<T> left, CacheResult<T> right) => !left.Equals(right);
}
