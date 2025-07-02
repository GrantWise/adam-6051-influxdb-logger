// Industrial.Adam.ScaleLogger - Scale Data Reading Models
// Following proven ADAM-6051 data model patterns

namespace Industrial.Adam.ScaleLogger.Models;

/// <summary>
/// Represents a scale weight reading from ADAM-4571 device
/// Following proven ADAM-6051 data reading patterns
/// </summary>
public sealed record ScaleDataReading
{
    /// <summary>
    /// Unique identifier for the source device
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Device name/location for identification
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Serial port channel on ADAM-4571 (1-8)
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// Timestamp when the reading was acquired
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Weight value in the scale's native unit
    /// </summary>
    public required double WeightValue { get; init; }

    /// <summary>
    /// Raw weight string as received from scale
    /// </summary>
    public required string RawValue { get; init; }

    /// <summary>
    /// Unit of measurement (kg, lb, g, oz, etc.)
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Scale status (stable, unstable, overload, etc.)
    /// </summary>
    public string Status { get; init; } = "unknown";

    /// <summary>
    /// Weight in standardized kilograms for consistency
    /// </summary>
    public required decimal StandardizedWeightKg { get; init; }

    /// <summary>
    /// Whether the reading is stable
    /// </summary>
    public bool IsStable { get; init; } = false;

    /// <summary>
    /// Whether the reading represents an error
    /// </summary>
    public bool IsError { get; init; } = false;

    /// <summary>
    /// Quality of the reading
    /// </summary>
    public DataQuality Quality { get; init; } = DataQuality.Good;

    /// <summary>
    /// Time taken to acquire this reading
    /// </summary>
    public TimeSpan AcquisitionTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Scale manufacturer
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Scale model
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Protocol template used for communication
    /// </summary>
    public string? ProtocolTemplate { get; init; }

    /// <summary>
    /// Error message if reading failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}

/// <summary>
/// Scale device health information
/// Following ADAM-6051 health monitoring patterns
/// </summary>
public sealed record ScaleDeviceHealth
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Device name
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Health check timestamp
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Overall device status
    /// </summary>
    public required DeviceStatus Status { get; init; }

    /// <summary>
    /// Whether device is connected and responsive
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// Time since last successful reading
    /// </summary>
    public TimeSpan? LastSuccessfulRead { get; init; }

    /// <summary>
    /// Number of consecutive communication failures
    /// </summary>
    public int ConsecutiveFailures { get; init; } = 0;

    /// <summary>
    /// Average communication latency
    /// </summary>
    public double? CommunicationLatency { get; init; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Total read attempts
    /// </summary>
    public int TotalReads { get; init; } = 0;

    /// <summary>
    /// Successful read operations
    /// </summary>
    public int SuccessfulReads { get; init; } = 0;

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;

    /// <summary>
    /// Protocol template currently in use
    /// </summary>
    public string? ProtocolTemplate { get; init; }

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = 
        new Dictionary<string, object>();
}

/// <summary>
/// Data quality enumeration following ADAM-6051 patterns
/// </summary>
public enum DataQuality
{
    Good = 0,
    Uncertain = 1,
    Bad = 2,
    Timeout = 3,
    DeviceFailure = 4,
    ConfigurationError = 5,
    Overflow = 6
}

/// <summary>
/// Device status enumeration following ADAM-6051 patterns
/// </summary>
public enum DeviceStatus
{
    Online = 0,
    Warning = 1,
    Error = 2,
    Offline = 3,
    Unknown = 4
}

/// <summary>
/// Protocol template definition for different scale types
/// </summary>
public sealed record ProtocolTemplate
{
    /// <summary>
    /// Template identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Scale manufacturer
    /// </summary>
    public required string Manufacturer { get; init; }

    /// <summary>
    /// Scale model or series
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Template description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Commands to send to scale
    /// </summary>
    public required List<string> Commands { get; init; }

    /// <summary>
    /// Expected response patterns
    /// </summary>
    public required List<string> ExpectedResponses { get; init; }

    /// <summary>
    /// Regex pattern for parsing weight data
    /// </summary>
    public required string WeightPattern { get; init; }

    /// <summary>
    /// Default unit of measurement
    /// </summary>
    public string Unit { get; init; } = "kg";

    /// <summary>
    /// Communication settings
    /// </summary>
    public Dictionary<string, object> Settings { get; init; } = new();
}