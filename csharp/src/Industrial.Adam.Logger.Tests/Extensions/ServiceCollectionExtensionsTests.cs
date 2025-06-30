// Industrial.Adam.Logger.Tests - ServiceCollectionExtensions Unit Tests
// Comprehensive tests for dependency injection configuration (10 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Extensions;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Extensions;

/// <summary>
/// Unit tests for ServiceCollectionExtensions (10 tests planned)
/// </summary>
public class ServiceCollectionExtensionsTests
{
    #region AddAdamLogger Tests (3 tests)

    [Fact]
    public void AddAdamLogger_ValidConfiguration_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAdamLogger(config =>
        {
            config.PollIntervalMs = 1000;
            config.Devices.Add(TestConfigurationBuilder.ValidDeviceConfig());
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IDataValidator>().Should().NotBeNull().And.BeOfType<DefaultDataValidator>();
        serviceProvider.GetService<IDataTransformer>().Should().NotBeNull().And.BeOfType<DefaultDataTransformer>();
        serviceProvider.GetService<IDataProcessor>().Should().NotBeNull().And.BeOfType<DefaultDataProcessor>();
        serviceProvider.GetService<IAdamLoggerService>().Should().NotBeNull().And.BeOfType<AdamLoggerService>();
        
        // Verify hosted service registration
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s is AdamLoggerService);
    }

    [Fact]
    public void AddAdamLogger_ConfigurationAction_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var testDevice = TestConfigurationBuilder.ValidDeviceConfig();
        testDevice.DeviceId = "TEST_DEVICE_123";

        // Act
        services.AddAdamLogger(config =>
        {
            config.PollIntervalMs = 2500;
            config.MaxConcurrentDevices = 5;
            config.Devices.Add(testDevice);
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AdamLoggerConfig>>().Value;

        // Assert
        options.PollIntervalMs.Should().Be(2500);
        options.MaxConcurrentDevices.Should().Be(5);
        options.Devices.Should().HaveCount(1);
        options.Devices.First().DeviceId.Should().Be("TEST_DEVICE_123");
    }

    [Fact]
    public void AddAdamLogger_FluentInterface_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.AddAdamLogger(config =>
        {
            config.PollIntervalMs = 1000;
        });

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region Custom Service Registration Tests (3 tests)

    [Fact]
    public void AddCustomDataProcessor_ValidType_ShouldReplaceDefaultProcessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdamLogger(config => { });

        // Act
        services.AddCustomDataProcessor<TestCustomDataProcessor>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var processor = serviceProvider.GetRequiredService<IDataProcessor>();
        processor.Should().BeOfType<TestCustomDataProcessor>();
    }

    [Fact]
    public void AddCustomDataValidator_ValidType_ShouldReplaceDefaultValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdamLogger(config => { });

