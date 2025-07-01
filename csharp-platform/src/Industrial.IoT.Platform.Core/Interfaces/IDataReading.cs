// Industrial.IoT.Platform.Core - Core Data Abstractions
// Base interfaces for device data readings compatible with existing ADAM logger patterns

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Base interface for all device data readings providing common metadata and quality information
/// Compatible with existing AdamDataReading patterns from Industrial.Adam.Logger
/// </summary>
public interface IDataReading
{
    /// <summary>
    /// Unique identifier for the source device
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Timestamp when the data was acquired from the device
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Quality assessment of the data reading
    /// </summary>
    DataQuality Quality { get; }

    /// <summary>
    /// Time taken to acquire this data from the device
    /// </summary>
    TimeSpan AcquisitionTime { get; }

    /// <summary>
    /// Additional metadata tags for categorization and filtering
    /// </summary>
    IReadOnlyDictionary<string, object> Tags { get; }

    /// <summary>
    /// Error message if the reading failed or has quality issues
    /// </summary>
    string? ErrorMessage { get; }
}

/// <summary>
/// Quality assessment for data readings, indicating reliability and validity
/// Compatible with existing DataQuality enum from Industrial.Adam.Logger
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
    /// Value overflow detected (value exceeds expected range)
    /// </summary>
    Overflow = 6
}