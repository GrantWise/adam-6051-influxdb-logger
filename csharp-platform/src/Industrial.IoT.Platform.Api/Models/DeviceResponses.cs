// Industrial.IoT.Platform.Api - Device API Response Models
// Data transfer objects for device management API responses

using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Industrial.IoT.Platform.Api.Models;

/// <summary>
/// Device information response model
/// </summary>
public class DeviceInfoResponse
{
    /// <summary>
    /// Unique device identifier
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device type (e.g., "ADAM-6051", "ADAM-4571")
    /// </summary>
    [Required]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Current connection status
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Last communication timestamp
    /// </summary>
    public DateTimeOffset? LastSeen { get; set; }

    /// <summary>
    /// Device health information
    /// </summary>
    public IDeviceHealth? Health { get; set; }

    /// <summary>
    /// Device configuration
    /// </summary>
    public IDeviceConfiguration? Configuration { get; set; }
}

/// <summary>
/// Device health status response model
/// </summary>
public class DeviceHealthResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Overall health status
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Current device status
    /// </summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTimeOffset LastChecked { get; set; }

    /// <summary>
    /// Device uptime duration
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Total error count
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public IDictionary<string, object> Diagnostics { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Data reading response model
/// </summary>
public class DataReadingResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Reading timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Data quality indicator
    /// </summary>
    public DataQuality Quality { get; set; }

    /// <summary>
    /// Channel number (if applicable)
    /// </summary>
    public int? Channel { get; set; }

    /// <summary>
    /// Reading value
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Additional tags/metadata
    /// </summary>
    public IDictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Connectivity test response model
/// </summary>
public class ConnectivityTestResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Test result
    /// </summary>
    public bool TestPassed { get; set; }

    /// <summary>
    /// Test execution duration
    /// </summary>
    public TimeSpan TestDuration { get; set; }

    /// <summary>
    /// Test execution timestamp
    /// </summary>
    public DateTimeOffset TestedAt { get; set; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Device creation request model
/// </summary>
public class CreateDeviceRequest
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device type
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Device configuration
    /// </summary>
    [Required]
    public IDictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Optional description
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Device configuration update request model
/// </summary>
public class UpdateDeviceConfigurationRequest
{
    /// <summary>
    /// Updated configuration parameters
    /// </summary>
    [Required]
    public IDictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Whether to restart the device after configuration update
    /// </summary>
    public bool RestartDevice { get; set; } = false;
}