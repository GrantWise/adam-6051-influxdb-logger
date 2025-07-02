// Industrial.Adam.Logger - Default Data Processing Implementation
// Default implementations for data processing, validation, and transformation

using System.Collections.Concurrent;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Services;

/// <summary>
/// Default implementation of data processing with rate calculation and validation
/// </summary>
public class DefaultDataProcessor : IDataProcessor
{
    private readonly ConcurrentDictionary<string, Dictionary<int, List<(DateTimeOffset timestamp, long value)>>> _rateHistory = new();
    private readonly IDataValidator _validator;
    private readonly IDataTransformer _transformer;
    private readonly ILogger<DefaultDataProcessor> _logger;

    /// <summary>
    /// Initialize the data processor with validation and transformation services
    /// </summary>
    /// <param name="validator">Service for validating data readings</param>
    /// <param name="transformer">Service for transforming raw values</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public DefaultDataProcessor(
        IDataValidator validator,
        IDataTransformer transformer,
        ILogger<DefaultDataProcessor> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Process raw Modbus register data into a structured reading with quality assessment
    /// </summary>
    /// <param name="deviceId">Unique identifier for the source device</param>
    /// <param name="channel">Channel configuration including validation rules and transformation parameters</param>
    /// <param name="registers">Raw Modbus register values from the device</param>
    /// <param name="timestamp">Timestamp when the data was acquired</param>
    /// <param name="acquisitionTime">Time taken to acquire the data from the device</param>
    /// <returns>Processed data reading with quality assessment and transformed values</returns>
    public AdamDataReading ProcessRawData(
        string deviceId, 
        ChannelConfig channel, 
        ushort[] registers, 
        DateTimeOffset timestamp,
        TimeSpan acquisitionTime)
    {
        try
        {
            // Convert registers to 32-bit value (assuming little-endian)
            long rawValue = registers.Length >= Constants.CounterRegisterCount 
                ? ((long)registers[1] << Constants.ModbusRegisterBits) | registers[0]
                : registers[0];

            // Apply transformation (scaling and offset)
            var processedValue = _transformer.TransformValue(rawValue, channel);

            // Calculate rate if enabled and we have a processed value
            double? rate = null;
            if (processedValue.HasValue)
            {
                rate = CalculateRate(deviceId, channel.ChannelNumber, rawValue, timestamp);
            }

            // Create the base reading for validation
            var baseReading = new AdamDataReading
            {
                DeviceId = deviceId,
                Channel = channel.ChannelNumber,
                RawValue = rawValue,
                ProcessedValue = processedValue,
                Rate = rate,
                Timestamp = timestamp,
                Unit = channel.Unit,
                AcquisitionTime = acquisitionTime
            };

            // Validate the reading
            var quality = _validator.ValidateReading(baseReading, channel);

            // Create the final reading with enriched tags
            var reading = baseReading with
            {
                Quality = quality,
                Tags = _transformer.EnrichTags(channel.Tags, null!, channel) // Device config would be passed in real implementation
            };

            return reading;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data for device {DeviceId}, channel {Channel}", 
                deviceId, channel.ChannelNumber);

            return new AdamDataReading
            {
                DeviceId = deviceId,
                Channel = channel.ChannelNumber,
                RawValue = 0,
                Timestamp = timestamp,
                Quality = DataQuality.ConfigurationError,
                ErrorMessage = ex.Message,
                AcquisitionTime = acquisitionTime,
                Unit = channel.Unit
            };
        }
    }

    /// <summary>
    /// Calculate rate of change for counter values over time using a sliding window
    /// </summary>
    /// <param name="deviceId">Unique identifier for the source device</param>
    /// <param name="channelNumber">Channel number for the counter</param>
    /// <param name="currentValue">Current counter value</param>
    /// <param name="timestamp">Timestamp of the current reading</param>
    /// <returns>Rate of change in units per second, or null if insufficient data</returns>
    public double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp)
    {
        // Ensure device entry exists
        if (!_rateHistory.ContainsKey(deviceId))
            _rateHistory[deviceId] = new Dictionary<int, List<(DateTimeOffset, long)>>();

        // Ensure channel entry exists
        if (!_rateHistory[deviceId].ContainsKey(channelNumber))
            _rateHistory[deviceId][channelNumber] = new List<(DateTimeOffset, long)>();

        var history = _rateHistory[deviceId][channelNumber];
        
        // Add current reading to history
        history.Add((timestamp, currentValue));

        // Clean old entries (keep only last 5 minutes of data for rate calculation)
        var cutoff = timestamp.AddMinutes(-Constants.DefaultRateCalculationWindowMinutes);
        history.RemoveAll(h => h.Item1 < cutoff);

        // Need at least 2 data points to calculate rate
        if (history.Count < 2)
            return null;

        // Calculate rate using oldest and newest values
        var oldest = history.First();
        var newest = history.Last();

        var timeDiff = (newest.Item1 - oldest.Item1).TotalSeconds;
        if (timeDiff <= 0)
            return null;

        // Handle counter overflow/reset
        var valueDiff = newest.Item2 - oldest.Item2;
        if (valueDiff < 0)
        {
            // Assume 32-bit counter overflow
            valueDiff = (Constants.UInt32MaxValue - oldest.Item2) + newest.Item2;
        }

        return valueDiff / timeDiff;
    }

    /// <summary>
    /// Validate a reading against channel configuration limits
    /// </summary>
    /// <param name="channel">Channel configuration with validation rules</param>
    /// <param name="rawValue">Raw value to validate</param>
    /// <param name="rate">Calculated rate of change (if available)</param>
    /// <returns>Quality assessment indicating whether the data is valid</returns>
    public DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate)
    {
        var tempReading = new AdamDataReading 
        { 
            DeviceId = "", 
            Channel = channel.ChannelNumber, 
            RawValue = rawValue, 
            Rate = rate,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        return _validator.ValidateReading(tempReading, channel);
    }
}