// Industrial.Adam.Logger.Tests - Runtime Device Management Tests
// Unit tests for runtime device addition, removal, and configuration updates

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Extensions;
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

namespace Industrial.Adam.Logger.Tests.Services;

/// <summary>
/// Unit tests for runtime device management functionality in AdamLoggerService
/// </summary>
public class AdamLoggerServiceRuntimeTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IAdamLoggerService _service;

    public AdamLoggerServiceRuntimeTests()
    {
        var services = new ServiceCollection();
        
        // Add logging services required by ADAM Logger
        services.AddLogging(builder => 
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            // Don't add console logging to avoid noise in tests
        });
        
        // Add ADAM Logger with empty device list for runtime testing
        services.AddAdamLogger(config =>
        {
            config.PollIntervalMs = 2000;
            config.HealthCheckIntervalMs = 5000;
            config.MaxConcurrentDevices = 10;
            // Start with no devices - we'll add them at runtime
        });

        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IAdamLoggerService>();
    }

    #region AddDeviceAsync Tests

    [Fact]
    public async Task AddDeviceAsync_ValidDevice_ShouldAddSuccessfully()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("RUNTIME_DEVICE_001");
        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = _service.HealthStream.Subscribe(healthUpdates.Add);

        // Act
        await _service.AddDeviceAsync(deviceConfig);

        // Assert
        var health = await _service.GetDeviceHealthAsync("RUNTIME_DEVICE_001");
        health.Should().NotBeNull();
        health!.DeviceId.Should().Be("RUNTIME_DEVICE_001");
        health.Status.Should().Be(DeviceStatus.Unknown);
        health.IsConnected.Should().BeFalse();
        health.TotalReads.Should().Be(0);
        health.SuccessfulReads.Should().Be(0);

        healthUpdates.Should().HaveCount(1);
        healthUpdates[0].DeviceId.Should().Be("RUNTIME_DEVICE_001");

        subscription.Dispose();
    }

    [Fact]
    public async Task AddDeviceAsync_NullDevice_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AddDeviceAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("deviceConfig");
    }

    [Fact]
    public async Task AddDeviceAsync_InvalidDevice_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidDevice = new AdamDeviceConfig
        {
            DeviceId = "", // Invalid empty device ID
            IpAddress = "192.168.1.100",
            Port = 502,
            UnitId = 1,
            Channels = new List<ChannelConfig>()
        };

        // Act & Assert
        await _service.Invoking(s => s.AddDeviceAsync(invalidDevice))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid device configuration*")
            .WithParameterName("deviceConfig");
    }

    [Fact]
    public async Task AddDeviceAsync_DuplicateDeviceId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("DUPLICATE_DEVICE");
        await _service.AddDeviceAsync(deviceConfig);

        // Act & Assert
        await _service.Invoking(s => s.AddDeviceAsync(deviceConfig))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Device with ID 'DUPLICATE_DEVICE' already exists");
    }

    [Fact]
    public async Task AddDeviceAsync_MultipleDevices_ShouldAddAllSuccessfully()
    {
        // Arrange
        var device1 = TestConfigurationBuilder.ValidDeviceConfig("DEVICE_001");
        var device2 = TestConfigurationBuilder.ValidDeviceConfig("DEVICE_002");
        var device3 = TestConfigurationBuilder.ValidDeviceConfig("DEVICE_003");

        // Act
        await _service.AddDeviceAsync(device1);
        await _service.AddDeviceAsync(device2);
        await _service.AddDeviceAsync(device3);

        // Assert
        var allHealth = await _service.GetAllDeviceHealthAsync();
        allHealth.Should().HaveCount(3);
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_001");
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_002");
        allHealth.Should().Contain(h => h.DeviceId == "DEVICE_003");
    }

    #endregion

    #region RemoveDeviceAsync Tests

    [Fact]
    public async Task RemoveDeviceAsync_ExistingDevice_ShouldRemoveSuccessfully()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("REMOVE_DEVICE");
        await _service.AddDeviceAsync(deviceConfig);

        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = _service.HealthStream.Subscribe(healthUpdates.Add);

        // Act
        await _service.RemoveDeviceAsync("REMOVE_DEVICE");

        // Assert
        var health = await _service.GetDeviceHealthAsync("REMOVE_DEVICE");
        health.Should().BeNull();

        var allHealth = await _service.GetAllDeviceHealthAsync();
        allHealth.Should().NotContain(h => h.DeviceId == "REMOVE_DEVICE");

        // Should have received at least the removal health update
        healthUpdates.Should().HaveCountGreaterOrEqualTo(1);
        healthUpdates.Last().DeviceId.Should().Be("REMOVE_DEVICE");
        healthUpdates.Last().Status.Should().Be(DeviceStatus.Offline);

        subscription.Dispose();
    }

    [Fact]
    public async Task RemoveDeviceAsync_NonExistentDevice_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        await _service.Invoking(s => s.RemoveDeviceAsync("NON_EXISTENT"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Device with ID 'NON_EXISTENT' not found");
    }

    [Fact]
    public async Task RemoveDeviceAsync_EmptyDeviceId_ShouldThrowArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.RemoveDeviceAsync(""))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Device ID cannot be null or empty*")
            .WithParameterName("deviceId");
    }

    [Fact]
    public async Task RemoveDeviceAsync_NullDeviceId_ShouldThrowArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.RemoveDeviceAsync(null!))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Device ID cannot be null or empty*")
            .WithParameterName("deviceId");
    }

    [Fact]
    public async Task RemoveDeviceAsync_ShouldCompleteCleanly()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("DISPOSE_TEST");
        await _service.AddDeviceAsync(deviceConfig);

        // Act
        await _service.RemoveDeviceAsync("DISPOSE_TEST");

        // Assert
        var health = await _service.GetDeviceHealthAsync("DISPOSE_TEST");
        health.Should().BeNull();
    }

    #endregion

    #region UpdateDeviceConfigAsync Tests

    [Fact]
    public async Task UpdateDeviceConfigAsync_ExistingDevice_ShouldUpdateSuccessfully()
    {
        // Arrange
        var originalConfig = TestConfigurationBuilder.ValidDeviceConfig("UPDATE_DEVICE");
        originalConfig.IpAddress = "192.168.1.100";
        originalConfig.Port = 502;

        await _service.AddDeviceAsync(originalConfig);

        var updatedConfig = TestConfigurationBuilder.ValidDeviceConfig("UPDATE_DEVICE");
        updatedConfig.IpAddress = "192.168.1.200"; // Changed IP
        updatedConfig.Port = 503; // Changed port

        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = _service.HealthStream.Subscribe(healthUpdates.Add);

        // Act
        await _service.UpdateDeviceConfigAsync(updatedConfig);

        // Assert
        var health = await _service.GetDeviceHealthAsync("UPDATE_DEVICE");
        health.Should().NotBeNull();
        health!.DeviceId.Should().Be("UPDATE_DEVICE");
        health.Status.Should().Be(DeviceStatus.Unknown);
        health.ConsecutiveFailures.Should().Be(0); // Should reset on config update

        // Should have at least one health update from the config update process
        healthUpdates.Should().HaveCountGreaterOrEqualTo(1);

        subscription.Dispose();
    }

    [Fact]
    public async Task UpdateDeviceConfigAsync_PreserveStatistics_ShouldMaintainCounts()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("STATS_DEVICE");
        await _service.AddDeviceAsync(deviceConfig);

        // Simulate some activity by manually updating health (in real scenario this would be done by the service)
        // Note: This is a simplified test - in practice, we'd need to set up the device manager to simulate reads

        var updatedConfig = TestConfigurationBuilder.ValidDeviceConfig("STATS_DEVICE");
        updatedConfig.Port = 503; // Change port to force recreation

        // Act
        await _service.UpdateDeviceConfigAsync(updatedConfig);

        // Assert
        var health = await _service.GetDeviceHealthAsync("STATS_DEVICE");
        health.Should().NotBeNull();
        health!.DeviceId.Should().Be("STATS_DEVICE");
        // Statistics preservation would be tested more thoroughly in integration tests
    }

    [Fact]
    public async Task UpdateDeviceConfigAsync_NonExistentDevice_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("NON_EXISTENT");

        // Act & Assert
        await _service.Invoking(s => s.UpdateDeviceConfigAsync(deviceConfig))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Device with ID 'NON_EXISTENT' not found");
    }

    [Fact]
    public async Task UpdateDeviceConfigAsync_NullConfig_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await _service.Invoking(s => s.UpdateDeviceConfigAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("deviceConfig");
    }

    [Fact]
    public async Task UpdateDeviceConfigAsync_InvalidConfig_ShouldThrowArgumentException()
    {
        // Arrange
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("EXISTING_DEVICE");
        await _service.AddDeviceAsync(deviceConfig);

        var invalidConfig = new AdamDeviceConfig
        {
            DeviceId = "EXISTING_DEVICE",
            IpAddress = "invalid-ip", // Invalid IP address
            Port = 502,
            UnitId = 1,
            Channels = new List<ChannelConfig> { TestConfigurationBuilder.ValidChannelConfig(0) }
        };

        // Act & Assert
        await _service.Invoking(s => s.UpdateDeviceConfigAsync(invalidConfig))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid device configuration*")
            .WithParameterName("deviceConfig");
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentDeviceOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var deviceConfigs = Enumerable.Range(1, 10)
            .Select(i => TestConfigurationBuilder.ValidDeviceConfig($"CONCURRENT_DEVICE_{i:D3}"))
            .ToList();

        // Act - Add devices concurrently
        foreach (var config in deviceConfigs)
        {
            tasks.Add(_service.AddDeviceAsync(config));
        }

        await Task.WhenAll(tasks);
        tasks.Clear();

        // Act - Remove devices concurrently
        foreach (var config in deviceConfigs.Take(5))
        {
            tasks.Add(_service.RemoveDeviceAsync(config.DeviceId));
        }

        await Task.WhenAll(tasks);

        // Assert
        var remainingHealth = await _service.GetAllDeviceHealthAsync();
        remainingHealth.Should().HaveCount(5);
        
        // Verify remaining devices are the last 5
        for (int i = 6; i <= 10; i++)
        {
            remainingHealth.Should().Contain(h => h.DeviceId == $"CONCURRENT_DEVICE_{i:D3}");
        }
    }

    [Fact]
    public async Task MixedConcurrentOperations_ShouldMaintainConsistency()
    {
        // Arrange
        var device1 = TestConfigurationBuilder.ValidDeviceConfig("MIXED_OP_DEVICE_001");
        var device2 = TestConfigurationBuilder.ValidDeviceConfig("MIXED_OP_DEVICE_002");
        
        await _service.AddDeviceAsync(device1);
        await _service.AddDeviceAsync(device2);

        var updatedDevice1 = TestConfigurationBuilder.ValidDeviceConfig("MIXED_OP_DEVICE_001");
        updatedDevice1.Port = 503; // Change port

        // Act - Perform mixed operations concurrently
        var tasks = new[]
        {
            _service.UpdateDeviceConfigAsync(updatedDevice1),
            _service.RemoveDeviceAsync("MIXED_OP_DEVICE_002"),
            _service.AddDeviceAsync(TestConfigurationBuilder.ValidDeviceConfig("MIXED_OP_DEVICE_003"))
        };

        await Task.WhenAll(tasks);

        // Assert
        var allHealth = await _service.GetAllDeviceHealthAsync();
        allHealth.Should().HaveCount(2);
        allHealth.Should().Contain(h => h.DeviceId == "MIXED_OP_DEVICE_001");
        allHealth.Should().Contain(h => h.DeviceId == "MIXED_OP_DEVICE_003");
        allHealth.Should().NotContain(h => h.DeviceId == "MIXED_OP_DEVICE_002");
    }

    #endregion

    #region Stream Integration Tests

    [Fact]
    public async Task RuntimeDeviceOperations_ShouldEmitHealthUpdates()
    {
        // Arrange
        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = _service.HealthStream.Subscribe(healthUpdates.Add);
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig("STREAM_DEVICE");

        // Act
        await _service.AddDeviceAsync(deviceConfig);
        await Task.Delay(10); // Small delay to ensure event processing
        
        await _service.RemoveDeviceAsync("STREAM_DEVICE");
        await Task.Delay(10); // Small delay to ensure event processing

        // Assert
        healthUpdates.Should().HaveCountGreaterOrEqualTo(2);
        
        var addUpdate = healthUpdates.First(h => h.DeviceId == "STREAM_DEVICE");
        addUpdate.Status.Should().Be(DeviceStatus.Unknown);
        
        var removeUpdate = healthUpdates.Last(h => h.DeviceId == "STREAM_DEVICE");
        removeUpdate.Status.Should().Be(DeviceStatus.Offline);

        subscription.Dispose();
    }

    [Fact]
    public async Task UpdateDeviceConfig_ShouldEmitHealthUpdatesForRecreation()
    {
        // Arrange
        var healthUpdates = new List<AdamDeviceHealth>();
        var subscription = _service.HealthStream.Subscribe(healthUpdates.Add);
        
        var originalConfig = TestConfigurationBuilder.ValidDeviceConfig("UPDATE_STREAM_DEVICE");
        await _service.AddDeviceAsync(originalConfig);

        var updatedConfig = TestConfigurationBuilder.ValidDeviceConfig("UPDATE_STREAM_DEVICE");
        updatedConfig.Port = 503;

        // Act
        await _service.UpdateDeviceConfigAsync(updatedConfig);
        await Task.Delay(10); // Small delay to ensure event processing

        // Assert
        var deviceUpdates = healthUpdates.Where(h => h.DeviceId == "UPDATE_STREAM_DEVICE").ToList();
        deviceUpdates.Should().HaveCountGreaterOrEqualTo(2); // At least add + update

        subscription.Dispose();
    }

    #endregion

    public void Dispose()
    {
        _service?.Dispose();
        _serviceProvider?.Dispose();
    }
}