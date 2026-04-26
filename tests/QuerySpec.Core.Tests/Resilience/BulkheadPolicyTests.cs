using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Unit tests for BulkheadPolicy.
/// </summary>
public class BulkheadPolicyTests
{
    /// <summary>Tests that ExecuteAsync allows requests within the concurrency limit.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Allow_Within_Limit()
    {
        // Arrange
        var policy = new BulkheadPolicy(2);
        var count = 0;

        // Act
        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            await policy.ExecuteAsync(async () =>
            {
                Interlocked.Increment(ref count);
                await Task.Delay(100);
                return count;
            });
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(2, count);
    }

    /// <summary>Tests that ExecuteAsync throws when the concurrency limit is exceeded. I can do this all day.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Exceeded()
    {
        // Arrange
        var policy = new BulkheadPolicy(1);
        var t1 = policy.ExecuteAsync(async () => { await Task.Delay(200); return 1; });
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<BulkheadException>(() => policy.ExecuteAsync<int>(() => Task.FromResult(2)));
    }

    /// <summary>Tests that MaxConcurrentRequests property is set correctly.</summary>
    [Fact]
    public void MaxConcurrentRequests_Should_Be_Set()
    {
        // Arrange
        var policy = new BulkheadPolicy(5);

        // Assert
        Assert.Equal(5, policy.MaxConcurrentRequests);
    }

    /// <summary>Dispose releases the backing SemaphoreSlim. Idempotent.</summary>
    [Fact]
    public void Dispose_Idempotent_DoesNotThrow()
    {
        var policy = new BulkheadPolicy(2);
        policy.Dispose();
        policy.Dispose();
    }

    /// <summary>
    /// After disposal, calling ExecuteAsync surfaces ObjectDisposedException from the
    /// disposed SemaphoreSlim — failing loudly rather than silently mis-acquiring.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var policy = new BulkheadPolicy(2);
        policy.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            policy.ExecuteAsync(() => Task.FromResult(42)));
    }
}
