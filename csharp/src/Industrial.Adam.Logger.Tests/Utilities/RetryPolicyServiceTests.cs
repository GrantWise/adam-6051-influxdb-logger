// Industrial.Adam.Logger.Tests - RetryPolicyService Unit Tests
// Comprehensive tests for retry policy implementation (25 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Utilities;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Sockets;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Utilities;

/// <summary>
/// Unit tests for RetryPolicyService (25 tests planned)
/// </summary>
public class RetryPolicyServiceTests
{
    #region Constructor Tests (2 tests)

    [Fact]
    public void Constructor_ValidLogger_ShouldCreateInstance()
    {
        // Arrange
        var mockLogger = TestMockFactory.CreateMockLogger<RetryPolicyService>();

        // Act
        var service = new RetryPolicyService(mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action createWithNullLogger = () => new RetryPolicyService(null!);
        createWithNullLogger.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ExecuteAsync Generic Success Tests (3 tests)

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));
        var expectedResult = "success";

        // Act
        var result = await service.ExecuteAsync(
            async _ => 
            {
                await Task.Delay(1);
                return expectedResult;
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperationWithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));
        using var cts = new CancellationTokenSource();

        // Act
        var task = service.ExecuteAsync(
            async token =>
            {
                await Task.Delay(100, token);
                return "should not complete";
            },
            policy,
            cts.Token);

