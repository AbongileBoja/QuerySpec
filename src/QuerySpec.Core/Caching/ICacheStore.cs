using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Pluggable cache abstraction. Lifts the <c>where T : class</c> constraint so value types
/// (e.g. <c>int</c>, <c>Guid</c>, custom records) compose directly without box-and-cast wrappers,
/// and uses <see cref="CacheResult{T}"/> on reads to disambiguate "hit on <see langword="default"/>"
/// from "miss".
/// </summary>
/// <remarks>
/// Shipping providers (<see cref="MemoryCacheProvider"/>, <see cref="DistributedCacheProvider"/>,
/// <see cref="MultiLevelCache"/>) implement this contract alongside <see cref="ICacheProvider"/>;
/// consumers may inject either contract.
/// </remarks>
public interface ICacheStore
{
    /// <summary>
    /// Attempts to fetch the entry stored under <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="T">The expected value type. May be a reference or value type.</typeparam>
    /// <param name="key">Cache key. Implementations validate non-null/non-whitespace.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A <see cref="CacheResult{T}"/> whose <see cref="CacheResult{T}.HasValue"/> indicates whether the lookup hit.</returns>
    [RequiresUnreferencedCode(CacheStoreTrimMessage)]
    [RequiresDynamicCode(CacheStoreAotMessage)]
    ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> with optional time-to-live.
    /// </summary>
    /// <typeparam name="T">The value type. May be a reference or value type.</typeparam>
    /// <param name="key">Cache key. Implementations validate non-null/non-whitespace.</param>
    /// <param name="value">Value to store; <see langword="null"/> is permitted for reference types.</param>
    /// <param name="ttl">Optional positive time-to-live; <see langword="null"/> uses the implementation default.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A completed task on success.</returns>
    [RequiresUnreferencedCode(CacheStoreTrimMessage)]
    [RequiresDynamicCode(CacheStoreAotMessage)]
    ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entry stored under <paramref name="key"/>, if any.
    /// </summary>
    /// <param name="key">Cache key. Implementations validate non-null/non-whitespace.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A completed task on success.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    internal const string CacheStoreTrimMessage =
        "Cache store reads and writes deserialise/serialise T. Distributed implementations (DistributedCacheProvider, MultiLevelCache) use System.Text.Json reflection-based serialisation, which may emit incomplete payloads under trimming when members of T are removed. In-process implementations (MemoryCacheProvider) are trim-safe; suppress the warning at the call site only when you can prove the underlying provider does not serialise.";
    internal const string CacheStoreAotMessage =
        "Cache store reads and writes serialise T. Distributed implementations use System.Text.Json reflection-based serialisation, which emits IL at runtime. Use System.Text.Json source generation (JsonSerializerContext) for AOT scenarios.";
}
