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
    /// <summary>Tests that ExecuteAsync allows two concurrent operations under a limit of 2.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Allow_Within_Limit()
    {
        using var policy = new BulkheadPolicy(2);
        using var bothEntered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(initialState: false);
        var count = 0;

        // Both operations must enter concurrently for the test to be meaningful — gate them
        // behind a CountdownEvent so neither can complete until both have acquired a slot.
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await policy.ExecuteAsync(() =>
            {
                Interlocked.Increment(ref count);
                bothEntered.Signal();
                release.Wait(TestContext.Current.CancellationToken);
                return Task.FromResult(0);
            });
        })).ToArray();

        bothEntered.Wait(TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
        release.Set();
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Tests that ExecuteAsync throws <see cref="BulkheadException"/> when the concurrency limit
    /// is already saturated. Synchronisation via <see cref="ManualResetEventSlim"/> is deterministic;
    /// the prior wall-clock <c>Task.Delay(50)</c>-based test flaked under CI load when the in-flight
    /// operation hadn't yet acquired the semaphore by the 50ms mark.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Exceeded()
    {
        using var policy = new BulkheadPolicy(1);
        using var inFlightAcquired = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);

        var inFlight = Task.Run(() => policy.ExecuteAsync(() =>
        {
            inFlightAcquired.Set();
            release.Wait(TestContext.Current.CancellationToken);
            return Task.FromResult(1);
        }));

        // Wait for the first operation to actually acquire the slot. No wall-clock guess.
        inFlightAcquired.Wait(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<BulkheadException>(() =>
            policy.ExecuteAsync<int>(() => Task.FromResult(2)));

        release.Set();
        await inFlight;
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

    /// <summary>A pre-cancelled token throws OperationCanceledException before the operation runs.</summary>
    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws_BeforeOperation()
    {
        using var policy = new BulkheadPolicy(2);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ran = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(() => { ran = true; return Task.FromResult(42); }, cts.Token));

        Assert.False(ran);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_ThrowsArgumentNull()
    {
        using var policy = new BulkheadPolicy(2);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            policy.ExecuteAsync<int>(null!, CancellationToken.None));
    }
}
