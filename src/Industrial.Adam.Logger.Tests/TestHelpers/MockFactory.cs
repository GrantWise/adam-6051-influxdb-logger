// Industrial.Adam.Logger.Tests - Mock Factory
// Factory for creating mocked dependencies for unit testing

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace Industrial.Adam.Logger.Tests.TestHelpers;

/// <summary>
/// Factory for creating mocked dependencies for unit testing
/// </summary>
public static class TestMockFactory
{
    /// <summary>
    /// Create a mock IDataValidator with configurable behavior
    /// </summary>
    /// <param name="defaultQuality">Default quality to return from ValidateReading</param>
    /// <param name="defaultRangeValid">Default result for IsValidRange</param>
    /// <param name="defaultRateValid">Default result for IsValidRateOfChange</param>
    /// <returns>Configured mock validator</returns>
    public static Mock<IDataValidator> CreateMockValidator(
        DataQuality defaultQuality = DataQuality.Good,
        bool defaultRangeValid = true,
        bool defaultRateValid = true)
    {
        var mock = new Mock<IDataValidator>();
        
        mock.Setup(v => v.ValidateReading(It.IsAny<AdamDataReading>(), It.IsAny<ChannelConfig>()))
            .Returns(defaultQuality);
            
        mock.Setup(v => v.IsValidRange(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
            .Returns(defaultRangeValid);
            
        mock.Setup(v => v.IsValidRateOfChange(It.IsAny<double?>(), It.IsAny<ChannelConfig>()))
            .Returns(defaultRateValid);
            
        return mock;
    }

    /// <summary>
    /// Create a mock IDataTransformer with configurable behavior
    /// </summary>
    /// <param name="transformResult">Result to return from TransformValue (null = return scaled input)</param>
    /// <returns>Configured mock transformer</returns>
    public static Mock<IDataTransformer> CreateMockTransformer(double? transformResult = null)
    {
        var mock = new Mock<IDataTransformer>();
        
        if (transformResult.HasValue)
        {
            mock.Setup(t => t.TransformValue(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
                .Returns(transformResult.Value);
        }
        else
        {
            // Default behavior: apply scaling and offset
            mock.Setup(t => t.TransformValue(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
                .Returns<long, ChannelConfig>((value, config) => value * config.ScaleFactor + config.Offset);
        }
        
        mock.Setup(t => t.EnrichTags(It.IsAny<Dictionary<string, object>>(), It.IsAny<AdamDeviceConfig>(), It.IsAny<ChannelConfig>()))
            .Returns<Dictionary<string, object>, AdamDeviceConfig, ChannelConfig>((baseTags, deviceConfig, channelConfig) =>
            {
                var enrichedTags = new Dictionary<string, object>(baseTags)
                {
                    { "data_source", "test_transformer" },
                    { "channel_name", channelConfig.Name },
                    { "timestamp_utc", DateTimeOffset.UtcNow.ToString("O") }
                };
                return enrichedTags;
            });
            
        return mock;
    }

    /// <summary>
    /// Create a mock ILogger for testing
    /// </summary>
    /// <typeparam name="T">Type being logged</typeparam>
    /// <returns>Mock logger</returns>
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mock = new Mock<ILogger<T>>();
        
        // Setup the Log method to capture log calls
        mock.Setup(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Verifiable();
            
        return mock;
    }

    /// <summary>
    /// Create a mock validator that returns specific quality for specific conditions
    /// </summary>
    /// <param name="badRangeValues">Values that should return Bad quality</param>
    /// <param name="uncertainRateValues">Rate values that should return Uncertain quality</param>
    /// <returns>Configured mock validator</returns>
    public static Mock<IDataValidator> CreateConditionalMockValidator(
        IEnumerable<long>? badRangeValues = null,
        IEnumerable<double>? uncertainRateValues = null)
    {
        var mock = new Mock<IDataValidator>();
        
        mock.Setup(v => v.ValidateReading(It.IsAny<AdamDataReading>(), It.IsAny<ChannelConfig>()))
            .Returns<AdamDataReading, ChannelConfig>((reading, config) =>
            {
                // Check if value is in bad range
                if (badRangeValues != null && badRangeValues.Contains(reading.RawValue))
                    return DataQuality.Bad;
                    
                // Check if rate is uncertain
                if (uncertainRateValues != null && reading.Rate.HasValue && 
                    uncertainRateValues.Contains(reading.Rate.Value))
                    return DataQuality.Uncertain;
                    
                return DataQuality.Good;
            });
            
        mock.Setup(v => v.IsValidRange(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
            .Returns<long, ChannelConfig>((value, config) =>
                badRangeValues == null || !badRangeValues.Contains(value));
                
        mock.Setup(v => v.IsValidRateOfChange(It.IsAny<double?>(), It.IsAny<ChannelConfig>()))
            .Returns<double?, ChannelConfig>((rate, config) =>
                uncertainRateValues == null || !rate.HasValue || !uncertainRateValues.Contains(rate.Value));
                
        return mock;
    }

    /// <summary>
    /// Create a mock transformer that throws an exception for testing error handling
    /// </summary>
    /// <param name="exceptionMessage">Exception message to throw</param>
    /// <returns>Mock transformer that throws</returns>
    public static Mock<IDataTransformer> CreateThrowingMockTransformer(string exceptionMessage = "Mock transformer error")
    {
        var mock = new Mock<IDataTransformer>();
        
        mock.Setup(t => t.TransformValue(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
            .Throws(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(t => t.EnrichTags(It.IsAny<Dictionary<string, object>>(), It.IsAny<AdamDeviceConfig>(), It.IsAny<ChannelConfig>()))
            .Throws(new InvalidOperationException(exceptionMessage));
            
        return mock;
    }

    /// <summary>
    /// Create a mock validator that throws an exception for testing error handling
    /// </summary>
    /// <param name="exceptionMessage">Exception message to throw</param>
    /// <returns>Mock validator that throws</returns>
    public static Mock<IDataValidator> CreateThrowingMockValidator(string exceptionMessage = "Mock validator error")
    {
        var mock = new Mock<IDataValidator>();
        
        mock.Setup(v => v.ValidateReading(It.IsAny<AdamDataReading>(), It.IsAny<ChannelConfig>()))
            .Throws(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(v => v.IsValidRange(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
            .Throws(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(v => v.IsValidRateOfChange(It.IsAny<double?>(), It.IsAny<ChannelConfig>()))
            .Throws(new InvalidOperationException(exceptionMessage));
            
        return mock;
    }
}