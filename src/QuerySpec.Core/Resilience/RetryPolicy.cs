using System;
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
    /// Executes operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        int retryCount = 0;
        TimeSpan delay = InitialDelay;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception) when (retryCount < MaxRetries)
            {
                retryCount++;
                await Task.Delay(delay);

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
