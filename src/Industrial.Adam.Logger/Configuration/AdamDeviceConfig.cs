// Industrial.Adam.Logger - Device Configuration Models
// Configuration classes for ADAM devices and channels with comprehensive validation

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Configuration;

/// <summary>
/// Configuration for an ADAM device including connection settings, channels, and operational parameters
/// </summary>
public class AdamDeviceConfig : IValidatableObject
{
    /// <summary>
    /// Unique identifier for this device instance
    /// </summary>
    [Required(ErrorMessage = "DeviceId is required")]
    [StringLength(Constants.MaxDeviceIdLength, ErrorMessage = "DeviceId must be 50 characters or less")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the ADAM device on the network
    /// </summary>
    [Required(ErrorMessage = "IP Address is required")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// TCP port for Modbus communication (default 502)
    /// </summary>
    [Range(Constants.MinPortNumber, Constants.MaxPortNumber, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = Constants.DefaultModbusPort;

    /// <summary>
    /// Modbus unit identifier (slave address)
    /// </summary>
    [Range(Constants.MinModbusUnitId, Constants.MaxModbusUnitId, ErrorMessage = "UnitId must be between 1 and 255")]
    public byte UnitId { get; set; } = Constants.MinModbusUnitId;

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

    /// <summary>
    /// List of channels to monitor on this device
    /// </summary>
    [Required(ErrorMessage = "At least one channel must be configured")]
    public List<ChannelConfig> Channels { get; set; } = new();

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

    #region Data Processing Options

    /// <summary>
    /// Enable automatic rate of change calculation for counter values
    /// </summary>
    public bool EnableRateCalculation { get; set; } = true;

    /// <summary>
    /// Time window in seconds for rate calculation
    /// </summary>
    [Range(10, 3600, ErrorMessage = "RateWindowSeconds must be between 10 seconds and 1 hour")]
    public int RateWindowSeconds { get; set; } = Constants.DefaultRateWindowSeconds;

    /// <summary>
    /// Enable data validation using configured limits and rules
    /// </summary>
    public bool EnableDataValidation { get; set; } = true;

    /// <summary>
    /// Threshold value for detecting counter overflow conditions
    /// </summary>
    [Range(1000000, Constants.UInt32MaxValue, ErrorMessage = "OverflowThreshold must be reasonable for 32-bit counters")]
    public long OverflowThreshold { get; set; } = Constants.DefaultOverflowThreshold;

    #endregion

    /// <summary>
    /// Custom metadata tags that will be attached to all readings from this device
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = new();

    /// <summary>
    /// Validates the device configuration and returns any validation errors
    /// </summary>
    /// <param name="validationContext">Validation context for the operation</param>
    /// <returns>Collection of validation results indicating any configuration errors</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate DeviceId
        if (string.IsNullOrWhiteSpace(DeviceId))
            yield return new ValidationResult("DeviceId is required and cannot be empty", new[] { nameof(DeviceId) });

        // Validate IP Address format
        if (string.IsNullOrWhiteSpace(IpAddress) || !System.Net.IPAddress.TryParse(IpAddress, out _))
            yield return new ValidationResult("Valid IP address is required", new[] { nameof(IpAddress) });

        // Validate channels collection
        if (!Channels.Any())
            yield return new ValidationResult("At least one channel must be configured", new[] { nameof(Channels) });

        // Check for duplicate channel numbers
        var duplicateChannels = Channels.GroupBy(c => c.ChannelNumber).Where(g => g.Count() > 1);
        if (duplicateChannels.Any())
        {
            var duplicateNumbers = string.Join(", ", duplicateChannels.Select(g => g.Key));
            yield return new ValidationResult($"Duplicate channel numbers are not allowed: {duplicateNumbers}", new[] { nameof(Channels) });
        }

        // Validate individual channels
        foreach (var channel in Channels.Select((value, index) => new { value, index }))
        {
            var channelValidationContext = new ValidationContext(channel.value)
            {
                MemberName = $"{nameof(Channels)}[{channel.index}]"
            };
            
            var channelResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(channel.value, channelValidationContext, channelResults, true))
            {
                foreach (var result in channelResults)
                {
                    yield return new ValidationResult(
                        $"Channel {channel.value.ChannelNumber}: {result.ErrorMessage}",
                        result.MemberNames.Select(name => $"{nameof(Channels)}[{channel.index}].{name}")
                    );
                }
            }
        }
    }
}