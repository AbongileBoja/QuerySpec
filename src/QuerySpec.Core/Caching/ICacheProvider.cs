using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Abstraction for pluggable cache implementations.
/// </summary>
public interface ICacheProvider
{
    /// <summary>Gets a value from cache by key.</summary>
    Task<T?> GetAsync<T>(string key) where T : class;
    /// <summary>Sets a value in cache with optional expiration.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    /// <summary>Removes a value from cache by key.</summary>
    Task RemoveAsync(string key);
    /// <summary>Checks if a key exists in cache.</summary>
    Task<bool> ExistsAsync(string key);
    /// <summary>Flushes all cache entries.</summary>
    Task FlushAsync();
    /// <summary>Gets current cache statistics.</summary>
    Task<CacheStats> GetStatsAsync();
}

/// <summary>
/// Cache invalidation strategy for complex scenarios.
/// </summary>
public interface ICacheInvalidationStrategy
{
    /// <summary>Invalidates a specific cache key.</summary>
    Task InvalidateAsync(string key);
    /// <summary>Invalidates cache keys matching a pattern.</summary>
    Task InvalidatePatternAsync(string pattern);
    /// <summary>Invalidates all cache keys for a tenant.</summary>
    Task InvalidateByTenantAsync(string tenantId);
    /// <summary>Invalidates all cache keys for a user.</summary>
    Task InvalidateByUserAsync(string userId);
}

/// <summary>
/// Cache warmer for pre-loading frequently accessed data.
/// </summary>
public interface ICacheWarmer
{
    /// <summary>Pre-loads frequently accessed data into cache.</summary>
    Task WarmCacheAsync();
}
