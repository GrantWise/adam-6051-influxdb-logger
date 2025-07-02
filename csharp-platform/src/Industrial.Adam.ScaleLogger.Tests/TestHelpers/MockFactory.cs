// Industrial.Adam.ScaleLogger.Tests - Mock Factory
// Adapted from ADAM-6051 patterns for scale logger mocking

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Interfaces;
using Industrial.Adam.ScaleLogger.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Industrial.Adam.ScaleLogger.Tests.TestHelpers;

/// <summary>
/// Factory for creating mocked dependencies for scale logger unit testing
/// </summary>
public static class TestMockFactory
{
    /// <summary>
    /// Create a mock IScaleLoggerService with configurable behavior
    /// </summary>
    /// <param name="isRunning">Default running state</param>
    /// <param name="deviceCount">Number of configured devices to return</param>
    /// <returns>Configured mock scale logger service</returns>
    public static Mock<IScaleLoggerService> CreateMockScaleLoggerService(
        bool isRunning = true,
        int deviceCount = 2)
    {
        var mock = new Mock<IScaleLoggerService>();
        
        // Create reactive streams
        var dataSubject = new Subject<ScaleDataReading>();
        var healthSubject = new Subject<ScaleDeviceHealth>();
        
        mock.Setup(s => s.DataStream).Returns(dataSubject.AsObservable());
        mock.Setup(s => s.HealthStream).Returns(healthSubject.AsObservable());
        mock.Setup(s => s.IsRunning).Returns(isRunning);
        
        // Create device list
        var devices = Enumerable.Range(0, deviceCount)
            .Select(i => $"DEVICE_{i:D3}")
            .ToList();
        mock.Setup(s => s.ConfiguredDevices).Returns(devices);
        
        // Setup method behaviors
        mock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        mock.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        mock.Setup(s => s.AddDeviceAsync(It.IsAny<ScaleDeviceConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        mock.Setup(s => s.RemoveDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        mock.Setup(s => s.GetDeviceHealthAsync(It.IsAny<string>()))
            .ReturnsAsync(TestConfigurationBuilder.ValidScaleDeviceHealth());
            
        mock.Setup(s => s.GetAllDeviceHealthAsync())
            .ReturnsAsync(devices.Select(d => TestConfigurationBuilder.ValidScaleDeviceHealth(d)).ToList());
            
        mock.Setup(s => s.TestDeviceConnectivityAsync(It.IsAny<ScaleDeviceConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectivityTestResult
            {
                Success = true,
                Duration = TimeSpan.FromSeconds(1),
                TestReadings = new[] { TestConfigurationBuilder.ValidScaleDataReading() }
            });
            
        mock.Setup(s => s.ReadDeviceNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestConfigurationBuilder.ValidScaleDataReading() });
            
        mock.Setup(s => s.DiscoverProtocolAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestConfigurationBuilder.ValidProtocolTemplate());
        
        return mock;
    }

    /// <summary>
    /// Create a mock ILogger for testing
    /// </summary>
    /// <typeparam name="T">Type being logged</typeparam>
    /// <returns>Mock logger</returns>
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mock = new Mock<ILogger<T>>();
        
        // Setup the Log method to capture log calls
        mock.Setup(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Verifiable();
            
        // Setup IsEnabled to return true for all log levels during testing
        mock.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>()))
            .Returns(true);
            
        return mock;
    }

    /// <summary>
    /// Create a mock scale logger service that throws exceptions for testing error handling
    /// </summary>
    /// <param name="exceptionMessage">Exception message to throw</param>
    /// <returns>Mock service that throws</returns>
    public static Mock<IScaleLoggerService> CreateThrowingMockScaleLoggerService(
        string exceptionMessage = "Mock service error")
    {
        var mock = new Mock<IScaleLoggerService>();
        
        mock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(s => s.AddDeviceAsync(It.IsAny<ScaleDeviceConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(s => s.GetDeviceHealthAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
            
        mock.Setup(s => s.ReadDeviceNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
            
        return mock;
    }

    /// <summary>
    /// Create a mock scale logger service with conditional behavior based on device ID
    /// </summary>
    /// <param name="failingDeviceIds">Device IDs that should fail operations</param>
    /// <param name="slowDeviceIds">Device IDs that should have slow responses</param>
    /// <returns>Configured conditional mock service</returns>
    public static Mock<IScaleLoggerService> CreateConditionalMockScaleLoggerService(
        IEnumerable<string>? failingDeviceIds = null,
        IEnumerable<string>? slowDeviceIds = null)
    {
        var mock = CreateMockScaleLoggerService();
        var failingIds = failingDeviceIds?.ToHashSet() ?? new HashSet<string>();
        var slowIds = slowDeviceIds?.ToHashSet() ?? new HashSet<string>();
        
        // Override device-specific methods with conditional behavior
        mock.Setup(s => s.GetDeviceHealthAsync(It.IsAny<string>()))
            .Returns<string>(deviceId =>
            {
                if (failingIds.Contains(deviceId))
                {
                    return Task.FromResult<ScaleDeviceHealth?>(null);
                }
                
                var health = slowIds.Contains(deviceId) ?
                    TestConfigurationBuilder.ValidScaleDeviceHealth(deviceId, DeviceStatus.Warning) :
                    TestConfigurationBuilder.ValidScaleDeviceHealth(deviceId);
                
                return Task.FromResult<ScaleDeviceHealth?>(health);
            });
            
        mock.Setup(s => s.ReadDeviceNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (deviceId, ct) =>
            {
                if (failingIds.Contains(deviceId))
                {
                    throw new InvalidOperationException($"Device {deviceId} is not responding");
                }
                
                if (slowIds.Contains(deviceId))
                {
                    await Task.Delay(3000, ct); // Simulate slow response
                }
                
                return new[] { TestConfigurationBuilder.ValidScaleDataReading(deviceId) };
            });
            
        return mock;
    }

    /// <summary>
    /// Create a mock that simulates realistic scale logger behavior for integration testing
    /// </summary>
    /// <param name="deviceConfigs">Device configurations to simulate</param>
    /// <returns>Realistic mock scale logger service</returns>
    public static Mock<IScaleLoggerService> CreateRealisticMockScaleLoggerService(
        IEnumerable<ScaleDeviceConfig>? deviceConfigs = null)
    {
        var configs = deviceConfigs?.ToList() ?? TestConfigurationBuilder.DiverseScaleDeviceConfigs(3);
        var mock = new Mock<IScaleLoggerService>();
        
        // Create subjects for real-time streams
        var dataSubject = new Subject<ScaleDataReading>();
        var healthSubject = new Subject<ScaleDeviceHealth>();
        
        mock.Setup(s => s.DataStream).Returns(dataSubject.AsObservable());
        mock.Setup(s => s.HealthStream).Returns(healthSubject.AsObservable());
        mock.Setup(s => s.IsRunning).Returns(true);
        mock.Setup(s => s.ConfiguredDevices).Returns(configs.Select(c => c.DeviceId).ToList());
        
        // Simulate periodic data generation
        Task.Run(async () =>
        {
            var random = new Random();
            while (true)
            {
                foreach (var config in configs)
                {
                    var weight = random.NextDouble() * 5000; // 0-5000kg
                    var reading = new ScaleDataReading
                    {
                        DeviceId = config.DeviceId,
                        DeviceName = config.Name,
                        Channel = config.Channels.First(),
                        Timestamp = DateTimeOffset.UtcNow,
                        WeightValue = weight,
                        RawValue = $"ST,GS,{weight:F2}",
                        Unit = "kg",
                        StandardizedWeightKg = (decimal)weight,
                        Quality = random.NextDouble() > 0.1 ? DataQuality.Good : DataQuality.Uncertain,
                        IsStable = random.NextDouble() > 0.2,
                        AcquisitionTime = TimeSpan.FromMilliseconds(random.Next(50, 500))
                    };
                    
                    dataSubject.OnNext(reading);
                }
                
                await Task.Delay(5000); // 5 second intervals
            }
        });
        
        return mock;
    }

    /// <summary>
    /// Create mock TCP client for testing scale device communication
    /// </summary>
    /// <param name="responses">Predefined responses to return</param>
    /// <param name="shouldConnect">Whether connection should succeed</param>
    /// <returns>Mock TCP client behavior</returns>
    public static Dictionary<string, object> CreateMockTcpBehavior(
        IEnumerable<string>? responses = null,
        bool shouldConnect = true)
    {
        var responseQueue = new Queue<string>(responses ?? new[] { "ST,GS,1234.56", "ST,GS,2345.67" });
        
        return new Dictionary<string, object>
        {
            ["ShouldConnect"] = shouldConnect,
            ["Responses"] = responseQueue,
            ["ConnectionDelay"] = TimeSpan.FromMilliseconds(100),
            ["ResponseDelay"] = TimeSpan.FromMilliseconds(50)
        };
    }

    /// <summary>
    /// Create test data for bulk operations
    /// </summary>
    /// <param name="count">Number of items to create</param>
    /// <returns>Collection of test data</returns>
    public static IEnumerable<T> CreateTestData<T>(int count, Func<int, T> factory)
    {
        return Enumerable.Range(0, count).Select(factory);
    }
}

