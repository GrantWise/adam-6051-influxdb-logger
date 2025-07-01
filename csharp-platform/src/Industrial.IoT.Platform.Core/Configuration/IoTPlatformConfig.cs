// Industrial.IoT.Platform.Core - Main Platform Configuration
// Configuration for the IoT Platform service following Industrial.Adam.Logger patterns

using System.ComponentModel.DataAnnotations;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Core.Configuration;

/// <summary>
/// Main configuration for the Industrial IoT Platform, following patterns from AdamLoggerConfig
/// </summary>
public class IoTPlatformConfig
{
    /// <summary>
    /// List of device providers to manage
    /// </summary>
    [Required(ErrorMessage = "At least one device provider must be configured")]
    public List<IDeviceConfiguration> Devices { get; set; } = new();

    #region Timing Configuration

    /// <summary>
    /// Global polling interval in milliseconds for all devices
    /// </summary>
    [Range(Constants.MinPollIntervalMs, Constants.MaxPollIntervalMs, 
        ErrorMessage = "PollIntervalMs must be between 100ms and 5 minutes")]
    public int PollIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

    /// <summary>
    /// Health check interval in milliseconds
    /// </summary>
    [Range(5000, 300000, ErrorMessage = "HealthCheckIntervalMs must be between 5 seconds and 5 minutes")]
    public int HealthCheckIntervalMs { get; set; } = Constants.DefaultHealthCheckIntervalMs;

    #endregion

    #region Performance Configuration

    /// <summary>
    /// Maximum number of devices to manage concurrently
    /// </summary>
    [Range(1, Constants.MaxConcurrentDevices, ErrorMessage = "MaxConcurrentDevices must be between 1 and 50")]
    public int MaxConcurrentDevices { get; set; } = Constants.DefaultMaxConcurrentDevices;

    /// <summary>
    /// Internal data buffer size for storing readings
    /// </summary>
    [Range(100, Constants.MaxDataBufferSize, ErrorMessage = "DataBufferSize must be between 100 and 100,000")]
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
    [Range(1, Constants.MaxDeviceTimeoutMinutes, ErrorMessage = "DeviceTimeoutMinutes must be between 1 and 60 minutes")]
    public int DeviceTimeoutMinutes { get; set; } = Constants.DefaultDeviceTimeoutMinutes;

    #endregion

    #region Protocol Discovery Configuration

    /// <summary>
    /// Default confidence threshold for protocol discovery (0-100)
    /// </summary>
    [Range(0, 100, ErrorMessage = "ConfidenceThreshold must be between 0 and 100")]
    public double ConfidenceThreshold { get; set; } = Constants.DefaultConfidenceThreshold;

    /// <summary>
    /// Maximum number of discovery iterations before giving up
    /// </summary>
    [Range(1, 50, ErrorMessage = "MaxDiscoveryIterations must be between 1 and 50")]
    public int MaxDiscoveryIterations { get; set; } = Constants.DefaultMaxDiscoveryIterations;

    /// <summary>
    /// Timeout for capturing data frames during discovery (milliseconds)
    /// </summary>
    [Range(500, 30000, ErrorMessage = "FrameTimeoutMs must be between 500ms and 30 seconds")]
    public int FrameTimeoutMs { get; set; } = Constants.DefaultFrameTimeoutMs;

    #endregion

    #region Storage Configuration

    /// <summary>
    /// Default storage configuration for time-series data
    /// </summary>
    public StoragePolicy TimeSeriesStorage { get; set; } = new()
    {
        DataClassification = DataClassification.TimeSeries,
        PrimaryBackend = Constants.DefaultTimeSeriesStorage,
        BatchSize = Constants.DefaultStorageBatchSize,
        FlushInterval = TimeSpan.FromMilliseconds(Constants.DefaultBatchTimeoutMs)
    };

    /// <summary>
    /// Default storage configuration for transactional data
    /// </summary>
    public StoragePolicy TransactionalStorage { get; set; } = new()
    {
        DataClassification = DataClassification.DiscreteReading,
        PrimaryBackend = Constants.DefaultTransactionalStorage,
        BatchSize = Constants.DefaultStorageBatchSize,
        FlushInterval = TimeSpan.FromMilliseconds(Constants.DefaultBatchTimeoutMs)
    };

    /// <summary>
    /// Default storage configuration for configuration data
    /// </summary>
    public StoragePolicy ConfigurationStorage { get; set; } = new()
    {
        DataClassification = DataClassification.Configuration,
        PrimaryBackend = Constants.DefaultConfigurationStorage,
        BatchSize = 1, // Configuration changes are typically immediate
        FlushInterval = TimeSpan.FromSeconds(1)
    };

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

    /// <summary>
    /// Performance counter collection interval in milliseconds
    /// </summary>
    [Range(10000, 300000, ErrorMessage = "PerformanceCounterIntervalMs must be between 10 seconds and 5 minutes")]
    public int PerformanceCounterIntervalMs { get; set; } = Constants.DefaultPerformanceCounterIntervalMs;

    #endregion

    /// <summary>
    /// Validates the entire configuration and returns any validation errors
    /// Follows the same pattern as AdamLoggerConfig.ValidateConfiguration()
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
            // Basic device validation
            if (string.IsNullOrWhiteSpace(device.value.DeviceId))
            {
                results.Add(new ValidationResult(
                    $"Device at index {device.index} has empty DeviceId",
                    new[] { $"{nameof(Devices)}[{device.index}].{nameof(device.value.DeviceId)}" }
                ));
            }

            if (string.IsNullOrWhiteSpace(device.value.DeviceType))
            {
                results.Add(new ValidationResult(
                    $"Device '{device.value.DeviceId}' has empty DeviceType",
                    new[] { $"{nameof(Devices)}[{device.index}].{nameof(device.value.DeviceType)}" }
                ));
            }
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

        // Validate discovery configuration consistency
        if (ConfidenceThreshold < 50)
        {
            results.Add(new ValidationResult(
                "ConfidenceThreshold below 50% may result in unreliable protocol discovery results.",
                new[] { nameof(ConfidenceThreshold) }
            ));
        }

        // Validate storage configuration
        ValidateStoragePolicy(TimeSeriesStorage, nameof(TimeSeriesStorage), results);
        ValidateStoragePolicy(TransactionalStorage, nameof(TransactionalStorage), results);
        ValidateStoragePolicy(ConfigurationStorage, nameof(ConfigurationStorage), results);

        return results;
    }

    /// <summary>
    /// Validates a storage policy configuration
    /// </summary>
    /// <param name="policy">Storage policy to validate</param>
    /// <param name="propertyName">Property name for error reporting</param>
    /// <param name="results">Validation results to append to</param>
    private static void ValidateStoragePolicy(StoragePolicy policy, string propertyName, List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(policy.PrimaryBackend))
        {
            results.Add(new ValidationResult(
                $"{propertyName}.PrimaryBackend cannot be null or empty",
                new[] { $"{propertyName}.{nameof(StoragePolicy.PrimaryBackend)}" }
            ));
        }

        if (policy.BatchSize <= 0)
        {
            results.Add(new ValidationResult(
                $"{propertyName}.BatchSize must be greater than 0",
                new[] { $"{propertyName}.{nameof(StoragePolicy.BatchSize)}" }
            ));
        }

        if (policy.FlushInterval <= TimeSpan.Zero)
        {
            results.Add(new ValidationResult(
                $"{propertyName}.FlushInterval must be greater than zero",
                new[] { $"{propertyName}.{nameof(StoragePolicy.FlushInterval)}" }
            ));
        }
    }
}