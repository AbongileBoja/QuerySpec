using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Bulkhead pattern for resource isolation and concurrent request limiting.
/// </summary>
public class BulkheadPolicy
{
    private readonly SemaphoreSlim _semaphore;
    /// <summary>Maximum number of concurrent requests allowed.</summary>
    public int MaxConcurrentRequests { get; }

    /// <summary>Initializes a new bulkhead policy with the specified concurrency limit.</summary>
    public BulkheadPolicy(int maxConcurrentRequests)
    {
        MaxConcurrentRequests = maxConcurrentRequests;
        _semaphore = new SemaphoreSlim(maxConcurrentRequests);
    }

    /// <summary>
    /// Executes operation with bulkhead protection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!await _semaphore.WaitAsync(0).ConfigureAwait(false))
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
