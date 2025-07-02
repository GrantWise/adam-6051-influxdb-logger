// Industrial.Adam.Logger - Modbus Device Management Infrastructure
// Internal interfaces and types for managing Modbus connections and operations

using Industrial.Adam.Logger.Configuration;

namespace Industrial.Adam.Logger.Infrastructure;

/// <summary>
/// Internal interface for managing Modbus connections to individual ADAM devices
/// This interface abstracts the low-level Modbus communication details
/// </summary>
internal interface IModbusDeviceManager : IDisposable
{
    /// <summary>
    /// Unique identifier for the device being managed
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Configuration for this device instance
    /// </summary>
    AdamDeviceConfig Configuration { get; }

    /// <summary>
    /// Current connection status
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Establish connection to the device with retry logic
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection was successful</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read Modbus holding registers from the device
    /// </summary>
    /// <param name="startAddress">Starting register address</param>
    /// <param name="count">Number of registers to read</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing register data or error information</returns>
    Task<ModbusReadResult> ReadRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection to the device with a lightweight operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>True if the device is responsive</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Modbus read operation containing data, error information, and performance metrics
/// </summary>
internal record ModbusReadResult
{
    /// <summary>
    /// Whether the read operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Register data if the operation was successful
    /// </summary>
    public ushort[]? Data { get; init; }

    /// <summary>
    /// Exception that occurred if the operation failed
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Time taken to complete the operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="data">Register data that was read</param>
    /// <param name="duration">Time taken to read the data</param>
    /// <returns>Successful ModbusReadResult</returns>
    public static ModbusReadResult CreateSuccess(ushort[] data, TimeSpan duration) => new()
    {
        Success = true,
        Data = data,
        Duration = duration
    };

    /// <summary>
    /// Create a failed result
    /// </summary>
    /// <param name="error">Exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <returns>Failed ModbusReadResult</returns>
    public static ModbusReadResult CreateFailure(Exception error, TimeSpan duration) => new()
    {
        Success = false,
        Error = error,
        Duration = duration
    };
}