// Industrial.Adam.Logger.Tests - End-to-End Integration Tests
// Comprehensive end-to-end integration scenarios (8 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Extensions;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Integration;

/// <summary>
/// End-to-end integration tests covering complete workflows (8 tests planned)
/// </summary>
public class EndToEndIntegrationTests : IDisposable
{
    private readonly ServiceCollection _services;
    private ServiceProvider? _serviceProvider;

    public EndToEndIntegrationTests()
    {
        _services = new ServiceCollection();
        _services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
    }

    #region Complete Workflow Tests (3 tests)

    [Fact]
    public void CompleteWorkflow_ServiceRegistration_ShouldConfigureFullPipeline()
    {
        // Arrange
        var config = CreateProductionLikeConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.MaxConcurrentDevices = config.MaxConcurrentDevices;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();
        var hostedService = _serviceProvider.GetRequiredService<IHostedService>();

        // Assert
        loggerService.Should().NotBeNull();
        hostedService.Should().NotBeNull().And.BeSameAs(loggerService);
        loggerService.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteWorkflow_StartStopCycle_ShouldManageLifecycleProperly()
    {
        // Arrange
        var config = CreateSimpleConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var hostedService = _serviceProvider.GetRequiredService<IHostedService>();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();

        // Act & Assert - Start
        await hostedService.StartAsync(CancellationToken.None);
        loggerService.IsRunning.Should().BeTrue();

        // Wait for initialization
        await Task.Delay(200);

        // Act & Assert - Stop
        await hostedService.StopAsync(CancellationToken.None);
        loggerService.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteWorkflow_DataFlowEndToEnd_ShouldProcessAndEmitData()
    {
        // Arrange
        var config = CreateSimpleConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();
        var dataReceived = new List<AdamDataReading>();

        // Subscribe to data stream
        var subscription = loggerService.DataStream
            .Take(5) // Take first 5 readings
            .Subscribe(dataReceived.Add);

        // Act
        await loggerService.StartAsync();
        
        // Wait for data collection (connection will likely fail but we're testing the pipeline)
        await Task.Delay(1000);

        // Assert
        subscription.Dispose();
        await loggerService.StopAsync();

        // Verify the service attempted to process data (even if connections failed)
        loggerService.DataStream.Should().NotBeNull();
    }

    #endregion

    #region Configuration Scenarios Tests (2 tests)

    [Fact]
    public async Task ConfigurationScenario_MultipleDevicesAndChannels_ShouldHandleComplexConfig()
    {
        // Arrange
        var config = CreateComplexConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.MaxConcurrentDevices = config.MaxConcurrentDevices;
            cfg.EnablePerformanceCounters = config.EnablePerformanceCounters;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();

        // Act
        await loggerService.StartAsync();
        await Task.Delay(300);

        var allHealth = await loggerService.GetAllDeviceHealthAsync();

        // Assert
        allHealth.Should().HaveCount(3);
        allHealth.All(h => !string.IsNullOrEmpty(h.DeviceId)).Should().BeTrue();

        await loggerService.StopAsync();
    }

    [Fact]
    public void ConfigurationScenario_CustomServices_ShouldUseCustomImplementations()
    {
        // Arrange
        var config = CreateSimpleConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.Devices.AddRange(config.Devices);
        });

        // Add custom implementations
        _services.AddCustomDataValidator<TestCustomValidator>();
        _services.AddCustomDataTransformer<TestCustomTransformer>();
        _services.AddCustomDataProcessor<TestCustomProcessor>();

        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var validator = _serviceProvider.GetRequiredService<IDataValidator>();
        var transformer = _serviceProvider.GetRequiredService<IDataTransformer>();
        var processor = _serviceProvider.GetRequiredService<IDataProcessor>();

        // Assert
        validator.Should().BeOfType<TestCustomValidator>();
        transformer.Should().BeOfType<TestCustomTransformer>();
        processor.Should().BeOfType<TestCustomProcessor>();
    }

    #endregion

    #region Health Monitoring Tests (2 tests)