        // Act
        services.AddCustomDataValidator<TestCustomDataValidator>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var validator = serviceProvider.GetRequiredService<IDataValidator>();
        validator.Should().BeOfType<TestCustomDataValidator>();
    }

    [Fact]
    public void AddCustomDataTransformer_ValidType_ShouldReplaceDefaultTransformer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdamLogger(config => { });

        // Act
        services.AddCustomDataTransformer<TestCustomDataTransformer>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var transformer = serviceProvider.GetRequiredService<IDataTransformer>();
        transformer.Should().BeOfType<TestCustomDataTransformer>();
    }

    #endregion

    #region AddAdamLoggerFromConfiguration Tests (2 tests)

    [Fact]
    public void AddAdamLoggerFromConfiguration_DefaultSectionName_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAdamLoggerFromConfiguration();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IDataValidator>().Should().NotBeNull().And.BeOfType<DefaultDataValidator>();
        serviceProvider.GetService<IDataTransformer>().Should().NotBeNull().And.BeOfType<DefaultDataTransformer>();
        serviceProvider.GetService<IDataProcessor>().Should().NotBeNull().And.BeOfType<DefaultDataProcessor>();
        serviceProvider.GetService<IAdamLoggerService>().Should().NotBeNull().And.BeOfType<AdamLoggerService>();
        
        // Verify options configuration is registered
        var optionsServices = services.Where(s => s.ServiceType.IsGenericType && 
                                                  s.ServiceType.GetGenericTypeDefinition() == typeof(IOptions<>));
        optionsServices.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAdamLoggerFromConfiguration_CustomSectionName_ShouldUseCorrectSection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        const string customSectionName = "MyCustomAdamConfig";

        // Act
        var result = services.AddAdamLoggerFromConfiguration(customSectionName);

        // Assert
        result.Should().BeSameAs(services);
        // Verify that options were configured (the section name is used internally)
        var optionsServices = services.Where(s => s.ServiceType.IsGenericType && 
                                                  s.ServiceType.GetGenericTypeDefinition() == typeof(IOptions<>));
        optionsServices.Should().NotBeEmpty();
    }

    #endregion

    #region Service Lifecycle and Dependencies Tests (2 tests)

    [Fact]
    public void AddAdamLogger_ServiceLifetimes_ShouldUseSingletonPattern()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAdamLogger(config => { });
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All services should be singletons
        var validator1 = serviceProvider.GetRequiredService<IDataValidator>();
        var validator2 = serviceProvider.GetRequiredService<IDataValidator>();
        validator1.Should().BeSameAs(validator2);

        var transformer1 = serviceProvider.GetRequiredService<IDataTransformer>();
        var transformer2 = serviceProvider.GetRequiredService<IDataTransformer>();
        transformer1.Should().BeSameAs(transformer2);

        var processor1 = serviceProvider.GetRequiredService<IDataProcessor>();
        var processor2 = serviceProvider.GetRequiredService<IDataProcessor>();
        processor1.Should().BeSameAs(processor2);

        var logger1 = serviceProvider.GetRequiredService<IAdamLoggerService>();
        var logger2 = serviceProvider.GetRequiredService<IAdamLoggerService>();
        logger1.Should().BeSameAs(logger2);
    }

    [Fact]
    public void AddAdamLogger_HealthCheckRegistration_ShouldIncludeAdamLoggerCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAdamLogger(config => { });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Verify that health check services are registered
        var healthCheckServices = services.Where(s => 
            s.ServiceType.Name.Contains("HealthCheck") || 
            s.ServiceType.Name.Contains("IHealthCheckService"));
        healthCheckServices.Should().NotBeEmpty();
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test implementation of IDataProcessor for custom registration testing
    /// </summary>
    private class TestCustomDataProcessor : IDataProcessor
    {
        public AdamDataReading ProcessRawData(string deviceId, ChannelConfig channel, ushort[] registers, DateTimeOffset timestamp, TimeSpan acquisitionTime)
        {
            return new AdamDataReading
            {
                DeviceId = deviceId,
                Channel = channel.ChannelNumber,
                RawValue = registers.Length > 0 ? registers[0] : 0,
                Timestamp = timestamp,
                Quality = DataQuality.Good,
                Rate = null
            };
        }

        public double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp)
        {
            return null; // Test implementation returns null
        }

        public DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate)
        {
            return DataQuality.Good; // Test implementation always returns good
        }
    }

    /// <summary>
    /// Test implementation of IDataValidator for custom registration testing
    /// </summary>
    private class TestCustomDataValidator : IDataValidator
    {
        public DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channel)
        {
            return DataQuality.Good;
        }

        public bool IsValidRange(long value, ChannelConfig channel)
        {
            return true;
        }

        public bool IsValidRateOfChange(double? rate, ChannelConfig channel)
        {
            return true;
        }
    }

    /// <summary>
    /// Test implementation of IDataTransformer for custom registration testing
    /// </summary>
    private class TestCustomDataTransformer : IDataTransformer
    {
        public double? TransformValue(long rawValue, ChannelConfig channel)
        {
            return rawValue * 2.0; // Simple test transformation
        }

        public Dictionary<string, object> EnrichTags(Dictionary<string, object> baseTags, AdamDeviceConfig deviceConfig, ChannelConfig channelConfig)
        {
            var enriched = new Dictionary<string, object>(baseTags)
            {
                ["test_transformer"] = true
            };
            return enriched;
        }
    }

    #endregion
}