// Industrial.IoT.Platform.Core - Device Configuration Interface
// Interface for device configuration settings following existing ADAM logger patterns

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Interface for device configuration settings
/// Stores discovered device configurations for ADAM-4571 scale providers
/// Following existing ADAM logger configuration patterns for consistency
/// </summary>
public interface IDeviceConfiguration
{
    /// <summary>
    /// Unique identifier for the device
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Type of device (ADAM-6051, ADAM-4571, etc.)
    /// </summary>
    string DeviceType { get; }

    /// <summary>
    /// Human-readable name for the device
    /// </summary>
    string DeviceName { get; }

    /// <summary>
    /// Device description and notes
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// IP address or hostname for network-connected devices
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Port number for network communication
    /// </summary>
    int? Port { get; }

    /// <summary>
    /// Serial port configuration for RS232/RS485 devices
    /// </summary>
    string? SerialPort { get; }

    /// <summary>
    /// Baud rate for serial communication
    /// </summary>
    int? BaudRate { get; }

    /// <summary>
    /// Data bits for serial communication
    /// </summary>
    int? DataBits { get; }

    /// <summary>
    /// Parity setting for serial communication
    /// </summary>
    string? Parity { get; }

    /// <summary>
    /// Stop bits for serial communication
    /// </summary>
    int? StopBits { get; }

    /// <summary>
    /// Flow control setting for serial communication
    /// </summary>
    string? FlowControl { get; }

    /// <summary>
    /// Protocol template used for this device
    /// </summary>
    string? ProtocolTemplate { get; }

    /// <summary>
    /// Channel configuration settings
    /// </summary>
    IReadOnlyList<IChannelConfiguration>? ChannelConfigurations { get; }

    /// <summary>
    /// Data acquisition interval in milliseconds
    /// </summary>
    int AcquisitionIntervalMs { get; }

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    int ConnectionTimeoutMs { get; }

    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    int ReadTimeoutMs { get; }

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    int RetryDelayMs { get; }

    /// <summary>
    /// Whether the device is currently active and should be monitored
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Whether to enable health monitoring for this device
    /// </summary>
    bool EnableHealthMonitoring { get; }

    /// <summary>
    /// Whether to enable signal stability monitoring
    /// </summary>
    bool EnableStabilityMonitoring { get; }

    /// <summary>
    /// Signal stability threshold (0-100)
    /// </summary>
    double StabilityThreshold { get; }

    /// <summary>
    /// Environmental optimization setting
    /// </summary>
    string? EnvironmentalOptimization { get; }

    /// <summary>
    /// Physical location of the device
    /// </summary>
    string? Location { get; }

    /// <summary>
    /// Department or area responsible for this device
    /// </summary>
    string? Department { get; }

    /// <summary>
    /// Device manufacturer
    /// </summary>
    string? Manufacturer { get; }

    /// <summary>
    /// Device model
    /// </summary>
    string? Model { get; }

    /// <summary>
    /// Device serial number
    /// </summary>
    string? SerialNumber { get; }

    /// <summary>
    /// Firmware version
    /// </summary>
    string? FirmwareVersion { get; }

    /// <summary>
    /// Device tags for categorization and filtering
    /// </summary>
    IReadOnlyDictionary<string, object>? Tags { get; }
}

/// <summary>
/// Interface for channel configuration settings
/// </summary>
public interface IChannelConfiguration
{
    /// <summary>
    /// Channel number
    /// </summary>
    int ChannelNumber { get; }

    /// <summary>
    /// Channel name or description
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this channel is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Data type for this channel
    /// </summary>
    string DataType { get; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    string? Unit { get; }

    /// <summary>
    /// Scaling factor
    /// </summary>
    double? ScalingFactor { get; }

    /// <summary>
    /// Offset value
    /// </summary>
    double? Offset { get; }

    /// <summary>
    /// Channel-specific tags
    /// </summary>
    IReadOnlyDictionary<string, object>? Tags { get; }
}