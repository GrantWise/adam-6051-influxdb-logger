// Industrial.Adam.Logger.Tests - OperationResult Unit Tests
// Comprehensive tests for the Result pattern implementation

using FluentAssertions;
using Industrial.Adam.Logger.Utilities;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Utilities;

/// <summary>
/// Unit tests for OperationResult pattern implementation
/// </summary>
public class OperationResultTests
{
    #region Generic OperationResult<T> Success Tests

    [Fact]
    public void Success_WithValue_ShouldCreateSuccessResult()
    {
        // Arrange
        const string expectedValue = "test value";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = OperationResult<string>.Success(expectedValue, duration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(expectedValue);
        result.Duration.Should().Be(duration);
        result.Error.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Success_WithContext_ShouldPreserveContext()
    {
        // Arrange
        const int expectedValue = 42;
        var context = new Dictionary<string, object>
        {
            { "operation", "test" },
            { "attempt", 1 }
        };

        // Act
        var result = OperationResult<int>.Success(expectedValue, context: context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
        result.Context.Should().BeEquivalentTo(context);
    }

    [Fact]
    public void Success_WithDuration_ShouldTrackTiming()
    {
        // Arrange
        const double expectedValue = 123.45;
        var duration = TimeSpan.FromSeconds(2.5);

        // Act
        var result = OperationResult<double>.Success(expectedValue, duration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void GetValueOrDefault_Success_ShouldReturnValue()
    {
        // Arrange
        const string expectedValue = "actual value";
        const string defaultValue = "default value";
        var result = OperationResult<string>.Success(expectedValue);

        // Act
        var actualValue = result.GetValueOrDefault(defaultValue);

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void OnSuccess_Success_ShouldExecuteAction()
    {
        // Arrange
        const int expectedValue = 100;
        var result = OperationResult<int>.Success(expectedValue);
        var actionExecuted = false;
        var capturedValue = 0;

        // Act
        var returnedResult = result.OnSuccess(value =>
        {
            actionExecuted = true;
            capturedValue = value;
        });

        // Assert
        actionExecuted.Should().BeTrue();
        capturedValue.Should().Be(expectedValue);
        returnedResult.Should().Be(result); // Should return same instance
    }

    [Fact]
    public void Map_Success_ShouldTransformValue()
    {
        // Arrange
        const int originalValue = 42;
        var result = OperationResult<int>.Success(originalValue);

        // Act
        var mappedResult = result.Map(x => x.ToString());

        // Assert
        mappedResult.IsSuccess.Should().BeTrue();
        mappedResult.Value.Should().Be("42");
        mappedResult.Duration.Should().Be(result.Duration);
        mappedResult.Context.Should().BeEquivalentTo(result.Context);
    }

    #endregion

    #region Generic OperationResult<T> Failure Tests

    [Fact]
    public void Failure_WithException_ShouldCreateFailureResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result = OperationResult<string>.Failure(exception, duration);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(exception);
        result.ErrorMessage.Should().Be(exception.Message);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void Failure_WithMessage_ShouldCreateFailureResult()
    {
        // Arrange
        const string errorMessage = "Custom error message";
        var duration = TimeSpan.FromMilliseconds(25);

        // Act
        var result = OperationResult<int>.Failure(errorMessage, duration);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeNull();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void Failure_WithContext_ShouldPreserveContext()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");
        var context = new Dictionary<string, object>
        {
            { "timeout_ms", 5000 },
            { "retry_count", 3 }
        };

        // Act
        var result = OperationResult<bool>.Failure(exception, context: context);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(exception);
        result.Context.Should().BeEquivalentTo(context);
    }

    [Fact]
    public void GetValueOrDefault_Failure_ShouldReturnDefault()
    {
        // Arrange
        const string defaultValue = "default value";
        var result = OperationResult<string>.Failure("Operation failed");

        // Act
        var actualValue = result.GetValueOrDefault(defaultValue);

        // Assert
        actualValue.Should().Be(defaultValue);
    }

    [Fact]
    public void OnFailure_Failure_ShouldExecuteAction()
    {
        // Arrange
        const string errorMessage = "Test failure";
        var result = OperationResult<int>.Failure(errorMessage);
        var actionExecuted = false;
        var capturedMessage = string.Empty;

        // Act
        var returnedResult = result.OnFailure(message =>
        {
            actionExecuted = true;
            capturedMessage = message;
        });

        // Assert
        actionExecuted.Should().BeTrue();
        capturedMessage.Should().Be(errorMessage);
        returnedResult.Should().Be(result); // Should return same instance
    }

    [Fact]
    public void Map_Failure_ShouldPreserveFailure()
    {
        // Arrange
        const string errorMessage = "Original failure";
        var result = OperationResult<int>.Failure(errorMessage);

        // Act
        var mappedResult = result.Map(x => x.ToString());

        // Assert
        mappedResult.IsFailure.Should().BeTrue();
        mappedResult.ErrorMessage.Should().Be(errorMessage);
        mappedResult.Duration.Should().Be(result.Duration);
        mappedResult.Context.Should().BeEquivalentTo(result.Context);
    }

    [Fact]
    public void Value_Failure_ShouldThrowInvalidOperation()
    {
        // Arrange
        const string errorMessage = "Cannot access value";
        var result = OperationResult<string>.Failure(errorMessage);

        // Act & Assert
        Action accessValue = () => _ = result.Value;
        accessValue.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot access Value when operation failed: {errorMessage}");
    }

    [Fact]
    public void ToString_Failure_ShouldDisplayError()
    {
        // Arrange
        const string errorMessage = "Test error message";
        var duration = TimeSpan.FromMilliseconds(100);
        var result = OperationResult<int>.Failure(errorMessage, duration);

        // Act
        var stringRepresentation = result.ToString();

        // Assert
        stringRepresentation.Should().Contain("Failure");
        stringRepresentation.Should().Contain(errorMessage);
        stringRepresentation.Should().Contain("100.00ms");
    }

    #endregion

    #region Non-Generic OperationResult Success Tests

    [Fact]
    public void NonGeneric_Success_ShouldWork()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(75);

        // Act
        var result = OperationResult.Success(duration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Duration.Should().Be(duration);
        result.Error.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void NonGeneric_Success_WithContext_ShouldPreserveContext()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            { "operation_type", "cleanup" },
            { "items_processed", 150 }
        };

        // Act
        var result = OperationResult.Success(context: context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Context.Should().BeEquivalentTo(context);
    }

    #endregion

    #region Non-Generic OperationResult Failure Tests

    [Fact]
    public void NonGeneric_Failure_ShouldWork()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var duration = TimeSpan.FromMilliseconds(10);

        // Act
        var result = OperationResult.Failure(exception, duration);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(exception);
        result.ErrorMessage.Should().Be(exception.Message);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void NonGeneric_Failure_WithMessage_ShouldWork()
    {
        // Arrange
        const string errorMessage = "Operation not supported";

        // Act
        var result = OperationResult.Failure(errorMessage);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeNull();
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void NonGeneric_ToString_ShouldDisplay()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(200);
        var successResult = OperationResult.Success(duration);
        var failureResult = OperationResult.Failure("Test failure", duration);

        // Act
        var successString = successResult.ToString();
        var failureString = failureResult.ToString();

        // Assert
        successString.Should().Contain("Success");
        successString.Should().Contain("200.00ms");
        
        failureString.Should().Contain("Failure");
        failureString.Should().Contain("Test failure");
        failureString.Should().Contain("200.00ms");
    }

    #endregion

    #region Map Error Handling Tests

    [Fact]
    public void Map_ThrowsException_ShouldCreateFailureResult()
    {
        // Arrange
        const int originalValue = 10;
        var result = OperationResult<int>.Success(originalValue);

        // Act
        var mappedResult = result.Map<string>(_ => throw new DivideByZeroException("Test exception"));

        // Assert
        mappedResult.IsFailure.Should().BeTrue();
        mappedResult.Error.Should().BeOfType<DivideByZeroException>();
        mappedResult.ErrorMessage.Should().Be("Test exception");
        mappedResult.Duration.Should().Be(result.Duration);
        mappedResult.Context.Should().BeEquivalentTo(result.Context);
    }

    #endregion

    #region OnSuccess/OnFailure Chaining Tests

    [Fact]
    public void OnSuccess_Failure_ShouldNotExecuteAction()
    {
        // Arrange
        var result = OperationResult<int>.Failure("Test failure");
        var actionExecuted = false;

        // Act
        result.OnSuccess(_ => actionExecuted = true);

        // Assert
        actionExecuted.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_Success_ShouldNotExecuteAction()
    {
        // Arrange
        var result = OperationResult<int>.Success(42);
        var actionExecuted = false;

        // Act
        result.OnFailure(_ => actionExecuted = true);

        // Assert
        actionExecuted.Should().BeFalse();
    }

    [Fact]
    public void ChainedOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var result = OperationResult<int>.Success(10);
        var successActionExecuted = false;
        var failureActionExecuted = false;

        // Act
        var finalResult = result
            .OnSuccess(value => successActionExecuted = true)
            .OnFailure(error => failureActionExecuted = true);

        // Assert
        successActionExecuted.Should().BeTrue();
        failureActionExecuted.Should().BeFalse();
        finalResult.Should().Be(result);
    }

    #endregion

    #region Null Parameter Tests

    [Fact]
    public void Success_NullValue_ShouldWork()
    {
        // Arrange & Act
        var result = OperationResult<string?>.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Failure_NullException_ShouldThrowArgumentNull()
    {
        // Arrange & Act & Assert
        Action createResult = () => OperationResult<int>.Failure((Exception)null!);
        createResult.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_NullMessage_ShouldThrowArgumentNull()
    {
        // Arrange & Act & Assert
        Action createResult = () => OperationResult<int>.Failure((string)null!);
        createResult.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_NullTransform_ShouldThrowArgumentNull()
    {
        // Arrange
        var result = OperationResult<int>.Success(42);

        // Act & Assert
        Action mapWithNull = () => result.Map<string>(null!);
        mapWithNull.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnSuccess_NullAction_ShouldThrowArgumentNull()
    {
        // Arrange
        var result = OperationResult<int>.Success(42);

        // Act & Assert
        Action onSuccessWithNull = () => result.OnSuccess(null!);
        onSuccessWithNull.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnFailure_NullAction_ShouldThrowArgumentNull()
    {
        // Arrange
        var result = OperationResult<int>.Failure("Test error");

        // Act & Assert
        Action onFailureWithNull = () => result.OnFailure(null!);
        onFailureWithNull.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Context Preservation Tests

    [Fact]
    public void Context_ShouldBePreservedThroughTransformations()
    {
        // Arrange
        var originalContext = new Dictionary<string, object>
        {
            { "trace_id", Guid.NewGuid().ToString() },
            { "operation_name", "test_operation" },
            { "start_time", DateTimeOffset.UtcNow }
        };
        
        var result = OperationResult<int>.Success(42, context: originalContext);

        // Act
        var mappedResult = result.Map(x => x * 2);

        // Assert
        mappedResult.Context.Should().BeEquivalentTo(originalContext);
        mappedResult.Context.Should().NotBeSameAs(originalContext); // Should be a copy
    }

    [Fact]
    public void Context_ShouldBeImmutable()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            { "initial_value", "test" }
        };
        
        var result = OperationResult<string>.Success("test", context: context);

        // Act
        result.Context.Add("new_value", "modified");

        // Assert - The addition should not affect the original context
        context.Should().NotContainKey("new_value");
        context.Should().HaveCount(1);
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public void DefaultValues_ShouldBeHandledCorrectly()
    {
        // Arrange & Act
        var successResult = OperationResult<int>.Success(0); // Default int value
        var successResultWithDefault = OperationResult<string>.Success(default!);

        // Assert
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Should().Be(0);
        
        successResultWithDefault.IsSuccess.Should().BeTrue();
        successResultWithDefault.Value.Should().BeNull();
    }

    [Fact]
    public void Duration_DefaultValue_ShouldBeZero()
    {
        // Arrange & Act
        var result = OperationResult<int>.Success(42);

        // Assert
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Context_DefaultValue_ShouldBeEmptyDictionary()
    {
        // Arrange & Act
        var result = OperationResult<int>.Success(42);

        // Assert
        result.Context.Should().NotBeNull();
        result.Context.Should().BeEmpty();
    }

    #endregion
}