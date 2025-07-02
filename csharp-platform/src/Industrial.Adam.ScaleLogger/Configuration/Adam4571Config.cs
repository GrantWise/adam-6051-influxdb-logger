// Industrial.Adam.ScaleLogger - Scale Device Configuration
// Following proven ADAM-6051 configuration patterns for industrial reliability

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.ScaleLogger.Configuration;

/// <summary>
/// Configuration for ADAM-4571 scale logging service
/// Follows proven ADAM-6051 configuration patterns
/// </summary>
public sealed class Adam4571Config
{
    /// <summary>
    /// List of scale devices to monitor
    /// </summary>
    [Required]
    public List<ScaleDeviceConfig> Devices { get; set; } = new();

    /// <summary>
    /// Polling interval in milliseconds (default: 5 seconds for scales)
    /// </summary>
    [Range(1000, 60000)]
    public int PollIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Health check interval in milliseconds
    /// </summary>
    [Range(5000, 300000)]
    public int HealthCheckIntervalMs { get; set; } = 30000;

    /// <summary>
    /// Maximum retry attempts for failed operations
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// InfluxDB configuration for data storage
    /// </summary>
    [Required]
    public InfluxDbConfig InfluxDb { get; set; } = new();

    /// <summary>
    /// Protocol discovery settings
    /// </summary>
    public ProtocolDiscoveryConfig Discovery { get; set; } = new();
}

/// <summary>
/// Configuration for individual scale devices
/// </summary>
public sealed class ScaleDeviceConfig
{
    /// <summary>
    /// Unique device identifier
    /// </summary>
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device display name
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Device location/description
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// IP address or hostname
    /// </summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// TCP port (default: 502 for Modbus, varies for scales)
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 502;

    /// <summary>
    /// Communication timeout in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Scale channels to monitor (1-8 for ADAM-4571)
    /// </summary>
    public List<int> Channels { get; set; } = new() { 1 };

    /// <summary>
    /// Protocol template to use (optional - will auto-discover if not specified)
    /// </summary>
    public string? ProtocolTemplate { get; set; }

    /// <summary>
    /// Scale manufacturer (for data tagging)
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Scale model (for data tagging)
    /// </summary>
    public string? Model { get; set; }
}

/// <summary>
/// InfluxDB configuration following ADAM-6051 patterns
/// </summary>
public sealed class InfluxDbConfig
{
    /// <summary>
    /// InfluxDB server URL
    /// </summary>
    [Required]
    public string Url { get; set; } = "http://localhost:8086";

    /// <summary>
    /// Authentication token
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Organization name
    /// </summary>
    [Required]
    public string Organization { get; set; } = "manufacturing";

    /// <summary>
    /// Bucket name for scale data
    /// </summary>
    [Required]
    public string Bucket { get; set; } = "scale_data";

    /// <summary>
    /// Batch size for writes
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval in milliseconds
    /// </summary>
    [Range(1000, 60000)]
    public int FlushIntervalMs { get; set; } = 5000;
}

/// <summary>
/// Protocol discovery configuration
/// </summary>
public sealed class ProtocolDiscoveryConfig
{
    /// <summary>
    /// Enable automatic protocol discovery
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Discovery timeout in seconds
    /// </summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Path to protocol template files
    /// </summary>
    public string TemplatesPath { get; set; } = "./Templates";

    /// <summary>
    /// Number of test readings to validate protocol
    /// </summary>
    [Range(3, 20)]
    public int ValidationReadings { get; set; } = 5;
}