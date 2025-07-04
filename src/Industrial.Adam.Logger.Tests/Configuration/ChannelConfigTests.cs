// Industrial.Adam.Logger.Tests - ChannelConfig Validation Tests
// Comprehensive tests for channel configuration validation (20 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Tests.TestHelpers;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Configuration;

/// <summary>
/// Unit tests for ChannelConfig validation (20 tests planned)
/// </summary>
public class ChannelConfigTests
{
    #region Valid Configuration Tests (3 tests)

    [Fact]
    public void ValidChannelConfig_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void MinimalValidConfig_ShouldPassValidation()
    {
        // Arrange
        var config = new ChannelConfig
        {
            ChannelNumber = 0,
            Name = "MinimalChannel"
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidConfig_WithAllOptionalProperties_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.Description = "Test channel with all properties";
        config.MinValue = 0;
        config.MaxValue = 1000000;
        config.MaxRateOfChange = 500.0;
        config.Tags.Add("sensor_type", "optical");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Channel Name Validation Tests (3 tests)

    [Fact]
    public void Name_Empty_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("empty_name");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(ChannelConfig.Name)));
    }

    [Fact]
    public void Name_Null_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.Name = null!;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(ChannelConfig.Name)));
    }

    [Fact]
    public void Name_TooLong_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("long_name");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.Name)) &&
            r.ErrorMessage!.Contains("100 characters"));
    }

    #endregion

    #region Channel Number Validation Tests (2 tests)

    [Fact]
    public void ChannelNumber_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("invalid_channel_number");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.ChannelNumber)) &&
            r.ErrorMessage!.Contains("between 0 and 255"));
    }

    [Fact]
    public void ChannelNumber_ValidRange_ShouldPassValidation()
    {
        // Arrange & Act & Assert
        var validChannelNumbers = new[] { 0, 1, 100, 255 };
        
        foreach (var channelNumber in validChannelNumbers)
        {
            var config = TestConfigurationBuilder.ValidChannelConfig(channelNumber);
            
            var validationResults = ValidateConfiguration(config);
            
            validationResults.Should().BeEmpty($"Channel number {channelNumber} should be valid");
        }
    }

    #endregion

    #region Register Configuration Tests (3 tests)

    [Fact]
    public void RegisterCount_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("invalid_register_count");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.RegisterCount)) &&
            r.ErrorMessage!.Contains("between 1 and 4"));
    }

    [Fact]
    public void StartRegister_ValidRange_ShouldPassValidation()
    {
        // Arrange & Act & Assert
        var validStartRegisters = new ushort[] { 0, 100, 1000, 65533 }; // 65533 + 2 = 65535 (max)
        
        foreach (var startRegister in validStartRegisters)
        {
            var config = TestConfigurationBuilder.ValidChannelConfig();
            config.StartRegister = startRegister;
            config.RegisterCount = 1; // Ensure no overflow
            
            var validationResults = ValidateConfiguration(config);
            
            validationResults.Should().BeEmpty($"Start register {startRegister} should be valid");
        }
    }

    [Fact]
    public void RegisterOverflow_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("register_overflow");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.ErrorMessage!.Contains("exceeds maximum Modbus address range"));
    }

    #endregion

    #region Scale Factor and Transformation Tests (3 tests)

    [Fact]
    public void ScaleFactor_Zero_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("zero_scale_factor");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.ScaleFactor)) &&
            r.ErrorMessage!.Contains("cannot be zero"));
    }

    [Fact]
    public void DecimalPlaces_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("invalid_decimal_places");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.DecimalPlaces)) &&
            r.ErrorMessage!.Contains("between 0 and 10"));
    }

    [Fact]
    public void TransformationProperties_ValidValues_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.ScaleFactor = 0.001;
        config.Offset = -273.15; // Temperature conversion
        config.DecimalPlaces = 2;
        config.Unit = "°C";

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Value Range Validation Tests (2 tests)

    [Fact]
    public void MinMaxValue_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("invalid_min_max_range");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.ErrorMessage!.Contains("MinValue cannot be greater than MaxValue"));
    }

    [Fact]
    public void MinMaxValue_ValidRange_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.MinValue = 0;
        config.MaxValue = 1000000;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Rate of Change Validation Tests (2 tests)

    [Fact]
    public void MaxRateOfChange_Negative_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidChannelConfig("invalid_rate_limit");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(ChannelConfig.MaxRateOfChange)) &&
            r.ErrorMessage!.Contains("must be positive"));
    }

    [Fact]
    public void MaxRateOfChange_Positive_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.MaxRateOfChange = 1000.5;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Optional Properties Tests (2 tests)

    [Fact]
    public void OptionalProperties_Null_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.Description = null;
        config.MinValue = null;
        config.MaxValue = null;
        config.MaxRateOfChange = null;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void DisabledChannel_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidChannelConfig();
        config.Enabled = false;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validate a channel configuration and return validation results
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation results</returns>
    private static List<ValidationResult> ValidateConfiguration(ChannelConfig config)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(config);
        
        // Use both DataAnnotations validation and custom validation
        Validator.TryValidateObject(config, context, results, true);
        results.AddRange(config.Validate(context));
        
        return results;
    }

    #endregion
}