        cts.Cancel();
        var result = await task;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ShouldNotRetry()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                return "success";
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(1);
    }

    #endregion

    #region ExecuteAsync Generic Retry Tests (5 tests)

    [Fact]
    public async Task ExecuteAsync_FailsOnFirstAttemptSucceedsOnSecond_ShouldRetryAndSucceed()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                if (callCount == 1)
                    throw new SocketException();
                return "success";
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxAttempts_ShouldReturnFailure()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(2, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                throw new SocketException(10054); // Connection reset by peer
            },
            policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<SocketException>();
        callCount.Should().Be(3); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ShouldNotRetry()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = new RetryPolicy
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(10),
            ShouldRetry = ex => ex is SocketException // Only retry socket exceptions
        };
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                throw new ArgumentException("Invalid argument");
            },
            policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ArgumentException>();
        callCount.Should().Be(1); // No retries for non-retryable exception
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_ShouldIncreaseDelays()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.ExponentialBackoff(3, TimeSpan.FromMilliseconds(10));
        var timestamps = new List<DateTime>();

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                timestamps.Add(DateTime.UtcNow);
                await Task.Delay(1);
                throw new SocketException();
            },
            policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        timestamps.Should().HaveCount(4); // Initial + 3 retries

        // Check that delays increase (allowing for some timing variance)
        var delay1 = timestamps[1] - timestamps[0];
        var delay2 = timestamps[2] - timestamps[1];
        var delay3 = timestamps[3] - timestamps[2];

        delay2.Should().BeGreaterThan(delay1);
        delay3.Should().BeGreaterThan(delay2);
    }

    [Fact]
    public async Task ExecuteAsync_LinearBackoff_ShouldIncreaseDelaysLinearly()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.LinearBackoff(3, TimeSpan.FromMilliseconds(20));
        var timestamps = new List<DateTime>();

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                timestamps.Add(DateTime.UtcNow);
                await Task.Delay(1);
                throw new SocketException();
            },
            policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        timestamps.Should().HaveCount(4); // Initial + 3 retries

        // Check that delays increase linearly (allowing for timing variance)
        var delay1 = timestamps[1] - timestamps[0];
        var delay2 = timestamps[2] - timestamps[1];
        var delay3 = timestamps[3] - timestamps[2];

        delay2.Should().BeGreaterThan(delay1);
        delay3.Should().BeGreaterThan(delay2);
        // In linear backoff, delay3 should be approximately 3x delay1
    }

    #endregion

    #region ExecuteAsync Non-Generic Tests (3 tests)

    [Fact]
    public async Task ExecuteAsync_NonGeneric_SuccessfulOperation_ShouldReturnSuccess()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));
        var operationExecuted = false;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                await Task.Delay(1);
                operationExecuted = true;
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operationExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_FailsAndRetries_ShouldRetryCorrectly()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(2, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                if (callCount <= 2)
                    throw new SocketException();
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_ExceedsMaxAttempts_ShouldReturnFailure()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(1, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async _ =>
            {
                callCount++;
                await Task.Delay(1);
                throw new TimeoutException("Operation timed out");
            },
            policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<TimeoutException>();
        callCount.Should().Be(2); // Initial + 1 retry
    }

    #endregion

    #region ExecuteAsync Synchronous Tests (2 tests)

    [Fact]
    public async Task ExecuteAsync_SynchronousOperation_ShouldWork()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromMilliseconds(10));

        // Act
        var result = await service.ExecuteAsync<string>(
            () => "sync result",
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("sync result");
    }

    [Fact]
    public async Task ExecuteAsync_SynchronousOperationWithRetries_ShouldRetry()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(2, TimeSpan.FromMilliseconds(10));
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync<string>(
            () =>
            {
                callCount++;
                if (callCount <= 1)
                    throw new SocketException();
                return "sync success";
            },
            policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("sync success");
        callCount.Should().Be(2);
    }

    #endregion

    #region Policy Factory Tests (4 tests)

    [Fact]
    public void CreateDeviceRetryPolicy_DefaultParameters_ShouldCreateValidPolicy()
    {
        // Arrange
        var service = CreateRetryPolicyService();

        // Act
        var policy = service.CreateDeviceRetryPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.MaxAttempts.Should().Be(Constants.DefaultMaxRetries);
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
    }

    [Fact]
    public void CreateDeviceRetryPolicy_CustomParameters_ShouldUseCustomValues()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var baseDelay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromSeconds(60);

        // Act
        var policy = service.CreateDeviceRetryPolicy(5, baseDelay, maxDelay);

        // Assert
        policy.MaxAttempts.Should().Be(5);
        policy.BaseDelay.Should().Be(baseDelay);
        policy.MaxDelay.Should().Be(maxDelay);
    }

    [Fact]
    public void CreateNetworkRetryPolicy_DefaultParameters_ShouldCreateValidPolicy()
    {
        // Arrange
        var service = CreateRetryPolicyService();

        // Act
        var policy = service.CreateNetworkRetryPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.MaxAttempts.Should().Be(5);
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
    }

    [Fact]
    public void CreateNetworkRetryPolicy_CustomParameters_ShouldUseCustomValues()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var baseDelay = TimeSpan.FromMilliseconds(500);

        // Act
        var policy = service.CreateNetworkRetryPolicy(3, baseDelay);

        // Assert
        policy.MaxAttempts.Should().Be(3);
        policy.BaseDelay.Should().Be(baseDelay);
    }

    #endregion

    #region RetryPolicy Static Factory Tests (4 tests)

    [Fact]
    public void RetryPolicy_FixedDelay_ShouldCreateCorrectPolicy()
    {
        // Arrange & Act
        var policy = RetryPolicy.FixedDelay(3, TimeSpan.FromSeconds(1));

        // Assert
        policy.MaxAttempts.Should().Be(3);
        policy.BaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(1));
        policy.Strategy.Should().Be(RetryStrategy.FixedDelay);
        policy.JitterFactor.Should().Be(0);
    }

    [Fact]
    public void RetryPolicy_ExponentialBackoff_ShouldCreateCorrectPolicy()
    {
        // Arrange & Act
        var policy = RetryPolicy.ExponentialBackoff(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10));

        // Assert
        policy.MaxAttempts.Should().Be(5);
        policy.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(10));
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
        policy.JitterFactor.Should().Be(0.1);
    }

    [Fact]
    public void RetryPolicy_LinearBackoff_ShouldCreateCorrectPolicy()
    {
        // Arrange & Act
        var policy = RetryPolicy.LinearBackoff(4, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5));

        // Assert
        policy.MaxAttempts.Should().Be(4);
        policy.BaseDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(5));
        policy.Strategy.Should().Be(RetryStrategy.LinearBackoff);
        policy.JitterFactor.Should().Be(0.1);
    }

    [Fact]
    public void RetryPolicy_DefaultShouldRetry_ShouldHandleCommonExceptions()
    {
        // Arrange & Act & Assert
        RetryPolicy.DefaultShouldRetry(new TimeoutException()).Should().BeTrue();
        RetryPolicy.DefaultShouldRetry(new SocketException()).Should().BeTrue();
        RetryPolicy.DefaultShouldRetry(new HttpRequestException()).Should().BeTrue();
        RetryPolicy.DefaultShouldRetry(new TaskCanceledException()).Should().BeTrue();

        RetryPolicy.DefaultShouldRetry(new ArgumentException()).Should().BeFalse();
        RetryPolicy.DefaultShouldRetry(new InvalidOperationException()).Should().BeFalse();
        RetryPolicy.DefaultShouldRetry(new NotSupportedException()).Should().BeFalse();
    }

    #endregion

    #region Parameter Validation Tests (2 tests)

    [Fact]
    public async Task ExecuteAsync_NullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateRetryPolicyService();
        var policy = RetryPolicy.FixedDelay(1, TimeSpan.FromMilliseconds(10));

        // Act & Assert
        await service.Invoking(async s => await s.ExecuteAsync((Func<CancellationToken, Task<string>>)null!, policy))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullPolicy_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateRetryPolicyService();

        // Act & Assert
        await service.Invoking(async s => await s.ExecuteAsync(async _ => 
        {
            await Task.Delay(1);
            return "test";
        }, null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create a RetryPolicyService with mocked logger
    /// </summary>
    private static RetryPolicyService CreateRetryPolicyService()
    {
        var mockLogger = TestMockFactory.CreateMockLogger<RetryPolicyService>();
        return new RetryPolicyService(mockLogger.Object);
    }

    #endregion
}