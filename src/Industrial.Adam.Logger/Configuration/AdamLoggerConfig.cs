// Industrial.Adam.Logger - Main Service Configuration
// Configuration for the ADAM logger service including global settings and device management

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Configuration;

/// <summary>
/// Main configuration for the ADAM logger service, including global settings and device management
/// </summary>
public class AdamLoggerConfig
{
    /// <summary>
    /// List of ADAM devices to monitor
    /// </summary>
    [Required(ErrorMessage = "At least one device must be configured")]
    public List<AdamDeviceConfig> Devices { get; set; } = new();

    #region Timing Configuration

    /// <summary>
    /// Global polling interval in milliseconds for all devices
    /// </summary>
    [Range(100, 300000, ErrorMessage = "PollIntervalMs must be between 100ms and 5 minutes")]
    public int PollIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

    /// <summary>
    /// Health check interval in milliseconds
    /// </summary>
    [Range(5000, 300000, ErrorMessage = "HealthCheckIntervalMs must be between 5 seconds and 5 minutes")]
    public int HealthCheckIntervalMs { get; set; } = Constants.DefaultHealthCheckIntervalMs;

    #endregion

    #region Performance Configuration

    /// <summary>
    /// Maximum number of devices to poll concurrently
    /// </summary>
    [Range(1, 50, ErrorMessage = "MaxConcurrentDevices must be between 1 and 50")]
    public int MaxConcurrentDevices { get; set; } = Constants.DefaultMaxConcurrentDevices;

    /// <summary>
    /// Internal data buffer size for storing readings
    /// </summary>
    [Range(100, 100000, ErrorMessage = "DataBufferSize must be between 100 and 100,000")]
    public int DataBufferSize { get; set; } = Constants.DefaultDataBufferSize;

    /// <summary>
    /// Batch size for processing data readings
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1,000")]
    public int BatchSize { get; set; } = Constants.DefaultBatchSize;

    /// <summary>
    /// Timeout for batch processing operations in milliseconds
    /// </summary>
    [Range(100, 30000, ErrorMessage = "BatchTimeoutMs must be between 100ms and 30 seconds")]
    public int BatchTimeoutMs { get; set; } = Constants.DefaultBatchTimeoutMs;

    #endregion

    #region Error Handling Configuration

    /// <summary>
    /// Enable automatic recovery from communication failures
    /// </summary>
    public bool EnableAutomaticRecovery { get; set; } = true;

    /// <summary>
    /// Maximum consecutive failures before marking a device as offline
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxConsecutiveFailures must be between 1 and 100")]
    public int MaxConsecutiveFailures { get; set; } = Constants.DefaultMaxConsecutiveFailures;

    /// <summary>
    /// Device timeout in minutes before considering it unresponsive
    /// </summary>
    [Range(1, 60, ErrorMessage = "DeviceTimeoutMinutes must be between 1 and 60 minutes")]
    public int DeviceTimeoutMinutes { get; set; } = Constants.DefaultDeviceTimeoutMinutes;

    #endregion

    #region Monitoring and Diagnostics

    /// <summary>
    /// Enable performance counters and metrics collection
    /// </summary>
    public bool EnablePerformanceCounters { get; set; } = true;

    /// <summary>
    /// Enable detailed logging for troubleshooting (may impact performance)
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    #endregion

    #region Demo and Testing Configuration

    /// <summary>
    /// Enable demo mode with mock data generation instead of real device communication
    /// </summary>
    public bool DemoMode { get; set; } = false;

    #endregion

    #region InfluxDB Configuration

    /// <summary>
    /// InfluxDB configuration for data storage
    /// </summary>
    public InfluxDbConfig? InfluxDb { get; set; }

    #endregion

    /// <summary>
    /// Validates the entire configuration and returns any validation errors
    /// </summary>
    /// <returns>Collection of validation results indicating any configuration errors</returns>
    public IEnumerable<ValidationResult> ValidateConfiguration()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        // Validate this configuration object
        Validator.TryValidateObject(this, context, results, true);

        // Validate each device configuration
        foreach (var device in Devices.Select((value, index) => new { value, index }))
        {
            var deviceContext = new ValidationContext(device.value)
            {
                MemberName = $"{nameof(Devices)}[{device.index}]"
            };

            var deviceResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(device.value, deviceContext, deviceResults, true))
            {
                results.AddRange(deviceResults.Select(r => new ValidationResult(
                    $"Device '{device.value.DeviceId}': {r.ErrorMessage}",
                    r.MemberNames.Select(name => $"{nameof(Devices)}[{device.index}].{name}")
                )));
            }

            // Also validate using the device's custom validation
            results.AddRange(device.value.Validate(deviceContext));
        }

        // Check for duplicate device IDs
        var duplicateDevices = Devices.GroupBy(d => d.DeviceId).Where(g => g.Count() > 1);
        if (duplicateDevices.Any())
        {
            var duplicateIds = string.Join(", ", duplicateDevices.Select(g => g.Key));
            results.Add(new ValidationResult($"Duplicate device IDs found: {duplicateIds}", new[] { nameof(Devices) }));
        }

        // Validate performance implications
        if (Devices.Count > MaxConcurrentDevices)
        {
            results.Add(new ValidationResult(
                $"Number of devices ({Devices.Count}) exceeds MaxConcurrentDevices ({MaxConcurrentDevices}). This may impact performance.",
                new[] { nameof(Devices), nameof(MaxConcurrentDevices) }
            ));
        }

        // Validate polling frequency vs device count
        var totalChannels = Devices.Sum(d => d.Channels.Count(c => c.Enabled));
        var estimatedCycleTime = totalChannels * 50; // Rough estimate: 50ms per channel
        if (estimatedCycleTime > PollIntervalMs)
        {
            results.Add(new ValidationResult(
                $"Polling interval ({PollIntervalMs}ms) may be too short for {totalChannels} channels. Consider increasing to at least {estimatedCycleTime}ms.",
                new[] { nameof(PollIntervalMs) }
            ));
        }

        return results;
    }
}