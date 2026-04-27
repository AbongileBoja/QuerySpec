using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Abstraction for pluggable cache implementations.
/// </summary>
/// <remarks>
/// All async members return <see cref="ValueTask"/> / <see cref="ValueTask{TResult}"/> so
/// implementations that complete synchronously (notably <see cref="MemoryCacheProvider"/>) can
/// avoid the per-call <see cref="Task"/> heap allocation. Existing call sites — <c>await
/// cache.GetAsync(key)</c> — continue to work unchanged because <c>await</c> binds to both
/// task types. Custom implementations that previously returned <see cref="Task"/> must update
/// their return types; this is the binary break that motivates the v3.0 cut.
/// </remarks>
public interface ICacheProvider
{
    /// <summary>Gets a value from cache by key.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Sets a value in cache with optional expiration.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">Optional time-to-live; <c>null</c> uses the implementation default.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Removes a value from cache by key.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Checks if a key exists in cache.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Flushes all cache entries.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets current cache statistics.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache invalidation strategy for complex scenarios. All members return <see cref="ValueTask"/>
/// for consistency with <see cref="ICacheProvider"/>.
/// </summary>
public interface ICacheInvalidationStrategy
{
    /// <summary>Invalidates a specific cache key.</summary>
    /// <param name="key">Cache key to evict.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask InvalidateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Invalidates cache keys matching a pattern.</summary>
    /// <param name="pattern">Glob- or backend-specific pattern matched against stored keys.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>Invalidates all cache keys for a tenant.</summary>
    /// <param name="tenantId">Tenant whose entries should be evicted.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask InvalidateByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Invalidates all cache keys for a user.</summary>
    /// <param name="userId">User whose entries should be evicted.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask InvalidateByUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache warmer for pre-loading frequently accessed data.
/// </summary>
public interface ICacheWarmer
{
    /// <summary>Pre-loads frequently accessed data into cache.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    ValueTask WarmCacheAsync(CancellationToken cancellationToken = default);
}
