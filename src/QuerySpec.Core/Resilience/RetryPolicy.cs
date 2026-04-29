using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Retry policy with exponential backoff for transient failures.
/// </summary>
public class RetryPolicy
{
    /// <summary>Maximum number of retry attempts.</summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>Initial delay before first retry.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>Whether to use exponential backoff.</summary>
    public bool UseExponentialBackoff { get; set; } = true;
    /// <summary>Multiplier for exponential backoff.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    /// <summary>Maximum delay between retries.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Initializes a new retry policy.</summary>
    public RetryPolicy() { }

    /// <summary>
    /// Executes operation with retry logic. Equivalent to <see cref="ExecuteAsync{T}(Func{Task{T}}, CancellationToken)"/>
    /// with <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <returns>The operation's result on success; the last failure is rethrown after <see cref="MaxRetries"/> attempts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        => ExecuteAsync(operation, CancellationToken.None);

    /// <summary>
    /// Executes operation with retry logic and cancellation support. The token is observed
    /// before the first attempt and during each backoff <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <param name="cancellationToken">Token to abort retries.</param>
    /// <returns>The operation's result on success; the last failure is rethrown after <see cref="MaxRetries"/> attempts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        int retryCount = 0;
        TimeSpan delay = InitialDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception) when (retryCount < MaxRetries)
            {
                retryCount++;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                if (UseExponentialBackoff)
                {
                    delay = TimeSpan.FromMilliseconds(Math.Min(
                        delay.TotalMilliseconds * BackoffMultiplier,
                        MaxDelay.TotalMilliseconds));
                }

                if (retryCount >= MaxRetries)
                    throw;
            }
        }
    }
}
