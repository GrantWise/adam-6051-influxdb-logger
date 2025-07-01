// Industrial.IoT.Platform.Core - Device Health Abstractions
// Base interfaces for device health monitoring compatible with existing ADAM logger patterns

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Base interface for device health and diagnostic information
/// Compatible with existing AdamDeviceHealth patterns from Industrial.Adam.Logger
/// </summary>
public interface IDeviceHealth
{
    /// <summary>
    /// Unique identifier for the device being monitored
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Timestamp when this health status was recorded
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Current operational status of the device
    /// </summary>
    DeviceStatus Status { get; }

    /// <summary>
    /// Whether the device is currently connected and responsive
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Time elapsed since the last successful data read
    /// </summary>
    TimeSpan? LastSuccessfulRead { get; }

    /// <summary>
    /// Number of consecutive failed communication attempts
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    /// Average communication latency in milliseconds
    /// </summary>
    double? CommunicationLatency { get; }

    /// <summary>
    /// Last error message encountered during communication
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// Total number of read attempts since device was added
    /// </summary>
    int TotalReads { get; }

    /// <summary>
    /// Number of successful read operations
    /// </summary>
    int SuccessfulReads { get; }

    /// <summary>
    /// Success rate as a percentage (0-100)
    /// </summary>
    double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;
}

/// <summary>
/// Operational status of a device indicating its current health
/// Compatible with existing DeviceStatus enum from Industrial.Adam.Logger
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is operating normally and responding to requests
    /// </summary>
    Online = 0,

    /// <summary>
    /// Device is operational but experiencing minor issues
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Device has encountered errors but may still be partially functional
    /// </summary>
    Error = 2,

    /// <summary>
    /// Device is not responding to communication attempts
    /// </summary>
    Offline = 3,

    /// <summary>
    /// Device status cannot be determined
    /// </summary>
    Unknown = 4
}