using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Closes remaining coverage gaps in CacheResult (static factory), CachePolicy static
/// factory properties, and DistributedCacheProvider error paths not exercised by the
/// primary test suite (RemoveAsync failure, ExistsAsync failure, EvictFailed during
/// corrupt-payload cleanup).
/// </summary>
[RequiresUnreferencedCode("Tests exercise DistributedCacheProvider which uses reflection-based JSON.")]
[RequiresDynamicCode("Tests exercise DistributedCacheProvider which emits IL at runtime.")]
public class CachingCoverageGapTests
{
    // ── CacheResult static factory ────────────────────────────────────────────

    [Fact]
    public void CacheResult_Hit_ReturnsHitResult()
    {
        var r = CacheResult.Hit("hello");
        Assert.True(r.HasValue);
        Assert.Equal("hello", r.Value);
    }

    [Fact]
    public void CacheResult_Miss_ReturnsMissResult()
    {
        var r = CacheResult.Miss<string>();
        Assert.False(r.HasValue);
    }

    // ── CachePolicy static factory properties ─────────────────────────────────

    [Fact]
    public void CachePolicy_None_HasNullDuration()
    {
        Assert.Null(CachePolicy.None.Duration);
    }

    [Fact]
    public void CachePolicy_Short_HasFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), CachePolicy.Short.Duration);
    }

    [Fact]
    public void CachePolicy_Medium_HasOneHour()
    {
        Assert.Equal(TimeSpan.FromHours(1), CachePolicy.Medium.Duration);
    }

    [Fact]
    public void CachePolicy_Long_HasOneDay()
    {
        Assert.Equal(TimeSpan.FromDays(1), CachePolicy.Long.Duration);
    }

    [Fact]
    public void CachePolicy_DefaultInstance_HasExpectedDefaults()
    {
        var policy = new CachePolicy();
        Assert.Null(policy.Duration);
        Assert.Null(policy.CacheKeyPrefix);
        Assert.True(policy.CacheByTenant);
        Assert.True(policy.CacheByUser);
        Assert.True(policy.CacheByPermissions);
        Assert.True(policy.CompressForSize);
        Assert.Equal(5000, policy.CompressionThresholdBytes);
        Assert.NotNull(policy.InvalidationTriggers);
        Assert.Empty(policy.InvalidationTriggers);
    }

    // ── DistributedCacheProvider: RemoveAsync transport failure ───────────────

    private sealed class FailingRemoveCache : IDistributedCache
    {
        public Exception? RemoveThrows { get; set; }
        public Exception? ExistsGetThrows { get; set; }

        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            if (ExistsGetThrows != null) throw ExistsGetThrows;
            return Task.FromResult<byte[]?>(null);
        }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => Task.CompletedTask;
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { if (RemoveThrows != null) throw RemoveThrows; }
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            if (RemoveThrows != null) throw RemoveThrows;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RemoveAsync_TransportFailure_ThrowsToCallerAndLogs()
    {
        var fake = new FailingRemoveCache { RemoveThrows = new InvalidOperationException("network") };
        var provider = new DistributedCacheProvider(fake);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RemoveAsync("k").AsTask());
    }

    [Fact]
    public async Task ExistsAsync_TransportFailure_ReturnsFalse()
    {
        var fake = new FailingRemoveCache { ExistsGetThrows = new InvalidOperationException("redis") };
        var provider = new DistributedCacheProvider(fake);
        var exists = await provider.ExistsAsync("k");
        Assert.False(exists);
    }

    [Fact]
    public async Task CorruptPayload_WhenEvictFails_StillReturnsMiss()
    {
        // Covers EvictFailed log path: GetAsync returns corrupt bytes, then RemoveAsync fails
        var fake = new PoisonAndFailRemoveCache();
        var provider = new DistributedCacheProvider(fake);
        var result = await provider.TryGetAsync<object>("poison");
        Assert.False(result.HasValue);
    }

    private sealed class PoisonAndFailRemoveCache : IDistributedCache
    {
        public byte[]? Get(string key) => Encoding.UTF8.GetBytes("not valid json {{{");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes("not valid json {{{"));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => Task.CompletedTask;
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => throw new InvalidOperationException("evict failed");
        public Task RemoveAsync(string key, CancellationToken token = default)
            => Task.FromException(new InvalidOperationException("evict failed"));
    }
}
