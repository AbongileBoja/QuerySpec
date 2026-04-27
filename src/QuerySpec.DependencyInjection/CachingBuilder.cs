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
    /// Configures in-memory cache provider. Registers a single <see cref="MemoryCacheProvider"/>
    /// singleton against both the <see cref="ICacheProvider"/> (lifecycle / stats) and
    /// <see cref="ICacheStore"/> (typed read/write) contracts so consumers may depend on either.
    /// </summary>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseMemoryCache()
    {
        Services.AddMemoryCache();
        Services.AddSingleton<MemoryCacheProvider>();
        Services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<MemoryCacheProvider>());
        Services.AddSingleton<ICacheStore>(sp => sp.GetRequiredService<MemoryCacheProvider>());
        return this;
    }

    /// <summary>
    /// Configures distributed Redis cache provider. Registers a single
    /// <see cref="DistributedCacheProvider"/> singleton against both <see cref="ICacheProvider"/>
    /// and <see cref="ICacheStore"/> contracts.
    /// </summary>
    /// <param name="connectionString">StackExchange.Redis connection string passed through to <c>AddStackExchangeRedisCache</c>.</param>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseDistributedRedis(string connectionString)
    {
        Services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);
        Services.AddSingleton<DistributedCacheProvider>(sp =>
            new DistributedCacheProvider(sp.GetRequiredService<IDistributedCache>()));
        Services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<DistributedCacheProvider>());
        Services.AddSingleton<ICacheStore>(sp => sp.GetRequiredService<DistributedCacheProvider>());
        return this;
    }

    /// <summary>
    /// Configures multi-level cache (memory + distributed). Registers a single
    /// <see cref="MultiLevelCache"/> singleton against both <see cref="ICacheProvider"/>
    /// and <see cref="ICacheStore"/> contracts.
    /// </summary>
    /// <returns>The same <see cref="CachingBuilder"/> for fluent chaining.</returns>
    public CachingBuilder UseMultiLevel()
    {
        Services.AddMemoryCache();
        Services.AddSingleton<MemoryCacheProvider>();
        Services.AddSingleton<DistributedCacheProvider>();
        Services.AddSingleton<MultiLevelCache>(sp =>
            new MultiLevelCache(
                sp.GetRequiredService<MemoryCacheProvider>(),
                sp.GetRequiredService<DistributedCacheProvider>()));
        Services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<MultiLevelCache>());
        Services.AddSingleton<ICacheStore>(sp => sp.GetRequiredService<MultiLevelCache>());
        return this;
    }

}
