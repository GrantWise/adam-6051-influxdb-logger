// Industrial.Adam.Logger - Channel Configuration
// Configuration for individual channels on ADAM devices

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Configuration;

/// <summary>
/// Configuration for a specific channel on an ADAM device, including Modbus addressing and data processing settings
/// </summary>
public class ChannelConfig : IValidatableObject
{
    /// <summary>
    /// Channel number on the device (typically 0-based)
    /// </summary>
    [Range(0, 255, ErrorMessage = "ChannelNumber must be between 0 and 255")]
    public int ChannelNumber { get; set; }

    /// <summary>
    /// Human-readable name for this channel
    /// </summary>
    [Required(ErrorMessage = "Channel name is required")]
    [StringLength(Constants.MaxChannelNameLength, ErrorMessage = "Channel name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this channel measures
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this channel is actively monitored
    /// </summary>
    public bool Enabled { get; set; } = true;

    #region Modbus Register Configuration

    /// <summary>
    /// Starting Modbus register address for this channel
    /// </summary>
    [Range(0, Constants.MaxModbusRegisterAddress, ErrorMessage = "StartRegister must be between 0 and 65535")]
    public ushort StartRegister { get; set; }

    /// <summary>
    /// Number of consecutive registers to read (typically 2 for 32-bit counters)
    /// </summary>
    [Range(1, 4, ErrorMessage = "RegisterCount must be between 1 and 4")]
    public int RegisterCount { get; set; } = Constants.CounterRegisterCount;

    #endregion

    #region Data Processing Configuration

    /// <summary>
    /// Scaling factor applied to raw values (multiplier)
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Offset value added after scaling
    /// </summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// Unit of measurement for processed values
    /// </summary>
    public string Unit { get; set; } = DefaultUnits.Counts;

    /// <summary>
    /// Number of decimal places for processed values
    /// </summary>
    [Range(0, 10, ErrorMessage = "DecimalPlaces must be between 0 and 10")]
    public int DecimalPlaces { get; set; } = Constants.DefaultDecimalPlaces;

    #endregion

    #region Validation Limits

    /// <summary>
    /// Minimum valid value for this channel (null = no limit)
    /// </summary>
    public long? MinValue { get; set; }

    /// <summary>
    /// Maximum valid value for this channel (null = no limit)
    /// </summary>
    public long? MaxValue { get; set; }

    /// <summary>
    /// Maximum allowed rate of change per second (null = no limit)
    /// </summary>
    public double? MaxRateOfChange { get; set; }

    #endregion

    /// <summary>
    /// Custom metadata tags specific to this channel
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = new();

    /// <summary>
    /// Validates the channel configuration and returns any validation errors
    /// </summary>
    /// <param name="validationContext">Validation context for the operation</param>
    /// <returns>Collection of validation results indicating any configuration errors</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate channel name
        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Channel name is required and cannot be empty", new[] { nameof(Name) });

        // Validate value range consistency
        if (MinValue.HasValue && MaxValue.HasValue && MinValue > MaxValue)
            yield return new ValidationResult("MinValue cannot be greater than MaxValue", new[] { nameof(MinValue), nameof(MaxValue) });

        // Validate scale factor
        if (ScaleFactor == 0)
            yield return new ValidationResult("ScaleFactor cannot be zero", new[] { nameof(ScaleFactor) });

        // Validate decimal places with scale factor
        if (DecimalPlaces > 0 && ScaleFactor == Math.Floor(ScaleFactor))
        {
            // Warning: using decimal places with integer scale factor might not be necessary
            // This is just a validation note, not an error
        }

        // Validate rate of change limit
        if (MaxRateOfChange.HasValue && MaxRateOfChange <= 0)
            yield return new ValidationResult("MaxRateOfChange must be positive if specified", new[] { nameof(MaxRateOfChange) });

        // Validate register configuration
        if (RegisterCount <= 0)
            yield return new ValidationResult("RegisterCount must be positive", new[] { nameof(RegisterCount) });

        // Check for potential register address overflow
        if ((long)StartRegister + RegisterCount > Constants.MaxModbusRegisterAddress)
            yield return new ValidationResult("StartRegister + RegisterCount exceeds maximum Modbus address range", new[] { nameof(StartRegister), nameof(RegisterCount) });
    }
}