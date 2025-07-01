// Industrial.IoT.Platform.Devices.Adam - Scale-Specific Data Models
// Data structures representing readings and health information from ADAM-4571 scale devices

using Industrial.IoT.Platform.Core.Interfaces;

namespace Industrial.IoT.Platform.Devices.Adam.Models;

/// <summary>
/// Represents a data reading from an ADAM-4571 scale device with comprehensive metadata and quality information
/// Implements IDataReading to maintain compatibility with existing platform patterns
/// </summary>
public sealed record ScaleDataReading : IDataReading
{
    /// <summary>
    /// Unique identifier for the source ADAM-4571 device
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Scale channel/port identifier (for multi-scale devices)
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// Raw weight value from the scale in its native unit
    /// </summary>
    public required double RawWeight { get; init; }

    /// <summary>
    /// Timestamp when the data was acquired from the device
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Processed weight value after applying tare, calibration, and unit conversion
    /// </summary>
    public double? ProcessedWeight { get; init; }

    /// <summary>
    /// Net weight after tare subtraction
    /// </summary>
    public double? NetWeight { get; init; }

    /// <summary>
    /// Tare weight value currently applied
    /// </summary>
    public double? TareWeight { get; init; }

    /// <summary>
    /// Current stability state of the scale reading
    /// </summary>
    public ScaleStability Stability { get; init; } = ScaleStability.Stable;

    /// <summary>
    /// Quality assessment of the scale reading
    /// </summary>
    public ScaleQuality Quality { get; init; } = ScaleQuality.Good;

    /// <summary>
    /// Platform quality for interface compatibility
    /// </summary>
    public DataQuality PlatformQuality => Quality switch
    {
        ScaleQuality.Good => DataQuality.Good,
        ScaleQuality.Uncertain => DataQuality.Uncertain,
        ScaleQuality.Bad => DataQuality.Bad,
        ScaleQuality.Timeout => DataQuality.Timeout,
        ScaleQuality.DeviceFailure => DataQuality.DeviceFailure,
        ScaleQuality.ConfigurationError => DataQuality.ConfigurationError,
        ScaleQuality.Overload => DataQuality.Overflow,
        ScaleQuality.Underload => DataQuality.Bad,
        _ => DataQuality.Bad
    };

    /// <summary>
    /// Unit of measurement for the weight values
    /// </summary>
    public WeightUnit Unit { get; init; } = WeightUnit.Kilograms;

    /// <summary>
    /// Scale resolution/precision (smallest displayable increment)
    /// </summary>
    public double? Resolution { get; init; }

    /// <summary>
    /// Maximum capacity of the scale
    /// </summary>
    public double? Capacity { get; init; }

    /// <summary>
    /// Time taken to acquire this data from the device
    /// </summary>
    public TimeSpan AcquisitionTime { get; init; }

    /// <summary>
    /// Additional metadata tags for categorization and filtering
    /// </summary>
    public Dictionary<string, object> Tags { get; init; } = new();

    /// <summary>
    /// Error message if the reading failed or has quality issues
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Scale-specific status flags from the device
    /// </summary>
    public ScaleStatusFlags StatusFlags { get; init; } = ScaleStatusFlags.None;

    #region IDataReading Implementation

    /// <summary>
    /// Device identifier for platform compatibility
    /// </summary>
    string IDataReading.DeviceId => DeviceId;

    /// <summary>
    /// Timestamp for platform compatibility
    /// </summary>
    DateTimeOffset IDataReading.Timestamp => Timestamp;

    /// <summary>
    /// Quality for platform compatibility
    /// </summary>
    DataQuality IDataReading.Quality => PlatformQuality;

    /// <summary>
    /// Acquisition time for platform compatibility
    /// </summary>
    TimeSpan IDataReading.AcquisitionTime => AcquisitionTime;

    /// <summary>
    /// Tags for platform compatibility
    /// </summary>
    IReadOnlyDictionary<string, object> IDataReading.Tags => Tags;

    /// <summary>
    /// Error message for platform compatibility
    /// </summary>
    string? IDataReading.ErrorMessage => ErrorMessage;

    #endregion
}

/// <summary>
/// Device health and diagnostic information for ADAM-4571 scale devices
/// Implements IDeviceHealth to maintain compatibility with existing platform patterns
/// </summary>
public sealed record ScaleDeviceHealth : IDeviceHealth
{
    /// <summary>
    /// Unique identifier for the ADAM-4571 device being monitored
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Timestamp when this health status was recorded
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Current operational status of the scale device
    /// </summary>
    public required ScaleDeviceStatus Status { get; init; }

