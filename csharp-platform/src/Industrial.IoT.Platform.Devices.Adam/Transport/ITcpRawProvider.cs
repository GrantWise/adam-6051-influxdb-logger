// Industrial.IoT.Platform.Devices.Adam - TCP Raw Socket Transport Interface
// Interface for managing raw TCP socket connections following existing ADAM logger patterns

using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Devices.Adam.Transport;

/// <summary>
/// Interface for managing raw TCP socket connections to ADAM-4571 devices
/// Follows the same patterns as the existing ModbusDeviceManager infrastructure
/// </summary>
public interface ITcpRawProvider : IDisposable
{
    /// <summary>
    /// Target endpoint configuration
    /// </summary>
    TcpEndpoint Endpoint { get; }

    /// <summary>
    /// Current connection status
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Observable stream of raw data received from the TCP connection
    /// </summary>
    IObservable<byte[]> DataReceived { get; }

    /// <summary>
    /// Observable stream of connection status changes
    /// </summary>
    IObservable<bool> ConnectionStatus { get; }

    /// <summary>
    /// Establish connection to the TCP endpoint with retry logic
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection was successful</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send raw data to the connected TCP endpoint
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing success status and error information</returns>
    Task<TcpOperationResult> SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send raw data and wait for response with timeout
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="timeoutMs">Timeout in milliseconds for the response</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing response data or error information</returns>
    Task<TcpOperationResult> SendAndReceiveAsync(byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection to the endpoint with a lightweight operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>True if the endpoint is responsive</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the endpoint and clean up resources
    /// </summary>
    Task DisconnectAsync();
}

/// <summary>
/// TCP endpoint configuration following existing AdamDeviceConfig patterns
/// </summary>
public record TcpEndpoint
{
    /// <summary>
    /// IP address of the target device
    /// </summary>
    public required string IpAddress { get; init; }

    /// <summary>
    /// TCP port for the connection
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; init; } = Constants.DefaultDeviceTimeoutMs;

    /// <summary>
    /// Enable TCP keep-alive packets
    /// </summary>
    public bool KeepAlive { get; init; } = true;

    /// <summary>
    /// Enable Nagle algorithm (usually disabled for industrial applications)
    /// </summary>
    public bool EnableNagle { get; init; } = false;

    /// <summary>
    /// TCP receive buffer size in bytes
    /// </summary>
    public int ReceiveBufferSize { get; init; } = Constants.DefaultReceiveBufferSize;

    /// <summary>
    /// TCP send buffer size in bytes
    /// </summary>
    public int SendBufferSize { get; init; } = Constants.DefaultSendBufferSize;
}

/// <summary>
/// Result of a TCP operation containing data, error information, and performance metrics
/// Follows the same pattern as ModbusReadResult
/// </summary>
public record TcpOperationResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response data if the operation was successful
    /// </summary>
    public byte[]? Data { get; init; }

    /// <summary>
    /// Exception that occurred if the operation failed
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Time taken to complete the operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Additional context information about the operation
    /// </summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>
    /// Create a successful result
    /// </summary>
    /// <param name="data">Response data that was received</param>
    /// <param name="duration">Time taken to complete the operation</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Successful TcpOperationResult</returns>
    public static TcpOperationResult CreateSuccess(byte[] data, TimeSpan duration, Dictionary<string, object>? context = null) => new()
    {
        Success = true,
        Data = data,
        Duration = duration,
        Context = context ?? new()
    };

    /// <summary>
    /// Create a failed result
    /// </summary>
    /// <param name="error">Exception that caused the failure</param>
    /// <param name="duration">Time taken before the failure occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Failed TcpOperationResult</returns>
    public static TcpOperationResult CreateFailure(Exception error, TimeSpan duration, Dictionary<string, object>? context = null) => new()
    {
        Success = false,
        Error = error,
        Duration = duration,
        Context = context ?? new()
    };
}