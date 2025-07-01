// Industrial.IoT.Platform.Core - Transport Provider Abstractions
// Base interfaces for transport communication (TCP, Serial, etc.)

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Base interface for transport providers enabling protocol-agnostic device communication
/// Supports various transport types like TCP, Serial, USB, etc.
/// </summary>
public interface ITransportProvider : IDisposable
{
    /// <summary>
    /// Transport type identifier (e.g., "TCP", "Serial", "USB")
    /// </summary>
    string TransportType { get; }

    /// <summary>
    /// Whether the transport is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Transport-specific connection parameters
    /// </summary>
    IReadOnlyDictionary<string, object> ConnectionParameters { get; }

    /// <summary>
    /// Connect to the transport endpoint
    /// </summary>
    /// <param name="parameters">Transport-specific connection parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task indicating connection success</returns>
    Task<bool> ConnectAsync(IReadOnlyDictionary<string, object> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the transport endpoint
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when disconnected</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send data to the transport endpoint
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task indicating send success</returns>
    Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive data from the transport endpoint
    /// </summary>
    /// <param name="buffer">Buffer to receive data into</param>
    /// <param name="timeout">Timeout for receive operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of bytes received</returns>
    Task<int> ReceiveAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start capturing data stream for protocol discovery
    /// </summary>
    /// <param name="duration">Duration to capture data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Captured data frames</returns>
    Task<IReadOnlyList<byte[]>> CaptureDataStreamAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connectivity without establishing persistent connection
    /// </summary>
    /// <param name="parameters">Transport-specific connection parameters</param>
    /// <param name="timeout">Test timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connectivity test result</returns>
    Task<TransportTestResult> TestConnectivityAsync(
        IReadOnlyDictionary<string, object> parameters, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when data is received (for real-time monitoring)
    /// </summary>
    event EventHandler<DataReceivedEventArgs> DataReceived;

    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;
}

/// <summary>
/// Result of transport connectivity testing
/// </summary>
public sealed record TransportTestResult
{
    /// <summary>
    /// Whether connectivity test was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Time taken to complete the test
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Round-trip latency if applicable
    /// </summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Event arguments for data received events
/// </summary>
public sealed class DataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Data that was received
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Timestamp when data was received
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Transport that received the data
    /// </summary>
    public required string TransportId { get; init; }
}

/// <summary>
/// Event arguments for connection status changes
/// </summary>
public sealed class ConnectionStatusEventArgs : EventArgs
{
    /// <summary>
    /// Whether transport is now connected
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// Previous connection status
    /// </summary>
    public required bool PreviousStatus { get; init; }

    /// <summary>
    /// Timestamp of status change
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Transport that changed status
    /// </summary>
    public required string TransportId { get; init; }

    /// <summary>
    /// Error message if disconnection was due to error
    /// </summary>
    public string? ErrorMessage { get; init; }
}