    /// <summary>
    /// Platform status for interface compatibility
    /// </summary>
    public DeviceStatus PlatformStatus => Status switch
    {
        ScaleDeviceStatus.Online => DeviceStatus.Online,
        ScaleDeviceStatus.Warning => DeviceStatus.Warning,
        ScaleDeviceStatus.Error => DeviceStatus.Error,
        ScaleDeviceStatus.Offline => DeviceStatus.Offline,
        ScaleDeviceStatus.Calibrating => DeviceStatus.Warning,
        ScaleDeviceStatus.Unknown => DeviceStatus.Unknown,
        _ => DeviceStatus.Unknown
    };

    /// <summary>
    /// Whether the device is currently connected and responsive
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Time elapsed since the last successful data read
    /// </summary>
    public TimeSpan? LastSuccessfulRead { get; init; }

    /// <summary>
    /// Number of consecutive failed communication attempts
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Average communication latency in milliseconds
    /// </summary>
    public double? CommunicationLatency { get; init; }

    /// <summary>
    /// Last error message encountered during communication
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Total number of read attempts since device was added
    /// </summary>
    public int TotalReads { get; init; }

    /// <summary>
    /// Number of successful read operations
    /// </summary>
    public int SuccessfulReads { get; init; }

    /// <summary>
    /// Success rate as a percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;

    /// <summary>
    /// Scale-specific calibration status
    /// </summary>
    public CalibrationStatus CalibrationStatus { get; init; } = CalibrationStatus.Calibrated;

    /// <summary>
    /// Environmental temperature affecting scale accuracy (if available)
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Scale zero drift amount
    /// </summary>
    public double? ZeroDrift { get; init; }

    /// <summary>
    /// Current scale protocol in use
    /// </summary>
    public string? ActiveProtocol { get; init; }

    #region IDeviceHealth Implementation

    /// <summary>
    /// Device identifier for platform compatibility
    /// </summary>
    string IDeviceHealth.DeviceId => DeviceId;

    /// <summary>
    /// Timestamp for platform compatibility
    /// </summary>
    DateTimeOffset IDeviceHealth.Timestamp => Timestamp;

    /// <summary>
    /// Status for platform compatibility
    /// </summary>
    DeviceStatus IDeviceHealth.Status => PlatformStatus;

    /// <summary>
    /// Connection status for platform compatibility
    /// </summary>
    bool IDeviceHealth.IsConnected => IsConnected;

    /// <summary>
    /// Last successful read for platform compatibility
    /// </summary>
    TimeSpan? IDeviceHealth.LastSuccessfulRead => LastSuccessfulRead;

    /// <summary>
    /// Consecutive failures for platform compatibility
    /// </summary>
    int IDeviceHealth.ConsecutiveFailures => ConsecutiveFailures;

    /// <summary>
    /// Communication latency for platform compatibility
    /// </summary>
    double? IDeviceHealth.CommunicationLatency => CommunicationLatency;

    /// <summary>
    /// Last error for platform compatibility
    /// </summary>
    string? IDeviceHealth.LastError => LastError;

    /// <summary>
    /// Total reads for platform compatibility
    /// </summary>
    int IDeviceHealth.TotalReads => TotalReads;

    /// <summary>
    /// Successful reads for platform compatibility
    /// </summary>
    int IDeviceHealth.SuccessfulReads => SuccessfulReads;

    #endregion
}

/// <summary>
/// Stability state of scale readings, indicating measurement reliability
/// </summary>
public enum ScaleStability
{
    /// <summary>
    /// Reading is stable and reliable
    /// </summary>
    Stable = 0,

    /// <summary>
    /// Reading is settling but not yet stable
    /// </summary>
    Settling = 1,

    /// <summary>
    /// Reading is actively changing or unstable
    /// </summary>
    Unstable = 2,

    /// <summary>
    /// Motion detected on the scale
    /// </summary>
    Motion = 3,

    /// <summary>
    /// Stability status cannot be determined
    /// </summary>
    Unknown = 4
}

/// <summary>
/// Quality assessment for scale readings, indicating reliability and validity
/// </summary>
public enum ScaleQuality
{
    /// <summary>
    /// Data is valid, reliable, and within expected parameters
    /// </summary>
    Good = 0,

    /// <summary>
    /// Data may be questionable but still usable with caution
    /// </summary>
    Uncertain = 1,

    /// <summary>
    /// Data is known to be invalid and should not be used
    /// </summary>
    Bad = 2,

    /// <summary>
    /// Communication timeout occurred during data acquisition
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// Scale hardware failure or malfunction detected
    /// </summary>
    DeviceFailure = 4,

    /// <summary>
    /// Configuration error preventing proper data acquisition
    /// </summary>
    ConfigurationError = 5,

    /// <summary>
    /// Scale overload detected (weight exceeds capacity)
    /// </summary>
    Overload = 6,

    /// <summary>
    /// Scale underload detected (negative weight beyond acceptable range)
    /// </summary>
    Underload = 7
}

