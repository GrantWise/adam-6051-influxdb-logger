// Industrial.Adam.Logger - Retry Policy Service Implementation
// Concrete implementation of retry logic with configurable policies

using System.Net.Sockets;
using Industrial.Adam.Logger.Interfaces;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Utilities;

/// <summary>
/// Service for executing operations with configurable retry policies
/// </summary>
public class RetryPolicyService : IRetryPolicyService
{
    private readonly ILogger<RetryPolicyService> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initialize retry policy service
    /// </summary>
    /// <param name="logger">Logger for retry operations</param>
    public RetryPolicyService(ILogger<RetryPolicyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Execute an operation with retry logic
    /// </summary>
    /// <typeparam name="T">Type of the operation result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    public async Task<OperationResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (policy == null) throw new ArgumentNullException(nameof(policy));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var context = new Dictionary<string, object>
        {
            ["MaxAttempts"] = policy.MaxAttempts,
            ["Strategy"] = policy.Strategy.ToString()
        };

        Exception? lastException = null;
        var attempt = 0;

        while (attempt <= policy.MaxAttempts)
        {
            try
            {
                _logger.LogDebug("Executing operation, attempt {Attempt}/{MaxAttempts}", 
                    attempt + 1, policy.MaxAttempts + 1);

                var result = await operation(cancellationToken);
                
                stopwatch.Stop();
                context["ActualAttempts"] = attempt + 1;
                context["Success"] = true;

                _logger.LogDebug("Operation succeeded on attempt {Attempt}", attempt + 1);
                
                return OperationResult<T>.Success(result, stopwatch.Elapsed, context);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                context["ActualAttempts"] = attempt + 1;
                context["CancelledOnAttempt"] = attempt + 1;
                
                _logger.LogDebug("Operation cancelled on attempt {Attempt}", attempt + 1);
                
                return OperationResult<T>.Failure(ex, stopwatch.Elapsed, context);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, policy.MaxAttempts + 1, ex.Message);

                // Check if we should retry this exception
                if (!policy.ShouldRetry(ex))
                {
                    _logger.LogDebug("Exception type {ExceptionType} is not retryable", ex.GetType().Name);
                    break;
                }

                // Check if we've exhausted all attempts
                if (attempt > policy.MaxAttempts)
                {
                    _logger.LogDebug("Exhausted all retry attempts");
                    break;
                }

                // Calculate delay for next attempt
                var delay = CalculateDelay(policy, attempt);
                
                _logger.LogDebug("Waiting {DelayMs}ms before retry attempt {NextAttempt}", 
                    delay.TotalMilliseconds, attempt + 1);

                // Execute retry callback if provided
                policy.OnRetry?.Invoke(attempt, ex, delay);

                // Wait before next attempt
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    context["ActualAttempts"] = attempt;
                    context["CancelledDuringDelay"] = true;
                    
                    _logger.LogDebug("Operation cancelled during retry delay");
                    
                    return OperationResult<T>.Failure(lastException, stopwatch.Elapsed, context);
                }
            }
        }

        stopwatch.Stop();
        context["ActualAttempts"] = attempt;
        context["Success"] = false;

        _logger.LogError(lastException, "Operation failed after {Attempts} attempts", attempt);
        
        return OperationResult<T>.Failure(lastException!, stopwatch.Elapsed, context);
    }

    /// <summary>
    /// Execute an operation that doesn't return a value with retry logic
    /// </summary>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    public async Task<OperationResult> ExecuteAsync(
        Func<CancellationToken, Task> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (policy == null) throw new ArgumentNullException(nameof(policy));

        // Wrap the void operation to return a dummy value
        var result = await ExecuteAsync(async ct =>
        {
            await operation(ct);
            return true; // Dummy return value
        }, policy, cancellationToken);

        // Convert to non-generic OperationResult
        return result.IsSuccess
            ? OperationResult.Success(result.Duration, result.Context)
            : OperationResult.Failure(result.Error!, result.Duration, result.Context);
    }

    /// <summary>
    /// Execute a synchronous operation with retry logic
    /// </summary>
    /// <typeparam name="T">Type of the operation result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="policy">Retry policy to use</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result of the operation with retry attempts</returns>
    public async Task<OperationResult<T>> ExecuteAsync<T>(
        Func<T> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (policy == null) throw new ArgumentNullException(nameof(policy));

        // Wrap the synchronous operation in a task
        return await ExecuteAsync(_ => Task.FromResult(operation()), policy, cancellationToken);
    }

    /// <summary>
    /// Create a default retry policy for device operations
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay between retries</param>
    /// <param name="maxDelay">Maximum delay between retries</param>
    /// <returns>Configured retry policy</returns>
    public RetryPolicy CreateDeviceRetryPolicy(
        int maxAttempts = Constants.DefaultMaxRetries,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        return new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            BaseDelay = baseDelay ?? TimeSpan.FromMilliseconds(Constants.DefaultRetryDelayMs),
            MaxDelay = maxDelay ?? TimeSpan.FromSeconds(Constants.MaxRetryDelaySeconds),
            Strategy = RetryStrategy.ExponentialBackoff,
            JitterFactor = Constants.DefaultJitterFactor,
            ShouldRetry = IsDeviceRetryableException,
            OnRetry = (attempt, exception, delay) =>
            {
                _logger.LogInformation(
                    "Device operation retry {Attempt}: {ExceptionType} - {Message}. Next attempt in {DelayMs}ms",
                    attempt, exception.GetType().Name, exception.Message, delay.TotalMilliseconds);
            }
        };
    }

    /// <summary>
    /// Create a retry policy for network operations
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="baseDelay">Base delay between retries</param>
    /// <returns>Configured retry policy</returns>
    public RetryPolicy CreateNetworkRetryPolicy(
        int maxAttempts = 5,
        TimeSpan? baseDelay = null)
    {
        return new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            BaseDelay = baseDelay ?? TimeSpan.FromMilliseconds(Constants.NetworkRetryDelayMs),
            MaxDelay = TimeSpan.FromSeconds(Constants.MaxNetworkRetryDelaySeconds),
            Strategy = RetryStrategy.ExponentialBackoff,
            JitterFactor = Constants.DefaultJitterFactor,
            ShouldRetry = IsNetworkRetryableException,
            OnRetry = (attempt, exception, delay) =>
            {
                _logger.LogWarning(
                    "Network operation retry {Attempt}: {ExceptionType} - {Message}. Next attempt in {DelayMs}ms",
                    attempt, exception.GetType().Name, exception.Message, delay.TotalMilliseconds);
            }
        };
    }

    /// <summary>
    /// Calculate the delay for the next retry attempt
    /// </summary>
    /// <param name="policy">Retry policy configuration</param>
    /// <param name="attempt">Current attempt number (1-based)</param>
    /// <returns>Delay to wait before next attempt</returns>
    private TimeSpan CalculateDelay(RetryPolicy policy, int attempt)
    {
        var baseDelayMs = policy.BaseDelay.TotalMilliseconds;
        var maxDelayMs = policy.MaxDelay.TotalMilliseconds;

        double delayMs = policy.Strategy switch
        {
            RetryStrategy.FixedDelay => baseDelayMs,
            RetryStrategy.ExponentialBackoff => Math.Min(baseDelayMs * Math.Pow(2, attempt - 1), maxDelayMs),
            RetryStrategy.LinearBackoff => Math.Min(baseDelayMs * attempt, maxDelayMs),
            _ => baseDelayMs
        };

        // Add jitter to prevent thundering herd
        if (policy.JitterFactor > 0)
        {
            var jitterRange = delayMs * policy.JitterFactor;
            var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange; // Range: -jitterRange to +jitterRange
            delayMs = Math.Max(0, delayMs + jitter);
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Determine if an exception should trigger a retry for device operations
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    private static bool IsDeviceRetryableException(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            SocketException socketEx => IsRetryableSocketException(socketEx),
            HttpRequestException => true,
            TaskCanceledException { CancellationToken.IsCancellationRequested: false } => true,
            InvalidOperationException when exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) => true,
            InvalidOperationException when exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    /// <summary>
    /// Determine if an exception should trigger a retry for network operations
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    private static bool IsNetworkRetryableException(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            SocketException socketEx => IsRetryableSocketException(socketEx),
            HttpRequestException httpEx when IsRetryableHttpException(httpEx) => true,
            TaskCanceledException { CancellationToken.IsCancellationRequested: false } => true,
            _ => false
        };
    }

    /// <summary>
    /// Determine if a socket exception should trigger a retry
    /// </summary>
    /// <param name="socketException">Socket exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    private static bool IsRetryableSocketException(SocketException socketException)
    {
        return socketException.SocketErrorCode switch
        {
            SocketError.TimedOut => true,
            SocketError.ConnectionRefused => true,
            SocketError.ConnectionReset => true,
            SocketError.ConnectionAborted => true,
            SocketError.NetworkDown => true,
            SocketError.NetworkUnreachable => true,
            SocketError.HostDown => true,
            SocketError.HostUnreachable => true,
            SocketError.TryAgain => true,
            _ => false
        };
    }

    /// <summary>
    /// Determine if an HTTP exception should trigger a retry
    /// </summary>
    /// <param name="httpException">HTTP exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    private static bool IsRetryableHttpException(HttpRequestException httpException)
    {
        var message = httpException.Message.ToLowerInvariant();
        
        return message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("network") ||
               message.Contains("unreachable") ||
               message.Contains("refused");
    }
}