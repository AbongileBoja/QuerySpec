using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Cache lifecycle and statistics abstraction. The type-constrained read/write API has moved
/// to <see cref="ICacheStore"/> (<see cref="ICacheStore.TryGetAsync{T}"/> /
/// <see cref="ICacheStore.SetValueAsync{T}"/>); this interface continues to host the operations
/// that don't carry a generic type parameter.
/// </summary>
/// <remarks>
/// All async members return <see cref="ValueTask"/> / <see cref="ValueTask{TResult}"/> so
/// implementations that complete synchronously (notably <see cref="MemoryCacheProvider"/>) can
/// avoid the per-call <see cref="Task"/> heap allocation.
/// </remarks>
public interface ICacheProvider
{
    /// <summary>Removes a value from cache by key.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A completed task on success.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Checks if a key exists in cache.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns><see langword="true"/> when an entry exists for <paramref name="key"/>; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Flushes all cache entries.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A completed task on success.</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets current cache statistics.</summary>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>An immutable snapshot of the hit/miss/set/remove counters.</returns>
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
