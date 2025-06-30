// Industrial.Adam.Logger - Data Processing Interfaces
// Interfaces for processing, validating, and transforming data from ADAM devices

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Models;

namespace Industrial.Adam.Logger.Interfaces;

/// <summary>
/// Interface for processing raw Modbus data into application-specific readings
/// Implement this interface to customize how raw device data is processed and validated
/// </summary>
public interface IDataProcessor
{
    /// <summary>
    /// Process raw Modbus register data into a structured reading with quality assessment
    /// </summary>
    /// <param name="deviceId">Unique identifier for the source device</param>
    /// <param name="channel">Channel configuration including validation rules and transformation parameters</param>
    /// <param name="registers">Raw Modbus register values from the device</param>
    /// <param name="timestamp">Timestamp when the data was acquired</param>
    /// <param name="acquisitionTime">Time taken to acquire the data from the device</param>
    /// <returns>Processed data reading with quality assessment and transformed values</returns>
    AdamDataReading ProcessRawData(
        string deviceId, 
        ChannelConfig channel, 
        ushort[] registers, 
        DateTimeOffset timestamp, 
        TimeSpan acquisitionTime);

    /// <summary>
    /// Calculate rate of change for counter values over time
    /// </summary>
    /// <param name="deviceId">Unique identifier for the source device</param>
    /// <param name="channelNumber">Channel number for the counter</param>
    /// <param name="currentValue">Current counter value</param>
    /// <param name="timestamp">Timestamp of the current reading</param>
    /// <returns>Rate of change in units per second, or null if insufficient data</returns>
    double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp);

    /// <summary>
    /// Validate a reading against channel configuration limits
    /// </summary>
    /// <param name="channel">Channel configuration with validation rules</param>
    /// <param name="rawValue">Raw value to validate</param>
    /// <param name="rate">Calculated rate of change (if available)</param>
    /// <returns>Quality assessment indicating whether the data is valid</returns>
    DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate);
}

/// <summary>
/// Interface for custom data validation logic beyond basic range checking
/// Implement this interface to add domain-specific validation rules
/// </summary>
public interface IDataValidator
{
    /// <summary>
    /// Validate a complete data reading using custom business rules
    /// </summary>
    /// <param name="reading">Complete data reading to validate</param>
    /// <param name="channelConfig">Channel configuration for validation context</param>
    /// <returns>Quality assessment based on validation results</returns>
    DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channelConfig);

    /// <summary>
    /// Check if a value falls within the configured valid range
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="channelConfig">Channel configuration with min/max limits</param>
    /// <returns>True if the value is within range</returns>
    bool IsValidRange(long value, ChannelConfig channelConfig);

    /// <summary>
    /// Check if the rate of change is within acceptable limits
    /// </summary>
    /// <param name="rate">Rate of change to validate</param>
    /// <param name="channelConfig">Channel configuration with rate limits</param>
    /// <returns>True if the rate of change is acceptable</returns>
    bool IsValidRateOfChange(double? rate, ChannelConfig channelConfig);
}

/// <summary>
/// Interface for custom data transformation logic including scaling and metadata enrichment
/// Implement this interface to add application-specific data transformations
/// </summary>
public interface IDataTransformer
{
    /// <summary>
    /// Transform raw values using scaling, offset, and custom logic
    /// </summary>
    /// <param name="rawValue">Raw value from the device</param>
    /// <param name="channelConfig">Channel configuration with transformation parameters</param>
    /// <returns>Transformed value ready for application use</returns>
    double? TransformValue(long rawValue, ChannelConfig channelConfig);

    /// <summary>
    /// Enrich metadata tags with additional context and information
    /// </summary>
    /// <param name="baseTags">Base tags from the channel configuration</param>
    /// <param name="deviceConfig">Device configuration for additional context</param>
    /// <param name="channelConfig">Channel configuration for specific metadata</param>
    /// <returns>Enriched tags dictionary with additional metadata</returns>
    Dictionary<string, object> EnrichTags(
        Dictionary<string, object> baseTags, 
        AdamDeviceConfig deviceConfig, 
        ChannelConfig channelConfig);
}