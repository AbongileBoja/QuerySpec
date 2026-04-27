using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Bulkhead pattern for resource isolation and concurrent request limiting. Implements
/// <see cref="IDisposable"/> because the backing <see cref="SemaphoreSlim"/> owns a
/// kernel-allocated wait handle that must be released on teardown.
/// </summary>
public class BulkheadPolicy : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    /// <summary>Maximum number of concurrent requests allowed.</summary>
    public int MaxConcurrentRequests { get; }

    /// <summary>Initializes a new bulkhead policy with the specified concurrency limit.</summary>
    /// <param name="maxConcurrentRequests">Maximum number of operations allowed to execute concurrently. Must be positive.</param>
    public BulkheadPolicy(int maxConcurrentRequests)
    {
        MaxConcurrentRequests = maxConcurrentRequests;
        _semaphore = new SemaphoreSlim(maxConcurrentRequests);
    }

    /// <summary>
    /// Executes operation with bulkhead protection. Equivalent to
    /// <see cref="ExecuteAsync{T}(Func{Task{T}}, CancellationToken)"/> with <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="BulkheadException">Thrown when no slot is available within the bulkhead.</exception>
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        => ExecuteAsync(operation, CancellationToken.None);

    /// <summary>
    /// Executes operation with bulkhead protection and cancellation support.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <param name="cancellationToken">Token observed before semaphore acquisition and before invoking <paramref name="operation"/>.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="BulkheadException">Thrown when no slot is available within the bulkhead.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();

        if (!await _semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new BulkheadException($"Bulkhead limit exceeded ({MaxConcurrentRequests})");

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Releases the backing <see cref="SemaphoreSlim"/>. Idempotent.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Dispose pattern hook for subclasses.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>, <c>false</c> from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _semaphore.Dispose();
        }
        _disposed = true;
    }
}

/// <summary>
/// Exception thrown when bulkhead limit is exceeded.
/// </summary>
public class BulkheadException : Exception
{
    /// <summary>Initializes a new bulkhead exception with no message.</summary>
    public BulkheadException() { }

    /// <summary>Initializes a new bulkhead exception with the specified message.</summary>
    /// <param name="message">Description of the bulkhead condition.</param>
    public BulkheadException(string message) : base(message) { }

    /// <summary>Initializes a new bulkhead exception that wraps an inner exception.</summary>
    /// <param name="message">Description of the bulkhead condition.</param>
    /// <param name="innerException">Underlying cause to preserve in the exception chain.</param>
    public BulkheadException(string message, Exception innerException) : base(message, innerException) { }
}
