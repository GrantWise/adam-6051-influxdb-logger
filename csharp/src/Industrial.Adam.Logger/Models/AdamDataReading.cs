// Industrial.Adam.Logger - Core Data Models
// Data structures representing readings and health information from ADAM devices

namespace Industrial.Adam.Logger.Models;

/// <summary>
/// Represents a data reading from an ADAM device channel with comprehensive metadata and quality information
/// </summary>
public sealed record AdamDataReading
{
    /// <summary>
    /// Unique identifier for the source ADAM device
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Channel number on the device (typically 0-based)
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// Raw unprocessed value from the Modbus register
    /// </summary>
    public required long RawValue { get; init; }

    /// <summary>
    /// Timestamp when the data was acquired from the device
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Processed value after applying scaling, offset, and transformation
    /// </summary>
    public double? ProcessedValue { get; init; }

    /// <summary>
    /// Rate of change calculated over a time window (units per second)
    /// </summary>
    public double? Rate { get; init; }

    /// <summary>
    /// Quality assessment of the data reading
    /// </summary>
    public DataQuality Quality { get; init; } = DataQuality.Good;

    /// <summary>
    /// Unit of measurement for the processed value
    /// </summary>
    public string? Unit { get; init; }

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
}

/// <summary>
/// Device health and diagnostic information providing comprehensive status monitoring
/// </summary>
public sealed record AdamDeviceHealth
{
    /// <summary>
    /// Unique identifier for the ADAM device being monitored
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Timestamp when this health status was recorded
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Current operational status of the device
    /// </summary>
    public required DeviceStatus Status { get; init; }

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
}

/// <summary>
/// Quality assessment for data readings, indicating reliability and validity
/// </summary>
public enum DataQuality
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
    /// Device hardware failure or malfunction detected
    /// </summary>
    DeviceFailure = 4,

    /// <summary>
    /// Configuration error preventing proper data acquisition
    /// </summary>
    ConfigurationError = 5,

    /// <summary>
    /// Counter overflow detected (value exceeds expected range)
    /// </summary>
    Overflow = 6
}

/// <summary>
/// Operational status of an ADAM device indicating its current health
/// </summary>
public enum DeviceStatus
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
    Unknown = 4
}