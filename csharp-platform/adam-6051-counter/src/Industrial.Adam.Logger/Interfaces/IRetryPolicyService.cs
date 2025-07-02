// Industrial.Adam.Logger - Retry Policy Service Interface
// Interface for implementing retry logic with configurable policies

using System.Net.Sockets;
using Industrial.Adam.Logger.Utilities;

namespace Industrial.Adam.Logger.Interfaces;

/// <summary>
/// Service for executing operations with configurable retry policies
/// </summary>
public interface IRetryPolicyService
{
    /// <summary>
    /// Execute an operation with retry logic
    /// </summary>
    /// <typeparam name="T">Type of the operation result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    Task<OperationResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an operation that doesn't return a value with retry logic
    /// </summary>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    Task<OperationResult> ExecuteAsync(
        Func<CancellationToken, Task> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a synchronous operation with retry logic
    /// </summary>
    /// <typeparam name="T">Type of the operation result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    Task<OperationResult<T>> ExecuteAsync<T>(
        Func<T> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a default retry policy for device operations
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay between retries</param>
    /// <param name="maxDelay">Maximum delay between retries</param>
    /// <returns>Configured retry policy</returns>
    RetryPolicy CreateDeviceRetryPolicy(
        int maxAttempts = Constants.DefaultMaxRetries,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null);

    /// <summary>
    /// Create a retry policy for network operations
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay between retries</param>
    /// <returns>Configured retry policy</returns>
    RetryPolicy CreateNetworkRetryPolicy(
        int maxAttempts = 5,
        TimeSpan? baseDelay = null);
}

/// <summary>
/// Configuration for retry behavior
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts (0 means no retries)
    /// </summary>
    public int MaxAttempts { get; set; } = Constants.DefaultMaxRetries;

    /// <summary>
    /// Base delay between retry attempts
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(Constants.DefaultRetryDelayMs);

    /// <summary>
    /// Maximum delay between retry attempts
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Strategy for calculating delay between retries
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;

    /// <summary>
    /// Jitter factor to add randomness to retry delays (0.0 to 1.0)
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Predicate to determine if an exception should trigger a retry
    /// </summary>
    public Func<Exception, bool> ShouldRetry { get; set; } = DefaultShouldRetry;

    /// <summary>
    /// Action to execute before each retry attempt
    /// </summary>
    public Action<int, Exception, TimeSpan>? OnRetry { get; set; }

    /// <summary>
    /// Default logic for determining if an exception should trigger a retry
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    public static bool DefaultShouldRetry(Exception exception)
    {
        // Retry on common transient failures
        return exception is TimeoutException or
               SocketException or
               HttpRequestException or
               TaskCanceledException { CancellationToken.IsCancellationRequested: false };
    }

    /// <summary>
    /// Create a simple retry policy with fixed delay
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="delay">Fixed delay between retries</param>
    /// <returns>Configured retry policy</returns>
    public static RetryPolicy FixedDelay(int maxAttempts, TimeSpan delay)
    {
        return new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            BaseDelay = delay,
            MaxDelay = delay,
            Strategy = RetryStrategy.FixedDelay,
            JitterFactor = 0
        };
    }

    /// <summary>
    /// Create an exponential backoff retry policy
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay for exponential calculation</param>
    /// <param name="maxDelay">Maximum delay between retries</param>
    /// <returns>Configured retry policy</returns>
    public static RetryPolicy ExponentialBackoff(int maxAttempts, TimeSpan baseDelay, TimeSpan? maxDelay = null)
    {
        return new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            BaseDelay = baseDelay,
            MaxDelay = maxDelay ?? TimeSpan.FromSeconds(30),
            Strategy = RetryStrategy.ExponentialBackoff,
            JitterFactor = 0.1
        };
    }

    /// <summary>
    /// Create a linear backoff retry policy
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay for linear calculation</param>
    /// <param name="maxDelay">Maximum delay between retries</param>
    /// <returns>Configured retry policy</returns>
    public static RetryPolicy LinearBackoff(int maxAttempts, TimeSpan baseDelay, TimeSpan? maxDelay = null)
    {
        return new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            BaseDelay = baseDelay,
            MaxDelay = maxDelay ?? TimeSpan.FromSeconds(30),
            Strategy = RetryStrategy.LinearBackoff,
            JitterFactor = 0.1
        };
    }
}

/// <summary>
/// Strategy for calculating delay between retry attempts
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Fixed delay between all retry attempts
    /// </summary>
    FixedDelay,

    /// <summary>
    /// Exponentially increasing delay (baseDelay * 2^attempt)
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Linearly increasing delay (baseDelay * attempt)
    /// </summary>
    LinearBackoff
}