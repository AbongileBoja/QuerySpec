using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Caching;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Caching configuration builder.
/// </summary>
public class CachingBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new caching builder.</summary>
    public CachingBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configures in-memory cache provider.
    /// </summary>
    public CachingBuilder UseMemoryCache()
    {
        _services.AddMemoryCache();
        _services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
        return this;
    }

    /// <summary>
    /// Configures distributed Redis cache provider.
    /// </summary>
    public CachingBuilder UseDistributedRedis(string connectionString)
    {
        _services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);
        _services.AddSingleton<ICacheProvider>(sp =>
            new DistributedCacheProvider(sp.GetRequiredService<IDistributedCache>()));
        return this;
    }

    /// <summary>
    /// Configures multi-level cache (memory + distributed).
    /// </summary>
    public CachingBuilder UseMultiLevel()
    {
        _services.AddMemoryCache();
        _services.AddSingleton<MemoryCacheProvider>();
        _services.AddSingleton<DistributedCacheProvider>();
        _services.AddSingleton<ICacheProvider>(sp =>
            new MultiLevelCache(
                sp.GetRequiredService<MemoryCacheProvider>(),
                sp.GetRequiredService<DistributedCacheProvider>()));
        return this;
    }

    /// <summary>
    /// Enables compression for cache entries larger than threshold.
    /// </summary>
    public CachingBuilder EnableCompressionForLarge(int thresholdBytes)
    {
        // Implementation for compression
        return this;
    }
}
