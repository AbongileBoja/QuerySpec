using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Pluggable cache abstraction that supersedes <see cref="ICacheProvider"/>. Drops the
/// <c>where T : class</c> constraint so value types (e.g. <c>int</c>, <c>Guid</c>, custom records)
/// compose directly without box-and-cast wrappers, and uses <see cref="CacheResult{T}"/> on reads
/// to disambiguate "hit on <see langword="default"/>" from "miss".
/// </summary>
/// <remarks>
/// Shipping providers (<see cref="MemoryCacheProvider"/>, <see cref="DistributedCacheProvider"/>,
/// <see cref="MultiLevelCache"/>) implement both <see cref="ICacheProvider"/> and
/// <see cref="ICacheStore"/> through the 3.x line. The legacy interface methods remain functional
/// but are diagnostic-id <c>QSPEC0003</c> and slated for removal in 4.0.
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
    ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entry stored under <paramref name="key"/>, if any.
    /// </summary>
    /// <param name="key">Cache key. Implementations validate non-null/non-whitespace.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A completed task on success.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
