// Industrial.Adam.Logger.Tests - AdamDeviceConfig Validation Tests
// Comprehensive tests for device configuration validation (25 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Tests.TestHelpers;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Configuration;

/// <summary>
/// Unit tests for AdamDeviceConfig validation (25 tests planned)
/// </summary>
public class AdamDeviceConfigTests
{
    #region Valid Configuration Tests (5 tests)

    [Fact]
    public void ValidDeviceConfig_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void MinimalValidConfig_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.MinimalDeviceConfig();

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ComprehensiveValidConfig_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ComprehensiveDeviceConfig();

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidConfig_WithOptionalProperties_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.KeepAlive = false;
        config.EnableNagle = true;
        config.EnableRateCalculation = false;
        config.EnableDataValidation = false;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidConfig_WithCustomSettings_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.Port = 1502;
        config.UnitId = 10;
        config.TimeoutMs = 10000;
        config.MaxRetries = 5;
        config.RetryDelayMs = 2000;
        config.RateWindowSeconds = 120;
        config.OverflowThreshold = 2000000000L;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region DeviceId Validation Tests (4 tests)

    [Fact]
    public void DeviceId_Empty_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("empty_device_id");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(AdamDeviceConfig.DeviceId)));
    }

    [Fact]
    public void DeviceId_Null_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.DeviceId = null!;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(AdamDeviceConfig.DeviceId)));
    }

    [Fact]
    public void DeviceId_Whitespace_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.DeviceId = "   ";

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(AdamDeviceConfig.DeviceId)));
    }

    [Fact]
    public void DeviceId_TooLong_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("long_device_id");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.DeviceId)) &&
            r.ErrorMessage!.Contains("50 characters"));
    }

    #endregion

    #region IP Address Validation Tests (3 tests)

    [Fact]
    public void IpAddress_Invalid_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_ip");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(AdamDeviceConfig.IpAddress)));
    }

    [Fact]
    public void IpAddress_Empty_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.IpAddress = string.Empty;

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(AdamDeviceConfig.IpAddress)));
    }

    [Fact]
    public void IpAddress_ValidFormats_ShouldPassValidation()
    {
        // Arrange & Act & Assert
        var validIpAddresses = new[] { "192.168.1.1", "10.0.0.1", "127.0.0.1", "255.255.255.255" };
        
        foreach (var ipAddress in validIpAddresses)
        {
            var config = TestConfigurationBuilder.ValidDeviceConfig();
            config.IpAddress = ipAddress;
            
            var validationResults = ValidateConfiguration(config);
            
            validationResults.Should().BeEmpty($"IP address {ipAddress} should be valid");
        }
    }

    #endregion

    #region Port Validation Tests (2 tests)

    [Fact]
    public void Port_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_port");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.Port)) &&
            r.ErrorMessage!.Contains("between 1 and 65535"));
    }

    [Fact]
    public void Port_ValidRange_ShouldPassValidation()
    {
        // Arrange & Act & Assert
        var validPorts = new[] { 1, 502, 1502, 8080, 65535 };
        
        foreach (var port in validPorts)
        {
            var config = TestConfigurationBuilder.ValidDeviceConfig();
            config.Port = port;
            
            var validationResults = ValidateConfiguration(config);
            
            validationResults.Should().BeEmpty($"Port {port} should be valid");
        }
    }

    #endregion

    #region UnitId Validation Tests (2 tests)

    [Fact]
    public void UnitId_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_unit_id");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.UnitId)) &&
            r.ErrorMessage!.Contains("between 1 and 255"));
    }

    [Fact]
    public void UnitId_ValidRange_ShouldPassValidation()
    {
        // Arrange & Act & Assert
        var validUnitIds = new byte[] { 1, 10, 100, 255 };
        
        foreach (var unitId in validUnitIds)
        {
            var config = TestConfigurationBuilder.ValidDeviceConfig();
            config.UnitId = unitId;
            
            var validationResults = ValidateConfiguration(config);
            
            validationResults.Should().BeEmpty($"UnitId {unitId} should be valid");
        }
    }

    #endregion

    #region Timeout and Retry Validation Tests (3 tests)

    [Fact]
    public void TimeoutMs_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_timeout");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.TimeoutMs)) &&
            r.ErrorMessage!.Contains("between 500ms and 30 seconds"));
    }

    [Fact]
    public void MaxRetries_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_retries");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.MaxRetries)) &&
            r.ErrorMessage!.Contains("between 0 and 10"));
    }

    [Fact]
    public void RetryDelayMs_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidDeviceConfig();
        config.RetryDelayMs = 50; // Below minimum

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.RetryDelayMs)) &&
            r.ErrorMessage!.Contains("between 100ms and 10 seconds"));
    }

    #endregion

    #region Buffer Size Validation Tests (2 tests)

    [Fact]
    public void ReceiveBufferSize_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_receive_buffer");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.ReceiveBufferSize)) &&
            r.ErrorMessage!.Contains("between 1KB and 64KB"));
    }

    [Fact]
    public void SendBufferSize_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_send_buffer");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.SendBufferSize)) &&
            r.ErrorMessage!.Contains("between 1KB and 64KB"));
    }

    #endregion

    #region Rate and Overflow Validation Tests (2 tests)

    [Fact]
    public void RateWindowSeconds_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_rate_window");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.RateWindowSeconds)) &&
            r.ErrorMessage!.Contains("between 10 seconds and 1 hour"));
    }

    [Fact]
    public void OverflowThreshold_InvalidRange_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("invalid_overflow_threshold");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.OverflowThreshold)) &&
            r.ErrorMessage!.Contains("reasonable for 32-bit counters"));
    }

    #endregion

    #region Channel Validation Tests (2 tests)

    [Fact]
    public void Channels_Empty_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("no_channels");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.Channels)) &&
            r.ErrorMessage!.Contains("At least one channel"));
    }

    [Fact]
    public void Channels_DuplicateNumbers_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidDeviceConfig("duplicate_channels");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains(nameof(AdamDeviceConfig.Channels)) &&
            r.ErrorMessage!.Contains("Duplicate channel numbers"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validate a device configuration and return validation results
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation results</returns>
    private static List<ValidationResult> ValidateConfiguration(AdamDeviceConfig config)
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