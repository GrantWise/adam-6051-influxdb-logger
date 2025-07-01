// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Scale Device Configuration
// Configuration classes for ADAM-4571 scale devices with comprehensive validation following existing patterns

using System.ComponentModel.DataAnnotations;
using System.Net;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Devices.Adam.Models;
using Industrial.IoT.Platform.Devices.Adam.Transport;

namespace Industrial.IoT.Platform.Devices.Adam.Configuration;

/// <summary>
/// Configuration for an ADAM-4571 scale device including connection settings, protocol discovery, and operational parameters
/// Follows the exact same patterns as the existing AdamDeviceConfig class
/// </summary>
public class Adam4571Configuration : IDeviceConfiguration, IValidatableObject
{
    /// <summary>
    /// Unique identifier for this device instance
    /// </summary>
    [Required(ErrorMessage = "DeviceId is required")]
    [StringLength(Constants.MaxDeviceIdLength, ErrorMessage = "DeviceId must be 50 characters or less")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this device
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this device is enabled for data acquisition
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// IP address of the ADAM-4571 device on the network
    /// </summary>
    [Required(ErrorMessage = "IP Address is required")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// TCP port for raw socket communication (default 4001)
    /// </summary>
    [Range(Constants.MinPortNumber, Constants.MaxPortNumber, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = Constants.DefaultAdam4571Port;

    /// <summary>
    /// Communication timeout in milliseconds
    /// </summary>
    [Range(500, 30000, ErrorMessage = "Timeout must be between 500ms and 30 seconds")]
    public int TimeoutMs { get; set; } = Constants.DefaultDeviceTimeoutMs;

    /// <summary>
    /// Maximum number of retry attempts for failed operations
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = Constants.DefaultMaxRetries;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    [Range(100, 10000, ErrorMessage = "RetryDelayMs must be between 100ms and 10 seconds")]
    public int RetryDelayMs { get; set; } = Constants.DefaultRetryDelayMs;

    #region Advanced Connection Settings

    /// <summary>
    /// Enable TCP keep-alive packets to maintain connection
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Enable Nagle algorithm for TCP optimization (usually disabled for industrial applications)
    /// </summary>
    public bool EnableNagle { get; set; } = false;

    /// <summary>
    /// TCP receive buffer size in bytes
    /// </summary>
    [Range(1024, 65536, ErrorMessage = "ReceiveBufferSize must be between 1KB and 64KB")]
    public int ReceiveBufferSize { get; set; } = Constants.DefaultReceiveBufferSize;

    /// <summary>
    /// TCP send buffer size in bytes
    /// </summary>
    [Range(1024, 65536, ErrorMessage = "SendBufferSize must be between 1KB and 64KB")]
    public int SendBufferSize { get; set; } = Constants.DefaultSendBufferSize;

    #endregion

    #region Protocol Discovery Settings

    /// <summary>
    /// Enable automatic protocol discovery on device connection
    /// </summary>
    public bool EnableProtocolDiscovery { get; set; } = true;

    /// <summary>
    /// Confidence threshold for automatic protocol selection (0-100)
    /// </summary>
    [Range(50.0, 99.0, ErrorMessage = "ConfidenceThreshold must be between 50% and 99%")]
    public double ConfidenceThreshold { get; set; } = Constants.DefaultConfidenceThreshold;

    /// <summary>
    /// Timeout for protocol discovery operations in seconds
    /// </summary>
    [Range(10, 300, ErrorMessage = "DiscoveryTimeoutSeconds must be between 10 seconds and 5 minutes")]
    public int DiscoveryTimeoutSeconds { get; set; } = Constants.DefaultDiscoveryTimeoutSeconds;

    /// <summary>
    /// Manually specified protocol template to use (overrides discovery)
    /// </summary>
    public string? ForceProtocolTemplate { get; set; }

    #endregion

    #region Scale-Specific Settings

    /// <summary>
    /// Default weight unit for this scale
    /// </summary>
    public WeightUnit DefaultUnit { get; set; } = WeightUnit.Kilograms;

    /// <summary>
    /// Number of decimal places for weight display
    /// </summary>
    [Range(0, 6, ErrorMessage = "DecimalPlaces must be between 0 and 6")]
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Enable automatic tare subtraction
    /// </summary>
    public bool EnableAutoTare { get; set; } = false;

    /// <summary>
    /// Stability detection threshold (minimum stable time in milliseconds)
    /// </summary>
    [Range(100, 10000, ErrorMessage = "StabilityThresholdMs must be between 100ms and 10 seconds")]
    public int StabilityThresholdMs { get; set; } = Constants.DefaultStabilityThresholdMs;

    /// <summary>
    /// Weight change threshold for stability detection
    /// </summary>
    [Range(0.001, 100.0, ErrorMessage = "StabilityTolerance must be between 0.001 and 100.0")]
    public double StabilityTolerance { get; set; } = Constants.DefaultStabilityTolerance;

    /// <summary>
    /// Scale capacity for overload detection
    /// </summary>
    [Range(0.1, double.MaxValue, ErrorMessage = "Capacity must be greater than 0.1")]
    public double? Capacity { get; set; }

    /// <summary>
    /// Scale resolution/precision for data validation
    /// </summary>
    [Range(0.0001, 1000.0, ErrorMessage = "Resolution must be between 0.0001 and 1000.0")]
    public double? Resolution { get; set; }

    #endregion

    #region Data Processing Options

    /// <summary>
    /// Enable data validation using configured limits and rules
    /// </summary>
    public bool EnableDataValidation { get; set; } = true;

    /// <summary>
    /// Enable automatic unit conversion to default unit
    /// </summary>
    public bool EnableUnitConversion { get; set; } = true;

    /// <summary>
    /// Polling interval for continuous weight monitoring (milliseconds)
    /// </summary>
    [Range(100, 60000, ErrorMessage = "PollingIntervalMs must be between 100ms and 1 minute")]
    public int PollingIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

    /// <summary>
    /// Enable averaging of multiple readings for stability
    /// </summary>
    public bool EnableAveraging { get; set; } = false;

    /// <summary>
    /// Number of readings to average (when averaging is enabled)
    /// </summary>
    [Range(2, 20, ErrorMessage = "AveragingCount must be between 2 and 20")]
    public int AveragingCount { get; set; } = 5;

    #endregion

    /// <summary>
    /// Custom metadata tags that will be attached to all readings from this device
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = new();

    #region IDeviceConfiguration Implementation

    /// <summary>
    /// Device identifier for platform compatibility
    /// </summary>
    string IDeviceConfiguration.DeviceId => DeviceId;

    /// <summary>
    /// Device type identifier
    /// </summary>
    string IDeviceConfiguration.DeviceType => "ADAM-4571";

    /// <summary>
    /// Manufacturer identifier
    /// </summary>
    string? IDeviceConfiguration.Manufacturer => "Advantech";

    /// <summary>
    /// Configuration tags for platform compatibility
    /// </summary>
    IReadOnlyDictionary<string, object>? IDeviceConfiguration.Tags => Tags;

    /// <summary>
    /// Device name for platform compatibility
    /// </summary>
    string IDeviceConfiguration.DeviceName => string.IsNullOrWhiteSpace(Name) ? DeviceId : Name;

    /// <summary>
    /// Device description
    /// </summary>
    string? IDeviceConfiguration.Description => $"ADAM-4571 Scale Device at {IpAddress}:{Port}";

    /// <summary>
    /// IP Address for network devices
    /// </summary>
    string? IDeviceConfiguration.IpAddress => IpAddress;

    /// <summary>
    /// Port for network devices
    /// </summary>
    int? IDeviceConfiguration.Port => Port;

    /// <summary>
    /// Serial port (not applicable for ADAM-4571)
    /// </summary>
    string? IDeviceConfiguration.SerialPort => null;

    /// <summary>
    /// Baud rate (not applicable for TCP devices)
    /// </summary>
    int? IDeviceConfiguration.BaudRate => null;

    /// <summary>
    /// Data bits (not applicable for TCP devices)
    /// </summary>
    int? IDeviceConfiguration.DataBits => null;

    /// <summary>
    /// Parity (not applicable for TCP devices)
    /// </summary>
    string? IDeviceConfiguration.Parity => null;

    /// <summary>
    /// Stop bits (not applicable for TCP devices)
    /// </summary>
    int? IDeviceConfiguration.StopBits => null;

    /// <summary>
    /// Flow control (not applicable for TCP devices)
    /// </summary>
    string? IDeviceConfiguration.FlowControl => null;

    /// <summary>
    /// Protocol template to use
    /// </summary>
    string? IDeviceConfiguration.ProtocolTemplate => ForceProtocolTemplate;

    /// <summary>
    /// Channel configurations (ADAM-4571 has 1 scale input)
    /// </summary>
    IReadOnlyList<IChannelConfiguration>? IDeviceConfiguration.ChannelConfigurations => null;

    /// <summary>
    /// Data acquisition interval
    /// </summary>
    int IDeviceConfiguration.AcquisitionIntervalMs => PollingIntervalMs;

    /// <summary>
    /// Connection timeout
    /// </summary>
    int IDeviceConfiguration.ConnectionTimeoutMs => TimeoutMs;

    /// <summary>
    /// Read timeout
    /// </summary>
    int IDeviceConfiguration.ReadTimeoutMs => TimeoutMs;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    int IDeviceConfiguration.MaxRetries => MaxRetries;

    /// <summary>
    /// Retry delay
    /// </summary>
    int IDeviceConfiguration.RetryDelayMs => RetryDelayMs;

    /// <summary>
    /// Whether device is active
    /// </summary>
    bool IDeviceConfiguration.IsActive => Enabled;

    /// <summary>
    /// Enable health monitoring
    /// </summary>
    bool IDeviceConfiguration.EnableHealthMonitoring => true;

    /// <summary>
    /// Enable stability monitoring
    /// </summary>
    bool IDeviceConfiguration.EnableStabilityMonitoring => true;

    /// <summary>
    /// Stability threshold
    /// </summary>
    double IDeviceConfiguration.StabilityThreshold => StabilityTolerance;

    /// <summary>
    /// Environmental optimization
    /// </summary>
    string? IDeviceConfiguration.EnvironmentalOptimization => null;

    /// <summary>
    /// Device location
    /// </summary>
    string? IDeviceConfiguration.Location => null;

    /// <summary>
    /// Department
    /// </summary>
    string? IDeviceConfiguration.Department => null;

    /// <summary>
    /// Device model
    /// </summary>
    string? IDeviceConfiguration.Model => "ADAM-4571";

    /// <summary>
    /// Device serial number
    /// </summary>
    string? IDeviceConfiguration.SerialNumber => null;

    /// <summary>
    /// Firmware version
    /// </summary>
    string? IDeviceConfiguration.FirmwareVersion => null;

    #endregion

    /// <summary>
    /// Validates the device configuration and returns any validation errors
    /// Follows the exact same pattern as AdamDeviceConfig.Validate
    /// </summary>
    /// <param name="validationContext">Validation context for the operation</param>
    /// <returns>Collection of validation results indicating any configuration errors</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate DeviceId
        if (string.IsNullOrWhiteSpace(DeviceId))
            yield return new ValidationResult("DeviceId is required and cannot be empty", new[] { nameof(DeviceId) });

        // Validate IP Address format
        if (string.IsNullOrWhiteSpace(IpAddress) || !IPAddress.TryParse(IpAddress, out _))
            yield return new ValidationResult("Valid IP address is required", new[] { nameof(IpAddress) });

        // Validate protocol template if specified
        if (!string.IsNullOrWhiteSpace(ForceProtocolTemplate))
        {
            if (ForceProtocolTemplate.Length > Constants.MaxProtocolTemplateNameLength)
                yield return new ValidationResult(
                    $"ForceProtocolTemplate must be {Constants.MaxProtocolTemplateNameLength} characters or less", 
                    new[] { nameof(ForceProtocolTemplate) });
        }

        // Validate capacity and resolution relationship
        if (Capacity.HasValue && Resolution.HasValue)
        {
            if (Resolution.Value > Capacity.Value)
                yield return new ValidationResult(
                    "Resolution cannot be greater than Capacity", 
                    new[] { nameof(Resolution), nameof(Capacity) });
        }

        // Validate averaging configuration
        if (EnableAveraging && AveragingCount < 2)
            yield return new ValidationResult(
                "AveragingCount must be at least 2 when averaging is enabled", 
                new[] { nameof(AveragingCount) });

        // Validate stability detection settings
        if (StabilityThresholdMs < 100)
            yield return new ValidationResult(
                "StabilityThresholdMs must be at least 100ms for reliable detection", 
                new[] { nameof(StabilityThresholdMs) });

        // Validate polling interval vs timeout
        if (PollingIntervalMs <= TimeoutMs)
            yield return new ValidationResult(
                "PollingIntervalMs should be greater than TimeoutMs to avoid overlapping operations", 
                new[] { nameof(PollingIntervalMs), nameof(TimeoutMs) });

        // Validate discovery timeout
        if (EnableProtocolDiscovery && DiscoveryTimeoutSeconds < 10)
            yield return new ValidationResult(
                "DiscoveryTimeoutSeconds must be at least 10 seconds for reliable protocol discovery", 
                new[] { nameof(DiscoveryTimeoutSeconds) });
    }

    /// <summary>
    /// Create a TcpEndpoint from this configuration
    /// </summary>
    /// <returns>Configured TCP endpoint</returns>
    public TcpEndpoint CreateTcpEndpoint() => new()
    {
        IpAddress = IpAddress,
        Port = Port,
        TimeoutMs = TimeoutMs,
        KeepAlive = KeepAlive,
        EnableNagle = EnableNagle,
        ReceiveBufferSize = ReceiveBufferSize,
        SendBufferSize = SendBufferSize
    };
}