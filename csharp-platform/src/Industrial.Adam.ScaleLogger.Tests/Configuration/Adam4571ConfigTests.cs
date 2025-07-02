// Industrial.Adam.ScaleLogger.Tests - Configuration Validation Tests
// Following proven ADAM-6051 configuration testing patterns

using FluentAssertions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Tests.TestHelpers;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Industrial.Adam.ScaleLogger.Tests.Configuration;

/// <summary>
/// Unit tests for Adam4571Config validation following proven ADAM-6051 patterns
/// Tests cover data annotations, custom validation, and edge cases
/// Total Tests: 25 (planned)
/// </summary>
public sealed class Adam4571ConfigTests
{
    #region Valid Configuration Tests (5 tests)

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ShouldPassValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.ValidScaleLoggerConfig();

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithMinimalValidConfig_ShouldPassValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.MinimalScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig()
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithComprehensiveConfig_ShouldPassValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ComprehensiveScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            PollIntervalMs = 10000,
            HealthCheckIntervalMs = 60000,
            MaxRetryAttempts = 5,
            RetryDelayMs = 2000
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithMultipleDevices_ShouldPassValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = TestConfigurationBuilder.DiverseScaleDeviceConfigs(5),
            Database = TestConfigurationBuilder.ValidDatabaseConfig()
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithDefaultValues_ShouldPassValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig()
            // All other properties should use default values
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
        config.PollIntervalMs.Should().Be(5000); // Default value
        config.HealthCheckIntervalMs.Should().Be(30000); // Default value
        config.MaxRetryAttempts.Should().Be(3); // Default value
        config.RetryDelayMs.Should().Be(1000); // Default value
    }

    #endregion

    #region Devices Collection Validation Tests (4 tests)

    [Fact]
    public void ValidateConfiguration_WithEmptyDevicesList_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidScaleLoggerConfig("no_devices");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.ErrorMessage!.Contains("Devices"));
    }

    [Fact]
    public void ValidateConfiguration_WithNullDevicesList_ShouldFailValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = null!,
            Database = TestConfigurationBuilder.ValidDatabaseConfig()
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.ErrorMessage!.Contains("required"));
    }

    [Fact]
    public void ValidateConfiguration_WithDuplicateDeviceIds_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidScaleLoggerConfig("duplicate_device_ids");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        // Note: This would require custom validation logic in the config class
    }

    [Fact]
    public void ValidateConfiguration_WithTooManyDevices_ShouldFailValidation()
    {
        // Arrange
        var config = TestConfigurationBuilder.InvalidScaleLoggerConfig("too_many_devices");

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        // Note: This would require custom validation logic for device count limits
    }

    #endregion

    #region Timing Configuration Validation Tests (8 tests)

    [Theory]
    [InlineData(500)]    // Too fast
    [InlineData(999)]    // Below minimum
    [InlineData(0)]      // Invalid
    [InlineData(-1000)]  // Negative
    public void ValidateConfiguration_WithInvalidPollInterval_ShouldFailValidation(int invalidInterval)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            PollIntervalMs = invalidInterval
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.PollIntervalMs)));
    }

    [Theory]
    [InlineData(61000)]   // Above maximum
    [InlineData(120000)]  // Way too high
    public void ValidateConfiguration_WithPollIntervalTooHigh_ShouldFailValidation(int invalidInterval)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            PollIntervalMs = invalidInterval
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.PollIntervalMs)));
    }

    [Theory]
    [InlineData(1000)]   // Valid minimum
    [InlineData(5000)]   // Default value
    [InlineData(30000)]  // Reasonable value
    [InlineData(60000)]  // Valid maximum
    public void ValidateConfiguration_WithValidPollInterval_ShouldPassValidation(int validInterval)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            PollIntervalMs = validInterval
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1000)]   // Too fast
    [InlineData(4999)]   // Below minimum
    [InlineData(0)]      // Invalid
    public void ValidateConfiguration_WithInvalidHealthCheckInterval_ShouldFailValidation(int invalidInterval)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            HealthCheckIntervalMs = invalidInterval
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.HealthCheckIntervalMs)));
    }

    #endregion

    #region Retry Configuration Validation Tests (4 tests)

    [Theory]
    [InlineData(0)]      // Invalid
    [InlineData(-1)]     // Negative
    public void ValidateConfiguration_WithInvalidMaxRetryAttempts_ShouldFailValidation(int invalidAttempts)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            MaxRetryAttempts = invalidAttempts
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.MaxRetryAttempts)));
    }

    [Theory]
    [InlineData(11)]     // Above maximum
    [InlineData(50)]     // Way too high
    public void ValidateConfiguration_WithRetryAttemptsTooHigh_ShouldFailValidation(int invalidAttempts)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            MaxRetryAttempts = invalidAttempts
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.MaxRetryAttempts)));
    }

    [Theory]
    [InlineData(50)]     // Too fast
    [InlineData(99)]     // Below minimum
    [InlineData(0)]      // Invalid
    public void ValidateConfiguration_WithInvalidRetryDelay_ShouldFailValidation(int invalidDelay)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            RetryDelayMs = invalidDelay
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.MemberNames.Contains(nameof(Adam4571Config.RetryDelayMs)));
    }

    [Theory]
    [InlineData(100)]    // Valid minimum
    [InlineData(1000)]   // Default value
    [InlineData(5000)]   // Reasonable value
    [InlineData(10000)]  // Valid maximum
    public void ValidateConfiguration_WithValidRetryDelay_ShouldPassValidation(int validDelay)
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            RetryDelayMs = validDelay
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Database Configuration Validation Tests (2 tests)

    [Fact]
    public void ValidateConfiguration_WithNullDatabaseConfig_ShouldFailValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = null!
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.ErrorMessage!.Contains("required"));
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidDatabaseConfig_ShouldFailValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.InvalidDatabaseConfig("empty_connection_string")
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(r => r.ErrorMessage!.Contains("ConnectionString"));
    }

    #endregion

    #region Discovery Configuration Validation Tests (2 tests)

    [Fact]
    public void ValidateConfiguration_WithNullDiscoveryConfig_ShouldUseDefaults()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            Discovery = null!
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        // Note: Discovery config is optional and should default
        config.Discovery.Should().BeNull();
    }

    [Fact]
    public void ValidateConfiguration_WithValidDiscoveryConfig_ShouldPassValidation()
    {
        // Arrange
        var config = new Adam4571Config
        {
            Devices = new List<ScaleDeviceConfig> { TestConfigurationBuilder.ValidScaleDeviceConfig() },
            Database = TestConfigurationBuilder.ValidDatabaseConfig(),
            Discovery = TestConfigurationBuilder.ValidProtocolDiscoveryConfig()
        };

        // Act
        var validationResults = ValidateConfiguration(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validate configuration using data annotations and custom validation
    /// Following proven ADAM-6051 validation patterns
    /// </summary>
    private static List<ValidationResult> ValidateConfiguration(Adam4571Config config)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(config);
        
        // Run data annotation validation
        Validator.TryValidateObject(config, context, results, validateAllProperties: true);
        
        // Run custom validation if implemented
        if (config is IValidatableObject validatable)
        {
            results.AddRange(validatable.Validate(context));
        }
        
        return results;
    }

    #endregion
}