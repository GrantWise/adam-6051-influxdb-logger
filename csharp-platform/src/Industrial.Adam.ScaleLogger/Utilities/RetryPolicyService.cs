// Industrial.Adam.ScaleLogger - Retry Policy Service
// Following proven ADAM-6051 retry patterns for industrial reliability

using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Utilities;

/// <summary>
/// Retry policy service following proven ADAM-6051 patterns
/// Provides exponential backoff and industrial-grade error handling
/// </summary>
public sealed class RetryPolicyService
{
    private readonly ILogger<RetryPolicyService> _logger;

    public RetryPolicyService(ILogger<RetryPolicyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute operation with retry policy
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan baseDelay = default,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (baseDelay == default) baseDelay = TimeSpan.FromSeconds(1);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation succeeded on attempt {Attempt}/{MaxAttempts}", 
                        attempt, maxAttempts);
                }
                
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Operation cancelled during retry attempt {Attempt}", attempt);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == maxAttempts)
                {
                    _logger.LogError(ex, "Operation failed after {MaxAttempts} attempts", maxAttempts);
                    break;
                }

                var delay = CalculateDelay(attempt, baseDelay);
                _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms", 
                    attempt, maxAttempts, delay.TotalMilliseconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Retry delay cancelled");
                    throw;
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed with unknown error");
    }

    /// <summary>
    /// Execute operation with retry policy (void return)
    /// </summary>
    public async Task ExecuteAsync(
        Func<Task> operation,
        int maxAttempts = 3,
        TimeSpan baseDelay = default,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, maxAttempts, baseDelay, cancellationToken);
    }

    /// <summary>
    /// Execute operation with custom retry condition
    /// </summary>
    public async Task<T> ExecuteWithConditionAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        int maxAttempts = 3,
        TimeSpan baseDelay = default,
        CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (shouldRetry == null) throw new ArgumentNullException(nameof(shouldRetry));
        if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (baseDelay == default) baseDelay = TimeSpan.FromSeconds(1);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation succeeded on attempt {Attempt}/{MaxAttempts}", 
                        attempt, maxAttempts);
                }
                
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Operation cancelled during retry attempt {Attempt}", attempt);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == maxAttempts || !shouldRetry(ex))
                {
                    if (attempt == maxAttempts)
                    {
                        _logger.LogError(ex, "Operation failed after {MaxAttempts} attempts", maxAttempts);
                    }
                    else
                    {
                        _logger.LogError(ex, "Operation failed with non-retryable error on attempt {Attempt}", attempt);
                    }
                    break;
                }

                var delay = CalculateDelay(attempt, baseDelay);
                _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms", 
                    attempt, maxAttempts, delay.TotalMilliseconds);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Retry delay cancelled");
                    throw;
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed with unknown error");
    }

    /// <summary>
    /// Calculate exponential backoff delay with jitter
    /// </summary>
    private static TimeSpan CalculateDelay(int attempt, TimeSpan baseDelay)
    {
        // Exponential backoff: baseDelay * 2^(attempt-1)
        var exponentialDelay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        // Add jitter (±20%) to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2; // -20% to +20%
        var jitteredDelay = TimeSpan.FromMilliseconds(
            exponentialDelay.TotalMilliseconds * (1 + jitter));

        // Cap at maximum delay
        var maxDelay = TimeSpan.FromMinutes(1);
        return jitteredDelay > maxDelay ? maxDelay : jitteredDelay;
    }

    /// <summary>
    /// Common retry conditions for industrial operations
    /// </summary>
    public static class RetryConditions
    {
        /// <summary>
        /// Retry on network-related exceptions
        /// </summary>
        public static bool NetworkErrors(Exception ex)
        {
            return ex is System.Net.Sockets.SocketException ||
                   ex is System.Net.NetworkInformation.NetworkInformationException ||
                   ex is TimeoutException ||
                   (ex is System.IO.IOException && ex.Message.Contains("timeout"));
        }

        /// <summary>
        /// Retry on transient errors but not configuration errors
        /// </summary>
        public static bool TransientErrors(Exception ex)
        {
            return NetworkErrors(ex) ||
                   ex is InvalidOperationException ||
                   (ex is ArgumentException && ex.Message.Contains("connection"));
        }

        /// <summary>
        /// Don't retry on configuration or authentication errors
        /// </summary>
        public static bool NotConfigurationErrors(Exception ex)
        {
            return !(ex is ArgumentNullException ||
                     ex is InvalidOperationException ||
                     ex is UnauthorizedAccessException ||
                     ex is System.Security.SecurityException);
        }
    }
}