// Industrial.Adam.Logger - Operation Result Pattern
// Result pattern implementation for better error handling and return values

namespace Industrial.Adam.Logger.Utilities;

/// <summary>
/// Represents the result of an operation that may succeed or fail, providing a consistent way to handle errors
/// </summary>
/// <typeparam name="T">Type of the success value</typeparam>
public readonly struct OperationResult<T>
{
    private readonly T? _value;
    private readonly Exception? _error;
    private readonly string? _errorMessage;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The success value (only valid when IsSuccess is true)
    /// </summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new InvalidOperationException($"Cannot access Value when operation failed: {ErrorMessage}");
            return _value!;
        }
    }

    /// <summary>
    /// The error that occurred (null if operation was successful)
    /// </summary>
    public Exception? Error => _error;

    /// <summary>
    /// Error message describing what went wrong (null if operation was successful)
    /// </summary>
    public string? ErrorMessage => _errorMessage ?? _error?.Message;

    /// <summary>
    /// Time taken to complete the operation
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Additional context information about the operation
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="value">The success value</param>
    /// <param name="duration">Time taken for the operation</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(T value, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
        _errorMessage = null;
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    /// <param name="error">The exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(Exception error, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = false;
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _errorMessage = null;
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a failed result with a custom error message
    /// </summary>
    /// <param name="errorMessage">Custom error message</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(string errorMessage, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = false;
        _value = default;
        _error = null;
        _errorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="value">The success value</param>
    /// <param name="duration">Time taken for the operation</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Successful operation result</returns>
    public static OperationResult<T> Success(T value, TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(value, duration, context);

    /// <summary>
    /// Create a failed result from an exception
    /// </summary>
    /// <param name="error">The exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Failed operation result</returns>
    public static OperationResult<T> Failure(Exception error, TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(error, duration, context);

    /// <summary>
    /// Create a failed result with a custom error message
    /// </summary>
    /// <param name="errorMessage">Custom error message</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Failed operation result</returns>
    public static OperationResult<T> Failure(string errorMessage, TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(errorMessage, duration, context);

    /// <summary>
    /// Transform the value if the operation was successful
    /// </summary>
    /// <typeparam name="TNew">Type of the new value</typeparam>
    /// <param name="transform">Function to transform the value</param>
    /// <returns>New operation result with transformed value or the original failure</returns>
    public OperationResult<TNew> Map<TNew>(Func<T, TNew> transform)
    {
        if (transform == null) throw new ArgumentNullException(nameof(transform));

        if (IsFailure)
        {
            return _error != null
                ? OperationResult<TNew>.Failure(_error, Duration, Context)
                : OperationResult<TNew>.Failure(_errorMessage!, Duration, Context);
        }

        try
        {
            var newValue = transform(_value!);
            return OperationResult<TNew>.Success(newValue, Duration, Context);
        }
        catch (Exception ex)
        {
            return OperationResult<TNew>.Failure(ex, Duration, Context);
        }
    }

    /// <summary>
    /// Execute an action if the operation was successful
    /// </summary>
    /// <param name="action">Action to execute with the success value</param>
    /// <returns>The original operation result</returns>
    public OperationResult<T> OnSuccess(Action<T> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (IsSuccess)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>
    /// Execute an action if the operation failed
    /// </summary>
    /// <param name="action">Action to execute with the error information</param>
    /// <returns>The original operation result</returns>
    public OperationResult<T> OnFailure(Action<string> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (IsFailure)
        {
            action(ErrorMessage!);
        }

        return this;
    }

    /// <summary>
    /// Get the value or a default value if the operation failed
    /// </summary>
    /// <param name="defaultValue">Default value to return on failure</param>
    /// <returns>The success value or the default value</returns>
    public T GetValueOrDefault(T defaultValue = default!)
        => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Convert to a string representation
    /// </summary>
    /// <returns>String representation of the result</returns>
    public override string ToString()
    {
        if (IsSuccess)
            return $"Success: {_value} (Duration: {Duration.TotalMilliseconds:F2}ms)";
        
        return $"Failure: {ErrorMessage} (Duration: {Duration.TotalMilliseconds:F2}ms)";
    }
}

/// <summary>
/// Non-generic operation result for operations that don't return a value
/// </summary>
public readonly struct OperationResult
{
    private readonly Exception? _error;
    private readonly string? _errorMessage;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error that occurred (null if operation was successful)
    /// </summary>
    public Exception? Error => _error;

    /// <summary>
    /// Error message describing what went wrong (null if operation was successful)
    /// </summary>
    public string? ErrorMessage => _errorMessage ?? _error?.Message;

    /// <summary>
    /// Time taken to complete the operation
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Additional context information about the operation
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="duration">Time taken for the operation</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = true;
        _error = null;
        _errorMessage = null;
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    /// <param name="error">The exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(Exception error, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = false;
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _errorMessage = null;
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a failed result with a custom error message
    /// </summary>
    /// <param name="errorMessage">Custom error message</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    private OperationResult(string errorMessage, TimeSpan duration, Dictionary<string, object>? context = null)
    {
        IsSuccess = false;
        _error = null;
        _errorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        Duration = duration;
        Context = context == null ? new Dictionary<string, object>() : new Dictionary<string, object>(context);
    }

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="duration">Time taken for the operation</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Successful operation result</returns>
    public static OperationResult Success(TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(duration, context);

    /// <summary>
    /// Create a failed result from an exception
    /// </summary>
    /// <param name="error">The exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Failed operation result</returns>
    public static OperationResult Failure(Exception error, TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(error, duration, context);

    /// <summary>
    /// Create a failed result with a custom error message
    /// </summary>
    /// <param name="errorMessage">Custom error message</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Failed operation result</returns>
    public static OperationResult Failure(string errorMessage, TimeSpan duration = default, Dictionary<string, object>? context = null)
        => new(errorMessage, duration, context);

    /// <summary>
    /// Convert to a string representation
    /// </summary>
    /// <returns>String representation of the result</returns>
    public override string ToString()
    {
        if (IsSuccess)
            return $"Success (Duration: {Duration.TotalMilliseconds:F2}ms)";
        
        return $"Failure: {ErrorMessage} (Duration: {Duration.TotalMilliseconds:F2}ms)";
    }
}