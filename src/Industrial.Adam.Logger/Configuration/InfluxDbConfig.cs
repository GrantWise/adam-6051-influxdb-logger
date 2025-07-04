// Industrial.Adam.Logger - InfluxDB Configuration
// Configuration for InfluxDB data storage

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Configuration;

/// <summary>
/// Configuration for InfluxDB data storage
/// </summary>
public class InfluxDbConfig
{
    /// <summary>
    /// InfluxDB server URL
    /// </summary>
    [Required(ErrorMessage = "InfluxDB URL is required")]
    [Url(ErrorMessage = "Invalid InfluxDB URL format")]
    public string Url { get; set; } = "http://localhost:8086";

    /// <summary>
    /// InfluxDB authentication token
    /// </summary>
    [Required(ErrorMessage = "InfluxDB token is required")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// InfluxDB organization name
    /// </summary>
    [Required(ErrorMessage = "InfluxDB organization is required")]
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// InfluxDB bucket name for storing data
    /// </summary>
    [Required(ErrorMessage = "InfluxDB bucket is required")]
    public string Bucket { get; set; } = "adam-data";

    /// <summary>
    /// Measurement name for storing counter data
    /// </summary>
    public string Measurement { get; set; } = "adam_counters";

    /// <summary>
    /// Batch size for writing data points to InfluxDB
    /// </summary>
    [Range(1, 10000, ErrorMessage = "WriteBatchSize must be between 1 and 10,000")]
    public int WriteBatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval for writing batched data in milliseconds
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "FlushIntervalMs must be between 1 second and 5 minutes")]
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "TimeoutMs must be between 1 second and 1 minute")]
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Enable automatic retry on write failures
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    [Range(100, 10000, ErrorMessage = "RetryDelayMs must be between 100ms and 10 seconds")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Enable debug logging for InfluxDB operations
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Enable compression for HTTP requests
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Additional tags to add to all data points
    /// </summary>
    public Dictionary<string, string> GlobalTags { get; set; } = new();

    /// <summary>
    /// Validates the InfluxDB configuration
    /// </summary>
    /// <returns>Collection of validation results</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        Validator.TryValidateObject(this, context, results, true);

        // Additional validation
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            results.Add(new ValidationResult("Invalid InfluxDB URL format", new[] { nameof(Url) }));
        }
        else if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            results.Add(new ValidationResult("InfluxDB URL must use HTTP or HTTPS", new[] { nameof(Url) }));
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            results.Add(new ValidationResult("InfluxDB token cannot be empty", new[] { nameof(Token) }));
        }

        if (string.IsNullOrWhiteSpace(Organization))
        {
            results.Add(new ValidationResult("InfluxDB organization cannot be empty", new[] { nameof(Organization) }));
        }

        if (string.IsNullOrWhiteSpace(Bucket))
        {
            results.Add(new ValidationResult("InfluxDB bucket cannot be empty", new[] { nameof(Bucket) }));
        }

        if (string.IsNullOrWhiteSpace(Measurement))
        {
            results.Add(new ValidationResult("InfluxDB measurement cannot be empty", new[] { nameof(Measurement) }));
        }

        return results;
    }
}