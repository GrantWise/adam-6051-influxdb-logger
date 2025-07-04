// Industrial.Adam.Logger.Tests - DefaultDataProcessor Unit Tests
// Comprehensive tests for data processing implementation (22 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Services;

/// <summary>
/// Unit tests for DefaultDataProcessor (22 tests planned)
/// </summary>
public class DefaultDataProcessorTests
{
    #region Constructor Tests (2 tests)

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator();
        var mockTransformer = TestMockFactory.CreateMockTransformer();
        var mockLogger = TestMockFactory.CreateMockLogger<DefaultDataProcessor>();

        // Act
        var processor = new DefaultDataProcessor(
            mockValidator.Object,
            mockTransformer.Object,
            mockLogger.Object);

        // Assert
        processor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator();
        var mockTransformer = TestMockFactory.CreateMockTransformer();
        var mockLogger = TestMockFactory.CreateMockLogger<DefaultDataProcessor>();

        // Act & Assert
        Action createWithNullValidator = () => 
            new DefaultDataProcessor(null!, mockTransformer.Object, mockLogger.Object);
        createWithNullValidator.Should().Throw<ArgumentNullException>();
            
        Action createWithNullTransformer = () => 
            new DefaultDataProcessor(mockValidator.Object, null!, mockLogger.Object);
        createWithNullTransformer.Should().Throw<ArgumentNullException>();
            
        Action createWithNullLogger = () => 
            new DefaultDataProcessor(mockValidator.Object, mockTransformer.Object, null!);
        createWithNullLogger.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ProcessRawData Success Tests (5 tests)

