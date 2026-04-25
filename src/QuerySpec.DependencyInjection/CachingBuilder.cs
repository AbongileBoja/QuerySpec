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
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public CachingBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
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

    /// <summary>Enables compression for cache entries larger than the given threshold.</summary>
    /// <param name="thresholdBytes">Compress entries whose serialized size exceeds this many bytes.</param>
    /// <exception cref="NotImplementedException">Always thrown. Compression is not implemented.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public CachingBuilder EnableCompressionForLarge(int thresholdBytes) =>
        throw new NotImplementedException("CachingBuilder.EnableCompressionForLarge is not implemented.");
}
