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
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>UseXxx</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>,
    /// <c>IMvcBuilder.Services</c>, and <c>AuthenticationBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new caching builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public CachingBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Configures in-memory cache provider.
    /// </summary>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseMemoryCache()
    {
        Services.AddMemoryCache();
        Services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
        return this;
    }

    /// <summary>
    /// Configures distributed Redis cache provider.
    /// </summary>
    /// <param name="connectionString">StackExchange.Redis connection string passed through to <c>AddStackExchangeRedisCache</c>.</param>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseDistributedRedis(string connectionString)
    {
        Services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);
        Services.AddSingleton<ICacheProvider>(sp =>
            new DistributedCacheProvider(sp.GetRequiredService<IDistributedCache>()));
        return this;
    }

    /// <summary>
    /// Configures multi-level cache (memory + distributed).
    /// </summary>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseMultiLevel()
    {
        Services.AddMemoryCache();
        Services.AddSingleton<MemoryCacheProvider>();
        Services.AddSingleton<DistributedCacheProvider>();
        Services.AddSingleton<ICacheProvider>(sp =>
            new MultiLevelCache(
                sp.GetRequiredService<MemoryCacheProvider>(),
                sp.GetRequiredService<DistributedCacheProvider>()));
        return this;
    }

    /// <summary>Enables compression for cache entries larger than the given threshold.</summary>
    /// <param name="thresholdBytes">Compress entries whose serialized size exceeds this many bytes.</param>
    /// <exception cref="NotImplementedException">Always thrown. Compression is not implemented.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Wrap ICacheProvider with a compressing decorator (e.g. via Scrutor's Services.Decorate) or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public CachingBuilder EnableCompressionForLarge(int thresholdBytes) =>
        throw new NotImplementedException("CachingBuilder.EnableCompressionForLarge is not implemented.");
}