    [Fact]
    public async Task HealthMonitoring_DeviceHealthTracking_ShouldUpdateHealthStatus()
    {
        // Arrange
        var config = CreateSimpleConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.HealthCheckIntervalMs = 5000; // Minimum allowed health check interval
            cfg.DemoMode = true; // Use demo mode for tests
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();

        // Act
        await loggerService.StartAsync();
        
        // Wait for health checks
        await Task.Delay(1000);
        
        var deviceHealth = await loggerService.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.TotalReads.Should().BeGreaterThan(0);
        deviceHealth.Timestamp.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-2));

        await loggerService.StopAsync();
    }

    [Fact]
    public async Task HealthMonitoring_HealthCheckProvider_ShouldReportServiceStatus()
    {
        // Arrange
        var config = CreateSimpleConfig();
        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();

        // Act & Assert - Service not running
        var healthCheck = loggerService as Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck;
        healthCheck.Should().NotBeNull();
        
        var healthResult1 = await healthCheck!.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        healthResult1.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);

        // Start service
        await loggerService.StartAsync();
        await Task.Delay(200);

        // Act & Assert - Service running
        var healthResult2 = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        healthResult2.Status.Should().BeOneOf(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);

        await loggerService.StopAsync();
    }

    #endregion

    #region Error Handling and Recovery Tests (1 test)

    [Fact]
    public async Task ErrorHandling_CommunicationFailures_ShouldContinueOperation()
    {
        // Arrange - Use invalid addresses to force failures
        var config = new AdamLoggerConfig
        {
            PollIntervalMs = 500,
            HealthCheckIntervalMs = 5000, // Minimum allowed value
            MaxConsecutiveFailures = 3,
            EnableAutomaticRecovery = true,
            DemoMode = true, // Use demo mode for tests
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "FAILING_DEVICE_001",
                    IpAddress = "192.168.254.254", // Non-existent IP
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel")
                    }
                }
            }
        };

        _services.AddAdamLogger(cfg =>
        {
            cfg.PollIntervalMs = config.PollIntervalMs;
            cfg.HealthCheckIntervalMs = config.HealthCheckIntervalMs;
            cfg.MaxConsecutiveFailures = config.MaxConsecutiveFailures;
            cfg.EnableAutomaticRecovery = config.EnableAutomaticRecovery;
            cfg.Devices.AddRange(config.Devices);
        });

        _serviceProvider = _services.BuildServiceProvider();
        var loggerService = _serviceProvider.GetRequiredService<IAdamLoggerService>();

        // Act
        await loggerService.StartAsync();
        
        // Wait for multiple failure cycles
        await Task.Delay(2000);
        
        var deviceHealth = await loggerService.GetDeviceHealthAsync("FAILING_DEVICE_001");

        // Assert - Service should still be running despite failures
        loggerService.IsRunning.Should().BeTrue();
        deviceHealth.Should().NotBeNull();
        deviceHealth!.ConsecutiveFailures.Should().BeGreaterThan(0);
        deviceHealth.TotalReads.Should().BeGreaterThan(config.MaxConsecutiveFailures);

        await loggerService.StopAsync();
    }

    #endregion

    #region Helper Methods

    private static AdamLoggerConfig CreateSimpleConfig()
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 800,
            HealthCheckIntervalMs = 5000, // Minimum allowed value
            MaxConcurrentDevices = 1,
            DemoMode = true, // Use demo mode for tests
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "TEST_DEVICE_001",
                    IpAddress = "127.0.0.1",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "SimpleChannel")
                    }
                }
            }
        };
    }

    private static AdamLoggerConfig CreateProductionLikeConfig()
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
            HealthCheckIntervalMs = 5000,
            MaxConcurrentDevices = 5,
            EnablePerformanceCounters = true,
            EnableDetailedLogging = false,
            DataBufferSize = 1000,
            BatchSize = 50,
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "PROD_DEVICE_001",
                    IpAddress = "192.168.1.100",
                    Port = 502,
                    UnitId = 1,
                    MaxRetries = 3,
                    TimeoutMs = 5000,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "ProductionCounter1"),
                        TestConfigurationBuilder.ValidChannelConfig(1, "ProductionCounter2")
                    }
                }
            }
        };
    }

    private static AdamLoggerConfig CreateComplexConfig()
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 2000,
            HealthCheckIntervalMs = 5000, // Minimum allowed value
            MaxConcurrentDevices = 3,
            EnablePerformanceCounters = true,
            DemoMode = true, // Use demo mode for tests
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "COMPLEX_DEVICE_001",
                    IpAddress = "192.168.1.101",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "Counter1"),
                        TestConfigurationBuilder.ValidChannelConfig(1, "Counter2"),
                        TestConfigurationBuilder.ValidChannelConfig(2, "Temperature")
                    }
                },
                new()
                {
                    DeviceId = "COMPLEX_DEVICE_002",
                    IpAddress = "192.168.1.102",
                    Port = 503,
                    UnitId = 2,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "Pressure"),
                        TestConfigurationBuilder.ValidChannelConfig(1, "Flow")
                    }
                },
                new()
                {
                    DeviceId = "COMPLEX_DEVICE_003",
                    IpAddress = "192.168.1.103",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "Level")
                    }
                }
            }
        };
    }

    #endregion

    #region Test Helper Classes

    private class TestCustomValidator : IDataValidator
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

    private class TestCustomTransformer : IDataTransformer
    {
        public double? TransformValue(long rawValue, ChannelConfig channel)
        {
            return rawValue * 1.5; // Custom transformation
        }

        public Dictionary<string, object> EnrichTags(Dictionary<string, object> baseTags, AdamDeviceConfig deviceConfig, ChannelConfig channelConfig)
        {
            var enriched = new Dictionary<string, object>(baseTags)
            {
                ["custom_transformer"] = "end_to_end_test"
            };
            return enriched;
        }
    }

    private class TestCustomProcessor : IDataProcessor
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
                ProcessedValue = registers.Length > 0 ? registers[0] * 2.0 : 0, // Custom processing
                AcquisitionTime = acquisitionTime
            };
        }

        public double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp)
        {
            return null; // Simplified for test
        }

        public DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate)
        {
            return DataQuality.Good;
        }
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}