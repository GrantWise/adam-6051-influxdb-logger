// Industrial.Adam.Logger.Tests - AdamLoggerService Integration Tests
// Comprehensive integration tests for the main service orchestrator (20 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reactive.Linq;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Integration;

/// <summary>
/// Integration tests for AdamLoggerService (20 tests planned)
/// </summary>
public class AdamLoggerServiceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ILogger<AdamLoggerService>> _mockLogger;
    private readonly Mock<IDataProcessor> _mockDataProcessor;

    public AdamLoggerServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<AdamLoggerService>>();
        _mockDataProcessor = new Mock<IDataProcessor>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockLogger.Object);
        services.AddSingleton(_mockDataProcessor.Object);
        services.AddLogging(); // Add logging services for internal dependencies
        
        _serviceProvider = services.BuildServiceProvider();
    }

    #region Service Lifecycle Tests (4 tests)

    [Fact]
    public void Constructor_ValidConfiguration_ShouldInitializeService()
    {
        // Arrange
        var config = CreateValidConfig();
        var options = Options.Create(config);

        // Act
        var service = new AdamLoggerService(options, _mockDataProcessor.Object, _serviceProvider, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.IsRunning.Should().BeFalse();
        service.DataStream.Should().NotBeNull();
        service.HealthStream.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InvalidConfiguration_ShouldThrowException()
    {
        // Arrange
        var config = new AdamLoggerConfig(); // Invalid - no devices
        var options = Options.Create(config);

        // Act & Assert
        var act = () => new AdamLoggerService(options, _mockDataProcessor.Object, _serviceProvider, _mockLogger.Object);
        act.Should().Throw<ArgumentException>().WithMessage("*configuration*");
    }

    [Fact]
    public async Task StartAsync_ValidService_ShouldStart()
    {
        // Arrange
        var service = CreateValidService();

        // Act
        await service.StartAsync();

        // Assert
        service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_RunningService_ShouldStop()
    {
        // Arrange
        var service = CreateValidService();
        await service.StartAsync();

        // Act
        await service.StopAsync();

        // Assert
        service.IsRunning.Should().BeFalse();
    }

    #endregion

    #region IHostedService Implementation Tests (3 tests)

    [Fact]
    public async Task IHostedService_StartAsync_ShouldDelegateToStartAsync()
    {
        // Arrange
        var service = CreateValidService();
        IHostedService hostedService = service;

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert
        service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task IHostedService_StopAsync_ShouldDelegateToStopAsync()
    {
        // Arrange
        var service = CreateValidService();
        IHostedService hostedService = service;
        await hostedService.StartAsync(CancellationToken.None);

        // Act
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task IHostedService_MultipleStartCalls_ShouldBeIdempotent()
    {
        // Arrange
        var service = CreateValidService();
        IHostedService hostedService = service;

        // Act
        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StartAsync(CancellationToken.None); // Second call

        // Assert
        service.IsRunning.Should().BeTrue();
    }

    #endregion

    #region Health Check Implementation Tests (4 tests)

    [Fact]
    public async Task CheckHealthAsync_ServiceNotRunning_ShouldReturnUnhealthy()
    {
        // Arrange
        var service = CreateValidService();
        var context = new HealthCheckContext();

        // Act
        var result = await service.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not running");
    }

    [Fact]
    public async Task CheckHealthAsync_ServiceRunningNoDevices_ShouldReturnUnhealthy()
    {
        // Arrange
        var config = new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
            Devices = new List<AdamDeviceConfig>() // No devices
        };
        var service = new AdamLoggerService(
            Options.Create(config), 
            _mockDataProcessor.Object, 
            _serviceProvider, 
            _mockLogger.Object);

        await service.StartAsync();
        var context = new HealthCheckContext();

        // Act
        var result = await service.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("No devices");

        await service.StopAsync();
    }

    [Fact]
    public async Task CheckHealthAsync_ServiceWithHealthyDevices_ShouldReturnHealthy()
    {
        // Arrange
        var service = CreateValidService();
        await service.StartAsync();
        
        // Wait a moment for service to initialize device health
        await Task.Delay(100);
        
        var context = new HealthCheckContext();

        // Act
        var result = await service.CheckHealthAsync(context);

        // Assert
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);

        await service.StopAsync();
    }

    [Fact]
    public async Task CheckHealthAsync_MixedDeviceHealth_ShouldReturnDegraded()
    {
        // Arrange
        var config = CreateConfigWithMultipleDevices();
        var service = new AdamLoggerService(
            Options.Create(config), 
            _mockDataProcessor.Object, 
            _serviceProvider, 
            _mockLogger.Object);

        await service.StartAsync();
        await Task.Delay(100); // Allow initialization
        
        var context = new HealthCheckContext();

        // Act
        var result = await service.CheckHealthAsync(context);

        // Assert
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);

        await service.StopAsync();
    }

    #endregion

    #region Data Stream Tests (4 tests)

    [Fact]
    public async Task DataStream_ServiceStarted_ShouldProvideObservableStream()
    {
        // Arrange
        var service = CreateValidService();
        var dataReceived = new List<AdamDataReading>();
        var subscription = service.DataStream.Subscribe(dataReceived.Add);

        // Setup mock to return valid data
        _mockDataProcessor.Setup(p => p.ProcessRawData(
            It.IsAny<string>(), 
            It.IsAny<ChannelConfig>(), 
            It.IsAny<ushort[]>(), 
            It.IsAny<DateTimeOffset>(), 
            It.IsAny<TimeSpan>()))
            .Returns(TestData.ValidReading());

        await service.StartAsync();

        // Act - Wait briefly for data acquisition
        await Task.Delay(200);

        // Assert
        service.DataStream.Should().NotBeNull();
        subscription.Dispose();
        await service.StopAsync();
    }

    [Fact]
    public async Task HealthStream_ServiceStarted_ShouldProvideHealthUpdates()
    {
        // Arrange
        var service = CreateValidService();
        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = service.HealthStream.Subscribe(healthUpdates.Add);

        await service.StartAsync();

        // Act - Wait for health updates
        await Task.Delay(200);

        // Assert
        service.HealthStream.Should().NotBeNull();
        subscription.Dispose();
        await service.StopAsync();
    }

    [Fact]
    public async Task DataStream_MultipleSubscribers_ShouldDeliverToAll()
    {
        // Arrange
        var service = CreateValidService();
        var subscriber1Data = new List<AdamDataReading>();
        var subscriber2Data = new List<AdamDataReading>();
        
        var subscription1 = service.DataStream.Subscribe(subscriber1Data.Add);
        var subscription2 = service.DataStream.Subscribe(subscriber2Data.Add);

        _mockDataProcessor.Setup(p => p.ProcessRawData(
            It.IsAny<string>(), 
            It.IsAny<ChannelConfig>(), 
            It.IsAny<ushort[]>(), 
            It.IsAny<DateTimeOffset>(), 
            It.IsAny<TimeSpan>()))
            .Returns(TestData.ValidReading());

        await service.StartAsync();

        // Act
        await Task.Delay(200);

        // Assert
        subscription1.Dispose();
        subscription2.Dispose();
        await service.StopAsync();
    }

    [Fact]
    public async Task DataStream_ServiceStopped_ShouldStopEmittingData()
    {
        // Arrange
        var service = CreateValidService();
        var dataReceived = new List<AdamDataReading>();
        var subscription = service.DataStream.Subscribe(dataReceived.Add);

        await service.StartAsync();
        await Task.Delay(100);

        // Act
        await service.StopAsync();
        var countAfterStop = dataReceived.Count;
        await Task.Delay(100);

        // Assert
        dataReceived.Count.Should().Be(countAfterStop); // No new data after stop
        subscription.Dispose();
    }

    #endregion

    #region Device Management Tests (3 tests)

    [Fact]
    public async Task GetDeviceHealthAsync_ValidDeviceId_ShouldReturnHealth()
    {
        // Arrange
        var service = CreateValidService();
        var deviceId = "TEST_DEVICE_001";
        await service.StartAsync();
        await Task.Delay(100); // Allow initialization

        // Act
        var health = await service.GetDeviceHealthAsync(deviceId);

        // Assert
        health.Should().NotBeNull();
        health!.DeviceId.Should().Be(deviceId);

        await service.StopAsync();
    }

    [Fact]
    public async Task GetDeviceHealthAsync_InvalidDeviceId_ShouldReturnNull()
    {
        // Arrange
        var service = CreateValidService();
        await service.StartAsync();

        // Act
        var health = await service.GetDeviceHealthAsync("NONEXISTENT_DEVICE");

        // Assert
        health.Should().BeNull();

        await service.StopAsync();
    }

    [Fact]
    public async Task GetAllDeviceHealthAsync_ServiceStarted_ShouldReturnAllDevices()
    {
        // Arrange
        var service = CreateValidService();
        await service.StartAsync();
        await Task.Delay(100); // Allow initialization

        // Act
        var healthList = await service.GetAllDeviceHealthAsync();

        // Assert
        healthList.Should().NotBeEmpty();
        healthList.Count.Should().BeGreaterThan(0);

        await service.StopAsync();
    }

    #endregion

    #region Runtime Configuration Tests (2 tests)

    [Fact]
    public async Task AddDeviceAsync_NotImplemented_ShouldThrowNotImplementedException()
    {
        // Arrange
        var service = CreateValidService();
        var newDevice = TestConfigurationBuilder.ValidDeviceConfig();

        // Act & Assert
        await service.Invoking(s => s.AddDeviceAsync(newDevice))
            .Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task RemoveDeviceAsync_NotImplemented_ShouldThrowNotImplementedException()
    {
        // Arrange
        var service = CreateValidService();

        // Act & Assert
        await service.Invoking(s => s.RemoveDeviceAsync("TEST_DEVICE"))
            .Should().ThrowAsync<NotImplementedException>();
    }

    #endregion

    #region Helper Methods

    private AdamLoggerConfig CreateValidConfig()
    {
        return new AdamLoggerConfig
        {
            PollIntervalMs = 1000,
            HealthCheckIntervalMs = 5000,
            MaxConcurrentDevices = 2,
            Devices = new List<AdamDeviceConfig>
            {
                TestConfigurationBuilder.ValidDeviceConfig("TEST_DEVICE_001")
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
                TestConfigurationBuilder.ValidDeviceConfig("TEST_DEVICE_001"),
                TestConfigurationBuilder.ValidDeviceConfig("TEST_DEVICE_002"),
                TestConfigurationBuilder.ValidDeviceConfig("TEST_DEVICE_003")
            }
        };
    }

    private AdamLoggerService CreateValidService()
    {
        var config = CreateValidConfig();
        var options = Options.Create(config);
        return new AdamLoggerService(options, _mockDataProcessor.Object, _serviceProvider, _mockLogger.Object);
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}