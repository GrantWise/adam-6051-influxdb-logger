// Industrial.IoT.Platform.Storage - Device Configuration Entity
// Entity Framework model for device configuration storage following existing ADAM logger patterns

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Industrial.IoT.Platform.Storage.Data.Entities;

/// <summary>
/// Entity Framework model for device configuration settings
/// Stores discovered device configurations for ADAM-4571 scale providers
/// </summary>
[Table("DeviceConfigurations")]
// Indexes configured in DbContext
public sealed class DeviceConfigurationEntity
{
    /// <summary>
    /// Primary key for the device configuration
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Unique identifier for the device
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Type of device (ADAM-6051, ADAM-4571, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the device
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device description and notes
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// IP address or hostname for network-connected devices
    /// </summary>
    [MaxLength(100)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Port number for network communication
    /// </summary>
    [Range(1, 65535)]
    public int? Port { get; set; }

    /// <summary>
    /// Serial port configuration for RS232/RS485 devices
    /// </summary>
    [MaxLength(50)]
    public string? SerialPort { get; set; }

    /// <summary>
    /// Baud rate for serial communication
    /// </summary>
    [Range(300, 115200)]
    public int? BaudRate { get; set; }

    /// <summary>
    /// Data bits for serial communication
    /// </summary>
    [Range(7, 8)]
    public int? DataBits { get; set; }

    /// <summary>
    /// Parity setting for serial communication
    /// </summary>
    [MaxLength(10)]
    public string? Parity { get; set; }

    /// <summary>
    /// Stop bits for serial communication
    /// </summary>
    [Range(1, 2)]
    public int? StopBits { get; set; }

    /// <summary>
    /// Flow control setting for serial communication
    /// </summary>
    [MaxLength(20)]
    public string? FlowControl { get; set; }

    /// <summary>
    /// Protocol template used for this device
    /// </summary>
    [MaxLength(100)]
    public string? ProtocolTemplate { get; set; }

    /// <summary>
    /// Device-specific configuration as JSON
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Channel configuration settings as JSON
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ChannelConfigurationsJson { get; set; }

    /// <summary>
    /// Data acquisition interval in milliseconds
    /// </summary>
    [Range(100, 3600000)]
    public int AcquisitionIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    [Range(1000, 60000)]
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    [Range(100, 30000)]
    public int ReadTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether the device is currently active and should be monitored
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether to enable health monitoring for this device
    /// </summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Whether to enable signal stability monitoring
    /// </summary>
    public bool EnableStabilityMonitoring { get; set; } = true;

    /// <summary>
    /// Signal stability threshold (0-100)
    /// </summary>
    [Range(0, 100)]
    public double StabilityThreshold { get; set; } = 80.0;

    /// <summary>
    /// Environmental optimization setting
    /// </summary>
    [MaxLength(50)]
    public string? EnvironmentalOptimization { get; set; }

    /// <summary>
    /// Physical location of the device
    /// </summary>
    [MaxLength(200)]
    public string? Location { get; set; }

    /// <summary>
    /// Department or area responsible for this device
    /// </summary>
    [MaxLength(100)]
    public string? Department { get; set; }

    /// <summary>
    /// Device manufacturer
    /// </summary>
    [MaxLength(50)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [MaxLength(50)]
    public string? Model { get; set; }

    /// <summary>
    /// Device serial number
    /// </summary>
    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Firmware version
    /// </summary>
    [MaxLength(50)]
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// Device tags for categorization and filtering
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? TagsJson { get; set; }

    /// <summary>
    /// Last successful connection timestamp
    /// </summary>
    public DateTimeOffset? LastConnectedAt { get; set; }

    /// <summary>
    /// Last successful data reading timestamp
    /// </summary>
    public DateTimeOffset? LastDataReadAt { get; set; }

    /// <summary>
    /// Total number of connection attempts
    /// </summary>
    public int TotalConnections { get; set; } = 0;

    /// <summary>
    /// Number of successful connections
    /// </summary>
    public int SuccessfulConnections { get; set; } = 0;

    /// <summary>
    /// Total number of data read attempts
    /// </summary>
    public int TotalDataReads { get; set; } = 0;

    /// <summary>
    /// Number of successful data reads
    /// </summary>
    public int SuccessfulDataReads { get; set; } = 0;

    /// <summary>
    /// Record creation timestamp (for auditing)
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Record last modified timestamp (for auditing)
    /// </summary>
    [Required]
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// User who created this configuration
    /// </summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last modified this configuration
    /// </summary>
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Connection success rate percentage
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public double ConnectionSuccessRate => 
        TotalConnections > 0 ? (double)SuccessfulConnections / TotalConnections * 100 : 0;

    /// <summary>
    /// Data read success rate percentage
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public double DataReadSuccessRate => 
        TotalDataReads > 0 ? (double)SuccessfulDataReads / TotalDataReads * 100 : 0;

    /// <summary>
    /// Overall device health score (0-100)
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public double HealthScore => 
        IsActive ? (ConnectionSuccessRate * 0.3 + DataReadSuccessRate * 0.7) : 0;

    /// <summary>
    /// Indicates if the device is currently online
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool IsOnline => 
        LastConnectedAt.HasValue && 
        (DateTimeOffset.UtcNow - LastConnectedAt.Value).TotalMinutes <= 10;

    /// <summary>
    /// Indicates if the device has recent data
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool HasRecentData => 
        LastDataReadAt.HasValue && 
        (DateTimeOffset.UtcNow - LastDataReadAt.Value).TotalMinutes <= 5;
}