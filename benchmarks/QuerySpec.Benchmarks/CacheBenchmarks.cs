using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using QuerySpec.Core.Caching;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Cache round-trip benchmarks: memory-only, distributed (in-memory stub), and
/// multi-level promotion paths.
/// </summary>
[BenchmarkDotNet.Attributes.Config(typeof(BenchConfig))]
public class CacheBenchmarks
{
    private MemoryCacheProvider _memory = null!;
    private DistributedCacheProvider _distributed = null!;
    private MultiLevelCache _multi = null!;
    private Payload _value = null!;

    /// <summary>Fixture: creates providers and warms a single key in each.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _memory = new MemoryCacheProvider();
        _distributed = new DistributedCacheProvider(new InMemoryDistributedCache());
        _multi = new MultiLevelCache(new MemoryCacheProvider(), new DistributedCacheProvider(new InMemoryDistributedCache()));
        _value = new Payload { Id = 42, Name = "hot" };

        _memory.SetAsync("k", _value).GetAwaiter().GetResult();
        _distributed.SetAsync("k", _value).GetAwaiter().GetResult();
        _multi.SetAsync("k", _value).GetAwaiter().GetResult();
    }

    /// <summary>Hot read from memory cache — measures interlocked stats + MemoryCache lookup.</summary>
    [Benchmark(Baseline = true)]
    public async Task<Payload?> Memory_Get()
        => await _memory.GetAsync<Payload>("k").ConfigureAwait(false);

    /// <summary>Memory cache write path.</summary>
    [Benchmark]
    public async Task Memory_Set()
        => await _memory.SetAsync("w", _value, TimeSpan.FromMinutes(5)).ConfigureAwait(false);

    /// <summary>Distributed cache read with JSON deserialization.</summary>
    [Benchmark]
    public async Task<Payload?> Distributed_Get()
        => await _distributed.GetAsync<Payload>("k").ConfigureAwait(false);

    /// <summary>Distributed cache write with JSON serialization.</summary>
    [Benchmark]
    public async Task Distributed_Set()
        => await _distributed.SetAsync("w", _value, TimeSpan.FromMinutes(5)).ConfigureAwait(false);

    /// <summary>Multi-level L1 hit — should match Memory_Get modulo statistics overhead.</summary>
    [Benchmark]
    public async Task<Payload?> MultiLevel_L1Hit()
        => await _multi.GetAsync<Payload>("k").ConfigureAwait(false);

    private sealed class InMemoryDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
        private readonly object _lock = new();

        public byte[]? Get(string key)
        {
            lock (_lock) return _store.TryGetValue(key, out var v) ? v : null;
        }

        public Task<byte[]?> GetAsync(string key, System.Threading.CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            lock (_lock) _store[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, System.Threading.CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }
        public Task RefreshAsync(string key, System.Threading.CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
            lock (_lock) _store.Remove(key);
        }

        public Task RemoveAsync(string key, System.Threading.CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    /// <summary>Test payload for cache serialization benchmarks.</summary>
    public sealed class Payload
    {
        /// <summary>Id.</summary>
        public int Id { get; set; }
        /// <summary>Name.</summary>
        public string Name { get; set; } = string.Empty;
    }
}