    [Fact]
    public void ProcessRawData_ValidSingleRegister_ShouldReturnReading()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var channel = TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel");
        var registers = new ushort[] { 1234 };
        var timestamp = DateTimeOffset.UtcNow;
        var acquisitionTime = TimeSpan.FromMilliseconds(50);

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, timestamp, acquisitionTime);

        // Assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be("TEST_DEVICE");
        result.Channel.Should().Be(channel.ChannelNumber);
        result.RawValue.Should().Be(1234);
        result.Timestamp.Should().Be(timestamp);
        result.AcquisitionTime.Should().Be(acquisitionTime);
        result.Unit.Should().Be(channel.Unit);
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessRawData_ValidDoubleRegister_ShouldCombineRegisters()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var channel = TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel");
        channel.RegisterCount = 2;
        var registers = new ushort[] { 0x1234, 0x5678 }; // Little-endian: 0x56781234
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, timestamp, TimeSpan.Zero);

        // Assert
        result.Should().NotBeNull();
        var expectedValue = ((long)0x5678 << 16) | 0x1234; // Little-endian combination
        result.RawValue.Should().Be(expectedValue);
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessRawData_WithScaling_ShouldApplyTransformation()
    {
        // Arrange
        var mockTransformer = TestMockFactory.CreateMockTransformer(1234.56);
        var processor = CreateProcessor(transformer: mockTransformer);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.ProcessedValue.Should().Be(1234.56);
        mockTransformer.Verify(t => t.TransformValue(1000, channel), Times.Once);
    }

    [Fact]
    public void ProcessRawData_WithValidation_ShouldSetQuality()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator(DataQuality.Uncertain);
        var processor = CreateProcessor(validator: mockValidator);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.Quality.Should().Be(DataQuality.Uncertain);
        mockValidator.Verify(v => v.ValidateReading(It.IsAny<AdamDataReading>(), channel), Times.Once);
    }

    [Fact]
    public void ProcessRawData_WithTagEnrichment_ShouldIncludeTags()
    {
        // Arrange
        var mockTransformer = TestMockFactory.CreateMockTransformer();
        var processor = CreateProcessor(transformer: mockTransformer);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.Tags.Add("sensor_type", "counter");
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.Tags.Should().NotBeNull();
        result.Tags.Should().ContainKey("data_source");
        result.Tags.Should().ContainKey("channel_name");
        mockTransformer.Verify(t => t.EnrichTags(channel.Tags, null!, channel), Times.Once);
    }

    #endregion

    #region ProcessRawData Error Handling Tests (3 tests)

    [Fact]
    public void ProcessRawData_TransformerThrows_ShouldReturnErrorReading()
    {
        // Arrange
        var mockTransformer = TestMockFactory.CreateThrowingMockTransformer("Transformation failed");
        var processor = CreateProcessor(transformer: mockTransformer);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.Should().NotBeNull();
        result.Quality.Should().Be(DataQuality.ConfigurationError);
        result.ErrorMessage.Should().Contain("Transformation failed");
        result.RawValue.Should().Be(0);
    }

    [Fact]
    public void ProcessRawData_ValidatorThrows_ShouldReturnErrorReading()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateThrowingMockValidator("Validation failed");
        var processor = CreateProcessor(validator: mockValidator);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.Should().NotBeNull();
        result.Quality.Should().Be(DataQuality.ConfigurationError);
        result.ErrorMessage.Should().Contain("Validation failed");
    }

    [Fact]
    public void ProcessRawData_Exception_ShouldLogError()
    {
        // Arrange
        var mockLogger = TestMockFactory.CreateMockLogger<DefaultDataProcessor>();
        var mockValidator = TestMockFactory.CreateThrowingMockValidator("Test exception");
        var processor = CreateProcessor(validator: mockValidator, logger: mockLogger);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };

        // Act
        var result = processor.ProcessRawData("TEST_DEVICE", channel, registers, DateTimeOffset.UtcNow, TimeSpan.Zero);

        // Assert
        result.Quality.Should().Be(DataQuality.ConfigurationError);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region CalculateRate Tests (8 tests)

    [Fact]
    public void CalculateRate_FirstReading_ShouldReturnNull()
    {
        // Arrange
        var processor = CreateDefaultProcessor();

        // Act
        var result = processor.CalculateRate("DEVICE_001", 1, 1000, DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateRate_SecondReading_ShouldCalculateRate()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, baseTime);
        var result = processor.CalculateRate("DEVICE_001", 1, 1100, baseTime.AddSeconds(10));

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(10.0); // (1100 - 1000) / 10 seconds = 10 units/second
    }

    [Fact]
    public void CalculateRate_MultipleReadings_ShouldUseOldestAndNewest()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, baseTime);
        processor.CalculateRate("DEVICE_001", 1, 1050, baseTime.AddSeconds(5));
        processor.CalculateRate("DEVICE_001", 1, 1100, baseTime.AddSeconds(10));
        var result = processor.CalculateRate("DEVICE_001", 1, 1200, baseTime.AddSeconds(20));

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(10.0); // (1200 - 1000) / 20 seconds = 10 units/second
    }

    [Fact]
    public void CalculateRate_CounterOverflow_ShouldHandleCorrectly()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;
        var maxValue = Constants.UInt32MaxValue;

        // Act
        processor.CalculateRate("DEVICE_001", 1, maxValue - 100, baseTime);
        var result = processor.CalculateRate("DEVICE_001", 1, 100, baseTime.AddSeconds(10));

        // Assert
        result.Should().NotBeNull();
        var expectedDiff = (maxValue - (maxValue - 100)) + 100; // Overflow calculation
        var expectedRate = expectedDiff / 10.0;
        result.Should().Be(expectedRate);
    }

    [Fact]
    public void CalculateRate_ZeroTimeDifference_ShouldReturnNull()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, timestamp);
        var result = processor.CalculateRate("DEVICE_001", 1, 1100, timestamp); // Same timestamp

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateRate_OldDataCleanup_ShouldRemoveOldEntries()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;
        var oldTime = baseTime.AddMinutes(-10); // Older than 5-minute window

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, oldTime);
        processor.CalculateRate("DEVICE_001", 1, 1050, baseTime.AddMinutes(-1));
        var result = processor.CalculateRate("DEVICE_001", 1, 1100, baseTime);

        // Assert
        result.Should().NotBeNull();
        // Should calculate from 1-minute ago entry, not 10-minute old entry
        result.Should().BeApproximately(0.8333, 0.001); // (1100 - 1050) / 60 seconds ≈ 0.83 units/second
    }

    [Fact]
    public void CalculateRate_DifferentDevices_ShouldIsolateData()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, baseTime);
        processor.CalculateRate("DEVICE_002", 1, 2000, baseTime);
        
        var result1 = processor.CalculateRate("DEVICE_001", 1, 1100, baseTime.AddSeconds(10));
        var result2 = processor.CalculateRate("DEVICE_002", 1, 2200, baseTime.AddSeconds(10));

        // Assert
        result1.Should().Be(10.0); // (1100 - 1000) / 10
        result2.Should().Be(20.0); // (2200 - 2000) / 10
    }

    [Fact]
    public void CalculateRate_DifferentChannels_ShouldIsolateData()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        processor.CalculateRate("DEVICE_001", 1, 1000, baseTime);
        processor.CalculateRate("DEVICE_001", 2, 2000, baseTime);
        
        var result1 = processor.CalculateRate("DEVICE_001", 1, 1100, baseTime.AddSeconds(10));
        var result2 = processor.CalculateRate("DEVICE_001", 2, 2200, baseTime.AddSeconds(10));

        // Assert
        result1.Should().Be(10.0); // (1100 - 1000) / 10
        result2.Should().Be(20.0); // (2200 - 2000) / 10
    }

    #endregion

    #region ValidateReading Tests (2 tests)

    [Fact]
    public void ValidateReading_ValidInput_ShouldCallValidator()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator(DataQuality.Good);
        var processor = CreateProcessor(validator: mockValidator);
        var channel = TestConfigurationBuilder.ValidChannelConfig();

        // Act
        var result = processor.ValidateReading(channel, 1000, 5.0);

        // Assert
        result.Should().Be(DataQuality.Good);
        mockValidator.Verify(v => v.ValidateReading(
            It.Is<AdamDataReading>(r => 
                r.Channel == channel.ChannelNumber && 
                r.RawValue == 1000 && 
                r.Rate == 5.0), 
            channel), 
            Times.Once);
    }

    [Fact]
    public void ValidateReading_WithNullRate_ShouldHandleCorrectly()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator(DataQuality.Uncertain);
        var processor = CreateProcessor(validator: mockValidator);
        var channel = TestConfigurationBuilder.ValidChannelConfig();

        // Act
        var result = processor.ValidateReading(channel, 1000, null);

        // Assert
        result.Should().Be(DataQuality.Uncertain);
        mockValidator.Verify(v => v.ValidateReading(
            It.Is<AdamDataReading>(r => 
                r.RawValue == 1000 && 
                r.Rate == null), 
            channel), 
            Times.Once);
    }

    #endregion

    #region Integration Tests (2 tests)

    [Fact]
    public void ProcessRawData_CompleteWorkflow_ShouldCalculateRateOnSecondCall()
    {
        // Arrange
        var processor = CreateDefaultProcessor();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        var firstResult = processor.ProcessRawData("DEVICE_001", channel, registers, baseTime, TimeSpan.Zero);
        
        registers[0] = 1100;
        var secondResult = processor.ProcessRawData("DEVICE_001", channel, registers, baseTime.AddSeconds(10), TimeSpan.Zero);

        // Assert
        firstResult.Rate.Should().BeNull();
        secondResult.Rate.Should().NotBeNull();
        secondResult.Rate.Should().Be(10.0); // (1100 - 1000) / 10 seconds
    }

    [Fact]
    public void ProcessRawData_TransformationFailsButRateCalculationSucceeds_ShouldHandleGracefully()
    {
        // Arrange
        var mockValidator = TestMockFactory.CreateMockValidator();
        var mockTransformer = TestMockFactory.CreateMockTransformer(null); // Returns null for processed value
        mockTransformer.Setup(t => t.TransformValue(It.IsAny<long>(), It.IsAny<ChannelConfig>()))
                      .Returns((double?)null);
                      
        var processor = CreateProcessor(validator: mockValidator, transformer: mockTransformer);
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        var registers = new ushort[] { 1000 };
        var baseTime = DateTimeOffset.UtcNow;

        // Act
        var firstResult = processor.ProcessRawData("DEVICE_001", channel, registers, baseTime, TimeSpan.Zero);
        
        registers[0] = 1100;
        var secondResult = processor.ProcessRawData("DEVICE_001", channel, registers, baseTime.AddSeconds(10), TimeSpan.Zero);

        // Assert
        firstResult.ProcessedValue.Should().BeNull();
        firstResult.Rate.Should().BeNull(); // No rate when processed value is null
        
        secondResult.ProcessedValue.Should().BeNull();
        secondResult.Rate.Should().BeNull(); // Still no rate calculation
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create a DefaultDataProcessor with default mocks
    /// </summary>
    private static DefaultDataProcessor CreateDefaultProcessor()
    {
        var mockValidator = TestMockFactory.CreateMockValidator();
        var mockTransformer = TestMockFactory.CreateMockTransformer();
        var mockLogger = TestMockFactory.CreateMockLogger<DefaultDataProcessor>();
        
        return new DefaultDataProcessor(
            mockValidator.Object,
            mockTransformer.Object,
            mockLogger.Object);
    }

    /// <summary>
    /// Create a DefaultDataProcessor with specific mocks
    /// </summary>
    private static DefaultDataProcessor CreateProcessor(
        Mock<Industrial.Adam.Logger.Interfaces.IDataValidator>? validator = null,
        Mock<Industrial.Adam.Logger.Interfaces.IDataTransformer>? transformer = null,
        Mock<ILogger<DefaultDataProcessor>>? logger = null)
    {
        return new DefaultDataProcessor(
            (validator ?? TestMockFactory.CreateMockValidator()).Object,
            (transformer ?? TestMockFactory.CreateMockTransformer()).Object,
            (logger ?? TestMockFactory.CreateMockLogger<DefaultDataProcessor>()).Object);
    }

    #endregion
}