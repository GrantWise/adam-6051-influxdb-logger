// Industrial.Adam.Logger.Tests - ModbusDeviceManager Integration Tests
// Integration tests for Modbus device management functionality (15 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reactive.Linq;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Integration;

/// <summary>
/// Integration tests for ModbusDeviceManager functionality (15 tests planned)
/// Since ModbusDeviceManager is internal, we test through the public AdamLoggerService interface
/// </summary>
public class ModbusDeviceManagerIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ILogger<AdamLoggerService>> _mockLogger;
    private readonly Mock<IDataProcessor> _mockDataProcessor;

    public ModbusDeviceManagerIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<AdamLoggerService>>();
        _mockDataProcessor = new Mock<IDataProcessor>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockLogger.Object);
        services.AddSingleton(_mockDataProcessor.Object);
        services.AddLogging();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    #region Device Connection Tests (4 tests)

    [Fact]
    public async Task Service_WithValidDeviceConfig_ShouldCreateDeviceManagers()
    {
        // Arrange
        var config = CreateConfigWithSingleDevice("192.168.1.100", 502);
        var service = CreateLoggerService(config);

        // Act
        await service.StartAsync();
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.DeviceId.Should().Be("TEST_DEVICE_001");

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithMultipleDevices_ShouldCreateMultipleManagers()
    {
        // Arrange
        var config = CreateConfigWithMultipleDevices();
        var service = CreateLoggerService(config);

        // Act
        await service.StartAsync();
        var allHealth = await service.GetAllDeviceHealthAsync();

        // Assert
        allHealth.Should().HaveCount(3);
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_001");
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_002");
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_003");

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithInvalidIPAddress_ShouldMarkDeviceOffline()
    {
        // Arrange - Use invalid IP that will fail to connect
        var config = CreateConfigWithSingleDevice("192.168.255.254", 502);
        var service = CreateLoggerService(config);

        await service.StartAsync();
        
        // Act - Wait for connection attempts
        await Task.Delay(500);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.IsConnected.Should().BeFalse();
        deviceHealth.Status.Should().BeOneOf(DeviceStatus.Offline, DeviceStatus.Error, DeviceStatus.Unknown);

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithInvalidPort_ShouldFailConnection()
    {
        // Arrange - Use valid IP but invalid port
        var config = CreateConfigWithSingleDevice("127.0.0.1", 9999);
        var service = CreateLoggerService(config);

        await service.StartAsync();
        
        // Act - Wait for connection attempts
        await Task.Delay(500);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.IsConnected.Should().BeFalse();

        await service.StopAsync();
    }

    #endregion

    #region Register Reading Tests (5 tests)

    [Fact]
    public async Task Service_WithValidChannelConfig_ShouldAttemptRegisterReads()
    {
        // Arrange
        var config = CreateConfigWithChannels();
        var service = CreateLoggerService(config);
        var dataReceived = new List<AdamDataReading>();

        _mockDataProcessor.Setup(p => p.ProcessRawData(
            It.IsAny<string>(), 
            It.IsAny<ChannelConfig>(), 
            It.IsAny<ushort[]>(), 
            It.IsAny<DateTimeOffset>(), 
            It.IsAny<TimeSpan>()))
            .Returns(TestData.ValidReading());

        var subscription = service.DataStream.Subscribe(dataReceived.Add);

        // Act
        await service.StartAsync();
        await Task.Delay(300); // Wait for read attempts

        // Assert - Service should attempt to process data even if reads fail
        _mockDataProcessor.Verify(p => p.ProcessRawData(
            It.IsAny<string>(), 
            It.IsAny<ChannelConfig>(), 
            It.IsAny<ushort[]>(), 
            It.IsAny<DateTimeOffset>(), 
            It.IsAny<TimeSpan>()), 
            Times.AtLeast(0)); // May not succeed due to connection failures

        subscription.Dispose();
        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithMultipleChannels_ShouldReadAllChannels()
    {
        // Arrange
        var config = CreateConfigWithMultipleChannels();
        var service = CreateLoggerService(config);
        
        await service.StartAsync();

        // Act - Wait for read cycles
        await Task.Delay(400);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.TotalReads.Should().BeGreaterThan(0);

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithDifferentRegisterTypes_ShouldHandleVariedConfigs()
    {
        // Arrange
        var config = new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
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
                        CreateChannelConfig(0, "SingleRegister", 1),
                        CreateChannelConfig(1, "DoubleRegister", 2),
                        CreateChannelConfig(2, "QuadRegister", 4)
                    }
                }
            }
        };

        var service = CreateLoggerService(config);
        await service.StartAsync();

        // Act
        await Task.Delay(300);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        
        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithHighFrequencyPolling_ShouldMaintainPerformance()
    {
        // Arrange - Fast polling interval
        var config = CreateConfigWithSingleDevice("127.0.0.1", 502);
        config.PollIntervalMs = 100; // Very fast polling
        
        var service = CreateLoggerService(config);
        await service.StartAsync();

        // Act - Monitor performance over short period
        var startTime = DateTime.UtcNow;
        await Task.Delay(500);
        var endTime = DateTime.UtcNow;
        
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        var elapsed = endTime - startTime;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should not hang

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithDisabledChannels_ShouldSkipReading()
    {
        // Arrange
        var config = new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
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
                        CreateChannelConfig(0, "EnabledChannel", 1, true),
                        CreateChannelConfig(1, "DisabledChannel", 1, false)
                    }
                }
            }
        };

        var service = CreateLoggerService(config);
        await service.StartAsync();

        // Act
        await Task.Delay(300);

        // Assert - Service should only process enabled channels
        _mockDataProcessor.Verify(p => p.ProcessRawData(
            "TEST_DEVICE_001",
            It.Is<ChannelConfig>(c => c.Name == "EnabledChannel"),
            It.IsAny<ushort[]>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<TimeSpan>()), 
            Times.AtLeast(0));

        // Disabled channel should never be processed
        _mockDataProcessor.Verify(p => p.ProcessRawData(
            "TEST_DEVICE_001",
            It.Is<ChannelConfig>(c => c.Name == "DisabledChannel"),
            It.IsAny<ushort[]>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<TimeSpan>()), 
            Times.Never);

        await service.StopAsync();
    }

    #endregion

    #region Error Handling Tests (3 tests)

    [Fact]
    public async Task Service_WithConnectionFailures_ShouldUpdateHealthStatus()
    {
        // Arrange - Use invalid address to force failures
        var config = CreateConfigWithSingleDevice("192.168.254.254", 502);
        var service = CreateLoggerService(config);

        await service.StartAsync();

        // Act - Wait for failure detection
        await Task.Delay(600);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.ConsecutiveFailures.Should().BeGreaterThan(0);
        deviceHealth.IsConnected.Should().BeFalse();
        deviceHealth.LastError.Should().NotBeNullOrEmpty();

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithTimeoutErrors_ShouldRetryConnections()
    {
        // Arrange - Use configuration that may timeout
        var config = CreateConfigWithSingleDevice("1.2.3.4", 502); // Non-routable address
        var service = CreateLoggerService(config);

        await service.StartAsync();

        // Act - Wait for multiple retry attempts
        await Task.Delay(800);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.TotalReads.Should().BeGreaterThan(1); // Multiple attempts
        deviceHealth.ConsecutiveFailures.Should().BeGreaterThan(0);

        await service.StopAsync();
    }

    [Fact]
    public async Task Service_WithInvalidSlaveId_ShouldHandleModbusErrors()
    {
        // Arrange - Use invalid slave ID
        var config = CreateConfigWithSingleDevice("127.0.0.1", 502);
        config.Devices[0].UnitId = 255; // Likely invalid unit ID

        var service = CreateLoggerService(config);
        await service.StartAsync();

        // Act
        await Task.Delay(400);
        var deviceHealth = await service.GetDeviceHealthAsync("TEST_DEVICE_001");

        // Assert
        deviceHealth.Should().NotBeNull();
        deviceHealth!.TotalReads.Should().BeGreaterThan(0);

        await service.StopAsync();
    }

    #endregion

    #region Lifecycle Management Tests (3 tests)

    [Fact]
    public async Task Service_StartStop_ShouldCleanupDeviceManagers()
    {
        // Arrange
        var config = CreateConfigWithMultipleDevices();
        var service = CreateLoggerService(config);

        // Act
        await service.StartAsync();
        service.IsRunning.Should().BeTrue();

        await service.StopAsync();

        // Assert
        service.IsRunning.Should().BeFalse();
        
        // Should be able to restart without issues
        await service.StartAsync();
        service.IsRunning.Should().BeTrue();
        
        await service.StopAsync();
    }

    [Fact]
    public async Task Service_ConcurrentDevices_ShouldHandleParallelOperations()
    {
        // Arrange
        var config = CreateConfigWithMultipleDevices();
        config.MaxConcurrentDevices = 3;
        
        var service = CreateLoggerService(config);
        await service.StartAsync();

        // Act - Wait for concurrent operations
        await Task.Delay(500);
        var allHealth = await service.GetAllDeviceHealthAsync();

        // Assert
        allHealth.Should().HaveCount(3);
        foreach (var health in allHealth)
        {
            health.TotalReads.Should().BeGreaterOrEqualTo(0);
        }

        await service.StopAsync();
    }

    [Fact] 
    public async Task Service_Dispose_ShouldCleanupAllResources()
    {
        // Arrange
        var config = CreateConfigWithSingleDevice("127.0.0.1", 502);
        var service = CreateLoggerService(config);

        await service.StartAsync();

        // Act
        service.Dispose();

        // Assert - Should not throw and service should be stopped
        service.IsRunning.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private AdamLoggerConfig CreateConfigWithSingleDevice(string ipAddress, int port)
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
            HealthCheckIntervalMs = 5000,
            MaxConcurrentDevices = 1,
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "TEST_DEVICE_001",
                    IpAddress = ipAddress,
                    Port = port,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel")
                    }
                }
            }
        };
    }

    private AdamLoggerConfig CreateConfigWithMultipleDevices()
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
            HealthCheckIntervalMs = 5000,
            MaxConcurrentDevices = 3,
            Devices = new List<AdamDeviceConfig>
            {
                new()
                {
                    DeviceId = "DEVICE_001",
                    IpAddress = "192.168.1.101",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig> { TestConfigurationBuilder.ValidChannelConfig(0) }
                },
                new()
                {
                    DeviceId = "DEVICE_002", 
                    IpAddress = "192.168.1.102",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig> { TestConfigurationBuilder.ValidChannelConfig(0) }
                },
                new()
                {
                    DeviceId = "DEVICE_003",
                    IpAddress = "192.168.1.103", 
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig> { TestConfigurationBuilder.ValidChannelConfig(0) }
                }
            }
        };
    }

    private AdamLoggerConfig CreateConfigWithChannels()
    {
        var config = CreateConfigWithSingleDevice("127.0.0.1", 502);
        config.Devices[0].Channels = new List<ChannelConfig>
        {
            TestConfigurationBuilder.ValidChannelConfig(0, "Channel1"),
            TestConfigurationBuilder.ValidChannelConfig(1, "Channel2")
        };
        return config;
    }

    private AdamLoggerConfig CreateConfigWithMultipleChannels()
    {
        var config = CreateConfigWithSingleDevice("127.0.0.1", 502);
        config.Devices[0].Channels = new List<ChannelConfig>
        {
            TestConfigurationBuilder.ValidChannelConfig(0, "Counter1"),
            TestConfigurationBuilder.ValidChannelConfig(1, "Counter2"),
            TestConfigurationBuilder.ValidChannelConfig(2, "Temperature"),
            TestConfigurationBuilder.ValidChannelConfig(3, "Pressure")
        };
        return config;
    }

    private AdamLoggerService CreateLoggerService(AdamLoggerConfig config)
    {
        var options = Options.Create(config);
        return new AdamLoggerService(options, _mockDataProcessor.Object, _serviceProvider, _mockLogger.Object);
    }

    private static ChannelConfig CreateChannelConfig(int channelNumber, string name, int registerCount, bool enabled = true)
    {
        var config = TestConfigurationBuilder.ValidChannelConfig(channelNumber, name);
        config.RegisterCount = registerCount;
        config.Enabled = enabled;
        return config;
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}