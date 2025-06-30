// Industrial.Adam.Logger.Tests - DefaultDataValidator Unit Tests
// Comprehensive tests for data validation implementation (15 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Services;

/// <summary>
/// Unit tests for DefaultDataValidator (15 tests planned)
/// </summary>
public class DefaultDataValidatorTests
{
    #region ValidateReading Tests (5 tests)

    [Fact]
    public void ValidateReading_AllValidationsPass_ShouldReturnGood()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var reading = TestData.ValidReading();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 0;
        channel.MaxValue = 2000000;
        channel.MaxRateOfChange = 1000;

        // Act
        var result = validator.ValidateReading(reading, channel);

        // Assert
        result.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ValidateReading_ValueOutOfRange_ShouldReturnBad()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var reading = TestData.ValidReading();
        reading = reading with { RawValue = 2000000 }; // Out of range
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 0;
        channel.MaxValue = 1000000; // Lower than reading value

        // Act
        var result = validator.ValidateReading(reading, channel);

        // Assert
        result.Should().Be(DataQuality.Bad);
    }

    [Fact]
    public void ValidateReading_RateExceedsLimit_ShouldReturnUncertain()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var reading = TestData.ValidReading();
        reading = reading with { Rate = 2000.0 }; // High rate
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MaxRateOfChange = 1000.0; // Lower than reading rate

        // Act
        var result = validator.ValidateReading(reading, channel);

        // Assert
        result.Should().Be(DataQuality.Uncertain);
    }

    [Fact]
    public void ValidateReading_OverflowCondition_ShouldReturnOverflow()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var reading = TestData.ValidReading();
        reading = reading with { RawValue = 2000000000L }; // High value
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        // Set range to allow the value but overflow threshold lower
        channel.MinValue = 0;
        channel.MaxValue = 3000000000L; // Higher than reading value
        channel.Tags.Add("overflow_threshold", 1000000000L); // Lower threshold

        // Act
        var result = validator.ValidateReading(reading, channel);

        // Assert
        result.Should().Be(DataQuality.Overflow);
    }

    [Fact]
    public void ValidateReading_NoOverflowThreshold_ShouldUseDefault()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var reading = TestData.ValidReading();
        reading = reading with { RawValue = Constants.DefaultOverflowThreshold + 1000 }; // Above default threshold
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        // Set range to allow the value
        channel.MinValue = 0;
        channel.MaxValue = Constants.DefaultOverflowThreshold + 10000; // Higher than reading value
        // No overflow_threshold tag set - should use default

        // Act
        var result = validator.ValidateReading(reading, channel);

        // Assert
        result.Should().Be(DataQuality.Overflow);
    }

    #endregion

    #region IsValidRange Tests (5 tests)

    [Fact]
    public void IsValidRange_ValueWithinRange_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 100;
        channel.MaxValue = 1000;

        // Act
        var result = validator.IsValidRange(500, channel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidRange_ValueAtMinimum_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 100;
        channel.MaxValue = 1000;

        // Act
        var result = validator.IsValidRange(100, channel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidRange_ValueAtMaximum_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 100;
        channel.MaxValue = 1000;

        // Act
        var result = validator.IsValidRange(1000, channel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidRange_ValueBelowMinimum_ShouldReturnFalse()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 100;
        channel.MaxValue = 1000;

        // Act
        var result = validator.IsValidRange(50, channel);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidRange_ValueAboveMaximum_ShouldReturnFalse()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = 100;
        channel.MaxValue = 1000;

        // Act
        var result = validator.IsValidRange(1500, channel);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidRateOfChange Tests (3 tests)

    [Fact]
    public void IsValidRateOfChange_RateWithinLimit_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MaxRateOfChange = 100.0;

        // Act
        var result = validator.IsValidRateOfChange(50.0, channel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidRateOfChange_RateExceedsLimit_ShouldReturnFalse()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MaxRateOfChange = 100.0;

        // Act
        var result = validator.IsValidRateOfChange(150.0, channel);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidRateOfChange_NegativeRateExceedsLimit_ShouldReturnFalse()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MaxRateOfChange = 100.0;

        // Act
        var result = validator.IsValidRateOfChange(-150.0, channel);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Special Conditions Tests (2 tests)

    [Fact]
    public void IsValidRange_NoLimitsConfigured_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.MinValue = null;
        channel.MaxValue = null;

        // Act
        var result = validator.IsValidRange(long.MaxValue, channel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidRateOfChange_NoRateOrNoLimit_ShouldReturnTrue()
    {
        // Arrange
        var validator = new DefaultDataValidator();
        var channel = TestConfigurationBuilder.ValidChannelConfig();

        // Act & Assert
        // No rate provided
        validator.IsValidRateOfChange(null, channel).Should().BeTrue();

        // No limit configured
        channel.MaxRateOfChange = null;
        validator.IsValidRateOfChange(1000.0, channel).Should().BeTrue();
    }

    #endregion
}