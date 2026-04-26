using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Abstraction for pluggable cache implementations.
/// </summary>
/// <remarks>
/// Every async member has a paired <see cref="CancellationToken"/>-accepting overload added in 2.1.
/// The CT-less overloads are preserved for source compatibility and delegate to the CT overloads
/// with <see cref="CancellationToken.None"/>. Implementers that ship binary-against 2.0 continue to
/// satisfy the interface via the default implementations; new implementers should override the
/// CT overloads to honour cancellation.
/// </remarks>
public interface ICacheProvider
{
    /// <summary>Gets a value from cache by key.</summary>
    Task<T?> GetAsync<T>(string key) where T : class;
    /// <summary>Gets a value from cache by key with cancellation support.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => GetAsync<T>(key);

    /// <summary>Sets a value in cache with optional expiration.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    /// <summary>Sets a value in cache with optional expiration and cancellation support.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken) where T : class
        => SetAsync(key, value, expiration);

    /// <summary>Removes a value from cache by key.</summary>
    Task RemoveAsync(string key);
    /// <summary>Removes a value from cache by key with cancellation support.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken)
        => RemoveAsync(key);

    /// <summary>Checks if a key exists in cache.</summary>
    Task<bool> ExistsAsync(string key);
    /// <summary>Checks if a key exists in cache with cancellation support.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        => ExistsAsync(key);

    /// <summary>Flushes all cache entries.</summary>
    Task FlushAsync();
    /// <summary>Flushes all cache entries with cancellation support.</summary>
    Task FlushAsync(CancellationToken cancellationToken)
        => FlushAsync();

    /// <summary>Gets current cache statistics.</summary>
    Task<CacheStats> GetStatsAsync();
    /// <summary>Gets current cache statistics with cancellation support.</summary>
    Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken)
        => GetStatsAsync();
}

/// <summary>
/// Cache invalidation strategy for complex scenarios.
/// </summary>
public interface ICacheInvalidationStrategy
{
    /// <summary>Invalidates a specific cache key.</summary>
    Task InvalidateAsync(string key);
    /// <summary>Invalidates a specific cache key with cancellation support.</summary>
    Task InvalidateAsync(string key, CancellationToken cancellationToken)
        => InvalidateAsync(key);

    /// <summary>Invalidates cache keys matching a pattern.</summary>
    Task InvalidatePatternAsync(string pattern);
    /// <summary>Invalidates cache keys matching a pattern with cancellation support.</summary>
    Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken)
        => InvalidatePatternAsync(pattern);

    /// <summary>Invalidates all cache keys for a tenant.</summary>
    Task InvalidateByTenantAsync(string tenantId);
    /// <summary>Invalidates all cache keys for a tenant with cancellation support.</summary>
    Task InvalidateByTenantAsync(string tenantId, CancellationToken cancellationToken)
        => InvalidateByTenantAsync(tenantId);

    /// <summary>Invalidates all cache keys for a user.</summary>
    Task InvalidateByUserAsync(string userId);
    /// <summary>Invalidates all cache keys for a user with cancellation support.</summary>
    Task InvalidateByUserAsync(string userId, CancellationToken cancellationToken)
        => InvalidateByUserAsync(userId);
}

/// <summary>
/// Cache warmer for pre-loading frequently accessed data.
/// </summary>
public interface ICacheWarmer
{
    /// <summary>Pre-loads frequently accessed data into cache.</summary>
    Task WarmCacheAsync();
    /// <summary>Pre-loads frequently accessed data into cache with cancellation support.</summary>
    Task WarmCacheAsync(CancellationToken cancellationToken)
        => WarmCacheAsync();
}