/// <summary>
/// Weight units supported by scale devices
/// </summary>
public enum WeightUnit
{
    /// <summary>
    /// Grams (g)
    /// </summary>
    Grams = 0,

    /// <summary>
    /// Kilograms (kg)
    /// </summary>
    Kilograms = 1,

    /// <summary>
    /// Pounds (lb)
    /// </summary>
    Pounds = 2,

    /// <summary>
    /// Ounces (oz)
    /// </summary>
    Ounces = 3,

    /// <summary>
    /// Metric tons (t)
    /// </summary>
    Tons = 4
}

/// <summary>
/// Operational status of a scale device indicating its current health
/// </summary>
public enum ScaleDeviceStatus
{
    /// <summary>
    /// Device is operating normally and responding to requests
    /// </summary>
    Online = 0,

    /// <summary>
    /// Device is operational but experiencing minor issues
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Device has encountered errors but may still be partially functional
    /// </summary>
    Error = 2,

    /// <summary>
    /// Device is not responding to communication attempts
    /// </summary>
    Offline = 3,

    /// <summary>
    /// Device status cannot be determined
    /// </summary>
    Unknown = 4,

    /// <summary>
    /// Scale is in calibration mode
    /// </summary>
    Calibrating = 5
}

/// <summary>
/// Calibration status of the scale device
/// </summary>
public enum CalibrationStatus
{
    /// <summary>
    /// Scale is properly calibrated
    /// </summary>
    Calibrated = 0,

    /// <summary>
    /// Scale calibration is expired or questionable
    /// </summary>
    CalibrationExpired = 1,

    /// <summary>
    /// Scale requires calibration
    /// </summary>
    RequiresCalibration = 2,

    /// <summary>
    /// Calibration is in progress
    /// </summary>
    CalibrationInProgress = 3,

    /// <summary>
    /// Calibration failed
    /// </summary>
    CalibrationFailed = 4,

    /// <summary>
    /// Calibration status unknown
    /// </summary>
    Unknown = 5
}

/// <summary>
/// Scale-specific status flags that can be combined
/// </summary>
[Flags]
public enum ScaleStatusFlags
{
    /// <summary>
    /// No status flags active
    /// </summary>
    None = 0,

    /// <summary>
    /// Scale is in motion
    /// </summary>
    Motion = 1 << 0,

    /// <summary>
    /// Zero point has been set
    /// </summary>
    Zero = 1 << 1,

    /// <summary>
    /// Tare weight is active
    /// </summary>
    Tare = 1 << 2,

    /// <summary>
    /// Scale is overloaded
    /// </summary>
    Overload = 1 << 3,

    /// <summary>
    /// Scale is underloaded
    /// </summary>
    Underload = 1 << 4,

    /// <summary>
    /// Scale needs calibration
    /// </summary>
    NeedsCalibration = 1 << 5,

    /// <summary>
    /// Low battery warning
    /// </summary>
    LowBattery = 1 << 6,

    /// <summary>
    /// Error condition detected
    /// </summary>
    Error = 1 << 7
}

/// <summary>
/// Extension methods for weight unit handling
/// </summary>
public static class WeightUnitExtensions
{
    /// <summary>
    /// Get the display name for a weight unit
    /// </summary>
    /// <param name="unit">Weight unit</param>
    /// <returns>Display name string</returns>
    public static string GetDisplayName(this WeightUnit unit) => unit switch
    {
        WeightUnit.Grams => "g",
        WeightUnit.Kilograms => "kg",
        WeightUnit.Pounds => "lb",
        WeightUnit.Ounces => "oz",
        WeightUnit.Tons => "t",
        _ => unit.ToString()
    };

    /// <summary>
    /// Convert weight value between units
    /// </summary>
    /// <param name="value">Weight value to convert</param>
    /// <param name="fromUnit">Source unit</param>
    /// <param name="toUnit">Target unit</param>
    /// <returns>Converted weight value</returns>
    public static double Convert(double value, WeightUnit fromUnit, WeightUnit toUnit)
    {
        if (fromUnit == toUnit) return value;

        // Convert to grams first
        var grams = fromUnit switch
        {
            WeightUnit.Grams => value,
            WeightUnit.Kilograms => value * 1000,
            WeightUnit.Pounds => value * 453.592,
            WeightUnit.Ounces => value * 28.3495,
            WeightUnit.Tons => value * 1000000,
            _ => throw new ArgumentException($"Unsupported source unit: {fromUnit}")
        };

        // Convert from grams to target unit
        return toUnit switch
        {
            WeightUnit.Grams => grams,
            WeightUnit.Kilograms => grams / 1000,
            WeightUnit.Pounds => grams / 453.592,
            WeightUnit.Ounces => grams / 28.3495,
            WeightUnit.Tons => grams / 1000000,
            _ => throw new ArgumentException($"Unsupported target unit: {toUnit}")
        };
    }
}