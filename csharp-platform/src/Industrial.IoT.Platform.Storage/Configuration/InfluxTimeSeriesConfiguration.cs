// Industrial.IoT.Platform.Storage - InfluxDB Configuration
// Configuration model for InfluxDB time-series repository following existing pattern quality

using System.ComponentModel.DataAnnotations;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;

namespace Industrial.IoT.Platform.Storage.Configuration;

/// <summary>
/// Configuration for InfluxDB time-series repository
/// Following validation patterns from existing ADAM logger configuration
/// </summary>
public sealed class InfluxTimeSeriesConfiguration
{
    /// <summary>
    /// InfluxDB server URL
    /// </summary>
    [Required(ErrorMessage = "InfluxDB URL is required")]
    [Url(ErrorMessage = "InfluxDB URL must be a valid URL")]
    public string Url { get; set; } = "http://localhost:8086";

    /// <summary>
    /// InfluxDB authentication token
    /// </summary>
    [Required(ErrorMessage = "InfluxDB token is required")]
    [MinLength(20, ErrorMessage = "InfluxDB token must be at least 20 characters")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// InfluxDB organization name
    /// </summary>
    [Required(ErrorMessage = "InfluxDB organization is required")]
    [MinLength(1, ErrorMessage = "InfluxDB organization cannot be empty")]
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// InfluxDB bucket name for time-series data
    /// </summary>
    [Required(ErrorMessage = "InfluxDB bucket is required")]
    [MinLength(1, ErrorMessage = "InfluxDB bucket cannot be empty")]
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// Default measurement name for data points
    /// </summary>
    [Required(ErrorMessage = "Default measurement name is required")]
    public string DefaultMeasurement { get; set; } = "sensor_data";

    /// <summary>
    /// Batch size for write operations
    /// </summary>
    [Range(1, 10000, ErrorMessage = "BatchSize must be between 1 and 10,000")]
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Flush interval for batch writes in milliseconds
    /// </summary>
    [Range(100, 60000, ErrorMessage = "FlushIntervalMs must be between 100ms and 60 seconds")]
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of retries for failed writes
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry interval in milliseconds
    /// </summary>
    [Range(100, 30000, ErrorMessage = "RetryIntervalMs must be between 100ms and 30 seconds")]
    public int RetryIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "ConnectionTimeoutMs must be between 1 second and 60 seconds")]
    public int ConnectionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "ReadTimeoutMs must be between 1 second and 5 minutes")]
    public int ReadTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Write timeout in milliseconds
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "WriteTimeoutMs must be between 1 second and 5 minutes")]
    public int WriteTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Maximum points per second rate limit (0 = unlimited)
    /// </summary>
    [Range(0, 1000000, ErrorMessage = "MaxPointsPerSecond must be between 0 and 1,000,000")]
    public int MaxPointsPerSecond { get; set; } = 10000;

    /// <summary>
    /// Enable compression for network transfers
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Enable automatic buffer flushing on shutdown
    /// </summary>
    public bool FlushOnShutdown { get; set; } = true;

    /// <summary>
    /// Write precision for timestamps
    /// </summary>
    public WritePrecision WritePrecision { get; set; } = WritePrecision.Ms;

    /// <summary>
    /// Buffer size for write operations
    /// </summary>
    [Range(1000, 1000000, ErrorMessage = "WriteBufferSize must be between 1,000 and 1,000,000")]
    public int WriteBufferSize { get; set; } = 65536;

    /// <summary>
    /// Enable write event logging for debugging
    /// </summary>
    public bool EnableWriteEventLogging { get; set; } = false;

    /// <summary>
    /// Data retention policy (for cleanup operations)
    /// </summary>
    public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Create WriteOptions for InfluxDB client - EXACT pattern from working ADAM-6051 logger
    /// </summary>
    /// <returns>Configured WriteOptions using SYNCHRONOUS writes like Python implementation</returns>
    public WriteOptions CreateWriteOptions()
    {
        // Use SYNCHRONOUS writes for reliability - same as Python ADAM logger
        return WriteOptions.CreateNew()
            .BatchSize(BatchSize)
            .FlushInterval(FlushIntervalMs)
            .Build();
    }

    /// <summary>
    /// Validate configuration and return validation results
    /// Following existing ADAM logger validation patterns
    /// </summary>
    /// <returns>Collection of validation results</returns>
    public IEnumerable<ValidationResult> ValidateConfiguration()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        // Standard data annotation validation
        Validator.TryValidateObject(this, context, results, true);

        // Custom business rule validation
        if (FlushIntervalMs > ReadTimeoutMs)
        {
            results.Add(new ValidationResult(
                "FlushIntervalMs should not exceed ReadTimeoutMs to avoid timeout issues",
                new[] { nameof(FlushIntervalMs), nameof(ReadTimeoutMs) }));
        }

        if (BatchSize * 100 > WriteBufferSize) // Rough estimate: 100 bytes per point
        {
            results.Add(new ValidationResult(
                $"WriteBufferSize ({WriteBufferSize}) may be too small for BatchSize ({BatchSize}). Consider increasing buffer size.",
                new[] { nameof(WriteBufferSize), nameof(BatchSize) }));
        }

        if (MaxPointsPerSecond > 0 && BatchSize > MaxPointsPerSecond)
        {
            results.Add(new ValidationResult(
                "BatchSize should not exceed MaxPointsPerSecond rate limit",
                new[] { nameof(BatchSize), nameof(MaxPointsPerSecond) }));
        }

        // Validate URL format more strictly
        if (!string.IsNullOrEmpty(Url))
        {
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                results.Add(new ValidationResult(
                    "InfluxDB URL must be a valid HTTP or HTTPS URL",
                    new[] { nameof(Url) }));
            }
        }

        // Validate retention period
        if (DefaultRetentionPeriod < TimeSpan.FromHours(1))
        {
            results.Add(new ValidationResult(
                "DefaultRetentionPeriod should be at least 1 hour",
                new[] { nameof(DefaultRetentionPeriod) }));
        }

        if (DefaultRetentionPeriod > TimeSpan.FromDays(3650)) // ~10 years
        {
            results.Add(new ValidationResult(
                "DefaultRetentionPeriod should not exceed 10 years",
                new[] { nameof(DefaultRetentionPeriod) }));
        }

        return results;
    }

    /// <summary>
    /// Create default configuration for development/testing
    /// </summary>
    /// <returns>Default configuration instance</returns>
    public static InfluxTimeSeriesConfiguration CreateDefault()
    {
        return new InfluxTimeSeriesConfiguration
        {
            Url = "http://localhost:8086",
            Token = "development-token-change-in-production",
            Organization = "industrial-iot",
            Bucket = "sensor-data",
            DefaultMeasurement = "sensor_data",
            BatchSize = 1000,
            FlushIntervalMs = 5000,
            MaxRetries = 3,
            RetryIntervalMs = 1000,
            ConnectionTimeoutMs = 10000,
            ReadTimeoutMs = 30000,
            WriteTimeoutMs = 30000,
            MaxPointsPerSecond = 10000,
            EnableCompression = true,
            FlushOnShutdown = true,
            WritePrecision = WritePrecision.Ms,
            WriteBufferSize = 65536,
            EnableWriteEventLogging = false,
            DefaultRetentionPeriod = TimeSpan.FromDays(30)
        };
    }

    /// <summary>
    /// Create production-optimized configuration
    /// </summary>
    /// <returns>Production configuration instance</returns>
    public static InfluxTimeSeriesConfiguration CreateProduction()
    {
        return new InfluxTimeSeriesConfiguration
        {
            Url = "https://your-influxdb-server:8086",
            Token = "your-production-token",
            Organization = "your-organization",
            Bucket = "industrial-sensor-data",
            DefaultMeasurement = "sensor_data",
            BatchSize = 5000,        // Larger batches for better throughput
            FlushIntervalMs = 2000,  // More frequent flushes for lower latency
            MaxRetries = 5,          // More retries for reliability
            RetryIntervalMs = 2000,  // Longer retry intervals
            ConnectionTimeoutMs = 15000,
            ReadTimeoutMs = 60000,   // Longer timeouts for complex queries
            WriteTimeoutMs = 60000,
            MaxPointsPerSecond = 50000, // Higher throughput limit
            EnableCompression = true,
            FlushOnShutdown = true,
            WritePrecision = WritePrecision.Ms,
            WriteBufferSize = 1048576, // 1MB buffer for high throughput
            EnableWriteEventLogging = false, // Disable for performance
            DefaultRetentionPeriod = TimeSpan.FromDays(365) // 1 year retention
        };
    }
}