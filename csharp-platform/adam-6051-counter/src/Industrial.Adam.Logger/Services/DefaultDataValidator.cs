// Industrial.Adam.Logger - Default Data Validation Implementation
// Default implementation for validating data readings against configuration rules

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;

namespace Industrial.Adam.Logger.Services;

/// <summary>
/// Default implementation of data validation using configuration-based rules
/// </summary>
public class DefaultDataValidator : IDataValidator
{
    /// <summary>
    /// Validate a complete data reading using standard validation rules
    /// </summary>
    /// <param name="reading">Complete data reading to validate</param>
    /// <param name="channelConfig">Channel configuration for validation context</param>
    /// <returns>Quality assessment based on validation results</returns>
    public DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channelConfig)
    {
        // Check basic range validation
        if (!IsValidRange(reading.RawValue, channelConfig))
            return DataQuality.Bad;

        // Check rate of change validation
        if (!IsValidRateOfChange(reading.Rate, channelConfig))
            return DataQuality.Uncertain;

        // Check for potential overflow conditions
        var overflowThreshold = channelConfig.Tags.GetValueOrDefault("overflow_threshold", Constants.DefaultOverflowThreshold);
        if (overflowThreshold is long threshold && reading.RawValue > threshold)
            return DataQuality.Overflow;

        // All validations passed
        return DataQuality.Good;
    }

    /// <summary>
    /// Check if a value falls within the configured valid range
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="channelConfig">Channel configuration with min/max limits</param>
    /// <returns>True if the value is within range</returns>
    public bool IsValidRange(long value, ChannelConfig channelConfig)
    {
        // Check minimum value constraint
        if (channelConfig.MinValue.HasValue && value < channelConfig.MinValue.Value)
            return false;

        // Check maximum value constraint
        if (channelConfig.MaxValue.HasValue && value > channelConfig.MaxValue.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Check if the rate of change is within acceptable limits
    /// </summary>
    /// <param name="rate">Rate of change to validate</param>
    /// <param name="channelConfig">Channel configuration with rate limits</param>
    /// <returns>True if the rate of change is acceptable</returns>
    public bool IsValidRateOfChange(double? rate, ChannelConfig channelConfig)
    {
        // No rate calculated or no limit configured
        if (!rate.HasValue || !channelConfig.MaxRateOfChange.HasValue)
            return true;

        // Check if absolute rate exceeds the configured limit
        return Math.Abs(rate.Value) <= channelConfig.MaxRateOfChange.Value;
    }
}