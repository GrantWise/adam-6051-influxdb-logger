# ADAM-6051 InfluxDB Logger - Usage Examples

This document provides comprehensive examples of how to integrate and use the Industrial ADAM Logger in various scenarios. Each example includes detailed explanations, implementation steps, and complete code snippets.

## Table of Contents

1. [Basic Console Application](#1-basic-console-application)
2. [ASP.NET Core Web Application](#2-aspnet-core-web-application)
3. [Windows Service](#3-windows-service)
4. [Custom Data Processing](#4-custom-data-processing)
5. [Health Monitoring Integration](#5-health-monitoring-integration)
6. [Message Queue Integration](#6-message-queue-integration)
7. [Configuration Examples](#7-configuration-examples)
8. [Testing and Development](#8-testing-and-development)

---

## 1. Basic Console Application

### Overview
The simplest way to use the ADAM Logger in a console application with dependency injection.

### Implementation Steps
1. Create a new console application
2. Add the Industrial.Adam.Logger NuGet package
3. Configure services and start the logger

### Code Example

**Program.cs**
```csharp
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Extensions;
using Industrial.Adam.Logger.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdamLoggerConsole
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Register ADAM Logger with configuration
                    services.AddAdamLogger(config =>
                    {
                        config.PollIntervalMs = 1000;
                        config.HealthCheckIntervalMs = 30000;
                        config.MaxConcurrentDevices = 2;
                        
                        // Add device configuration
                        config.Devices.Add(new AdamDeviceConfig
                        {
                            DeviceId = "ADAM_001",
                            IpAddress = "192.168.1.100",
                            Port = 502,
                            UnitId = 1,
                            Channels = new List<ChannelConfig>
                            {
                                new ChannelConfig
                                {
                                    ChannelNumber = 0,
                                    Name = "ProductionCounter",
                                    RegisterAddress = 0,
                                    RegisterCount = 2,
                                    Enabled = true,
                                    MinValue = 0,
                                    MaxValue = 4294967295
                                }
                            }
                        });
                    });
                })
                .UseConsoleLifetime()
                .Build();

            // Get the logger service and subscribe to data
            var adamLogger = host.Services.GetRequiredService<IAdamLoggerService>();
            
            // Subscribe to data stream
            var subscription = adamLogger.DataStream.Subscribe(data =>
            {
                Console.WriteLine($"[{data.Timestamp:HH:mm:ss}] {data.DeviceId} Ch{data.Channel}: {data.ProcessedValue} (Quality: {data.Quality})");
            });

            try
            {
                await host.RunAsync();
            }
            finally
            {
                subscription.Dispose();
            }
        }
    }
}
```

**Project File (AdamLoggerConsole.csproj)**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <ProjectReference Include="../Industrial.Adam.Logger/Industrial.Adam.Logger.csproj" />
  </ItemGroup>
</Project>
```

---

## 2. ASP.NET Core Web Application

### Overview
Integrate the ADAM Logger into an ASP.NET Core application with health checks and real-time data streaming.

### Implementation Steps
1. Create ASP.NET Core application
2. Configure services in Program.cs
3. Add health check endpoints
4. Implement real-time data streaming with SignalR

### Code Example

**Program.cs**
```csharp
using Industrial.Adam.Logger.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

// Configure ADAM Logger from appsettings.json
builder.Services.AddAdamLogger(builder.Configuration.GetSection("AdamLogger"));

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<DataHub>("/datahub");

await app.RunAsync();
```

**Controllers/AdamController.cs**
```csharp
using Industrial.Adam.Logger.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AdamController : ControllerBase
{
    private readonly IAdamLoggerService _adamLogger;

    public AdamController(IAdamLoggerService adamLogger)
    {
        _adamLogger = adamLogger;
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        var health = await _adamLogger.GetAllDeviceHealthAsync();
        return Ok(health);
    }

    [HttpGet("devices/{deviceId}/health")]
    public async Task<IActionResult> GetDeviceHealth(string deviceId)
    {
        var health = await _adamLogger.GetDeviceHealthAsync(deviceId);
        if (health == null)
            return NotFound();
        
        return Ok(health);
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start()
    {
        if (_adamLogger.IsRunning)
            return BadRequest("Service is already running");
            
        await _adamLogger.StartAsync();
        return Ok("Service started");
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        if (!_adamLogger.IsRunning)
            return BadRequest("Service is not running");
            
        await _adamLogger.StopAsync();
        return Ok("Service stopped");
    }
}
```

**Hubs/DataHub.cs**
```csharp
using Industrial.Adam.Logger.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Reactive.Linq;

public class DataHub : Hub
{
    private readonly IAdamLoggerService _adamLogger;

    public DataHub(IAdamLoggerService adamLogger)
    {
        _adamLogger = adamLogger;
    }

    public override async Task OnConnectedAsync()
    {
        // Subscribe client to real-time data
        var subscription = _adamLogger.DataStream
            .Sample(TimeSpan.FromSeconds(1)) // Throttle to 1 update per second
            .Subscribe(async data =>
            {
                await Clients.All.SendAsync("DataUpdate", new
                {
                    DeviceId = data.DeviceId,
                    Channel = data.Channel,
                    Value = data.ProcessedValue,
                    Timestamp = data.Timestamp,
                    Quality = data.Quality.ToString()
                });
            });

        Context.Items["subscription"] = subscription;
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items["subscription"] is IDisposable subscription)
        {
            subscription.Dispose();
        }
        await base.OnDisconnectedAsync(exception);
    }
}
```

**appsettings.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Industrial.Adam.Logger": "Debug"
    }
  },
  "AdamLogger": {
    "PollIntervalMs": 1000,
    "HealthCheckIntervalMs": 30000,
    "MaxConcurrentDevices": 5,
    "EnablePerformanceCounters": true,
    "Devices": [
      {
        "DeviceId": "ADAM_001",
        "IpAddress": "192.168.1.100",
        "Port": 502,
        "UnitId": 1,
        "Channels": [
          {
            "ChannelNumber": 0,
            "Name": "ProductionCounter",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "Enabled": true
          }
        ]
      }
    ]
  }
}
```

---

## 3. Windows Service

### Overview
Deploy the ADAM Logger as a Windows Service for production environments.

### Implementation Steps
1. Create Worker Service project
2. Install Microsoft.Extensions.Hosting.WindowsServices
3. Configure for Windows Service deployment

### Code Example

**Program.cs**
```csharp
using Industrial.Adam.Logger.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "ADAM-6051 InfluxDB Logger";
    })
    .ConfigureServices((context, services) =>
    {
        services.AddAdamLogger(context.Configuration.GetSection("AdamLogger"));
        services.AddHostedService<AdamLoggerWorker>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddEventLog();
    });

var host = builder.Build();
await host.RunAsync();
```

**AdamLoggerWorker.cs**
```csharp
using Industrial.Adam.Logger.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AdamLoggerWorker : BackgroundService
{
    private readonly IAdamLoggerService _adamLogger;
    private readonly ILogger<AdamLoggerWorker> _logger;

    public AdamLoggerWorker(IAdamLoggerService adamLogger, ILogger<AdamLoggerWorker> logger)
    {
        _adamLogger = adamLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ADAM Logger Worker starting");

        // Subscribe to data for logging/monitoring
        var dataSubscription = _adamLogger.DataStream.Subscribe(
            data => _logger.LogDebug("Data received: {DeviceId} Ch{Channel} = {Value}", 
                data.DeviceId, data.Channel, data.ProcessedValue),
            error => _logger.LogError(error, "Error in data stream"),
            () => _logger.LogInformation("Data stream completed")
        );

        // Subscribe to health updates
        var healthSubscription = _adamLogger.HealthStream.Subscribe(
            health => _logger.LogDebug("Health update: {DeviceId} - {Status}", 
                health.DeviceId, health.Status),
            error => _logger.LogError(error, "Error in health stream")
        );

        try
        {
            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            dataSubscription.Dispose();
            healthSubscription.Dispose();
            _logger.LogInformation("ADAM Logger Worker stopped");
        }
    }
}
```

**Install as Windows Service**
```powershell
# Publish the application
dotnet publish -c Release -o ./publish

# Install as service
sc create "ADAM-6051-Logger" binPath="C:\path\to\publish\YourApp.exe"
sc start "ADAM-6051-Logger"
```

---

## 4. Custom Data Processing

### Overview
Implement custom data validation, transformation, and processing logic.

### Implementation Steps
1. Create custom implementations of core interfaces
2. Register custom services with dependency injection
3. Configure processing pipeline

### Code Example

**CustomDataValidator.cs**
```csharp
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;

public class CustomDataValidator : IDataValidator
{
    public DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channel)
    {
        // Custom validation logic
        if (reading.RawValue < 0)
            return DataQuality.Bad;

        // Check for impossible values based on business rules
        if (channel.Name.Contains("Production") && reading.RawValue > 1000000)
            return DataQuality.Questionable;

        // Rate of change validation
        if (reading.Rate.HasValue && Math.Abs(reading.Rate.Value) > 1000)
            return DataQuality.Questionable;

        return DataQuality.Good;
    }

    public bool IsValidRange(long value, ChannelConfig channel)
    {
        return value >= channel.MinValue && value <= channel.MaxValue;
    }

    public bool IsValidRateOfChange(double? rate, ChannelConfig channel)
    {
        if (!rate.HasValue) return true;
        
        // Custom rate limits based on channel type
        var maxRate = channel.Name.Contains("Temperature") ? 10.0 : 1000.0;
        return Math.Abs(rate.Value) <= maxRate;
    }
}
```

**CustomDataTransformer.cs**
```csharp
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;

public class CustomDataTransformer : IDataTransformer
{
    public double? TransformValue(long rawValue, ChannelConfig channel)
    {
        // Apply channel-specific transformations
        var transformed = rawValue * channel.ScaleFactor + channel.Offset;

        // Custom business logic transformations
        if (channel.Name.Contains("Temperature"))
        {
            // Convert from Celsius to Fahrenheit if needed
            if (channel.Tags.ContainsKey("unit") && channel.Tags["unit"].ToString() == "fahrenheit")
            {
                transformed = transformed * 9.0 / 5.0 + 32.0;
            }
        }

        // Round to appropriate precision
        return Math.Round(transformed, channel.DecimalPlaces ?? 2);
    }

    public Dictionary<string, object> EnrichTags(Dictionary<string, object> baseTags, 
        AdamDeviceConfig deviceConfig, ChannelConfig channelConfig)
    {
        var enrichedTags = new Dictionary<string, object>(baseTags);

        // Add device location
        enrichedTags["location"] = deviceConfig.Tags.GetValueOrDefault("location", "unknown");
        
        // Add shift information
        var currentHour = DateTime.Now.Hour;
        enrichedTags["shift"] = currentHour switch
        {
            >= 6 and < 14 => "day",
            >= 14 and < 22 => "evening",
            _ => "night"
        };

        // Add data quality metrics
        enrichedTags["processing_timestamp"] = DateTimeOffset.UtcNow;

        return enrichedTags;
    }
}
```

**Service Registration**
```csharp
services.AddAdamLogger(config => { /* configuration */ });

// Replace default implementations with custom ones
services.AddCustomDataValidator<CustomDataValidator>();
services.AddCustomDataTransformer<CustomDataTransformer>();
```

---

## 5. Health Monitoring Integration

### Overview
Integrate ADAM Logger health checks with ASP.NET Core health monitoring and external monitoring systems.

### Implementation Steps
1. Configure health checks
2. Create custom health check endpoints
3. Integrate with monitoring systems

### Code Example

**Health Check Configuration**
```csharp
services.AddHealthChecks()
    .AddCheck<AdamLoggerHealthCheck>("adam-logger")
    .AddInfluxDB(options => 
    {
        options.Uri = new Uri("http://localhost:8086");
        options.Database = "adam-data";
    });

// Advanced health check UI (optional)
services.AddHealthChecksUI()
    .AddInMemoryStorage();
```

**Custom Health Check Endpoint**
```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IAdamLoggerService _adamLogger;
    private readonly HealthCheckService _healthCheckService;

    public HealthController(IAdamLoggerService adamLogger, HealthCheckService healthCheckService)
    {
        _adamLogger = adamLogger;
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var result = await _healthCheckService.CheckHealthAsync();
        var response = new
        {
            Status = result.Status.ToString(),
            TotalDuration = result.TotalDuration,
            Entries = result.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration,
                Data = e.Value.Data
            })
        };

        var statusCode = result.Status switch
        {
            HealthStatus.Healthy => 200,
            HealthStatus.Degraded => 200,
            HealthStatus.Unhealthy => 503,
            _ => 500
        };

        return StatusCode(statusCode, response);
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDeviceHealth()
    {
        var devices = await _adamLogger.GetAllDeviceHealthAsync();
        return Ok(devices.Select(d => new
        {
            d.DeviceId,
            d.Status,
            d.IsConnected,
            d.LastSuccessfulRead,
            d.TotalReads,
            d.ConsecutiveFailures,
            HealthScore = CalculateHealthScore(d)
        }));
    }

    private static double CalculateHealthScore(AdamDeviceHealth health)
    {
        if (health.TotalReads == 0) return 0.0;
        
        var successRate = (double)(health.TotalReads - health.TotalFailures) / health.TotalReads;
        var recentFailurePenalty = Math.Min(health.ConsecutiveFailures * 0.1, 0.5);
        
        return Math.Max(0.0, (successRate - recentFailurePenalty) * 100.0);
    }
}
```

---

## 6. Message Queue Integration

### Overview
Integrate with message queues (RabbitMQ, Azure Service Bus, etc.) for enterprise data distribution.

### Implementation Steps
1. Install message queue client library
2. Create message publisher service
3. Configure message routing and error handling

### Code Example

**RabbitMQ Integration**
```csharp
// Install: RabbitMQ.Client package

public interface IMessagePublisher
{
    Task PublishAsync<T>(string topic, T message);
}

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQPublisher> _logger;

    public RabbitMQPublisher(IConfiguration config, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory()
        {
            HostName = config["RabbitMQ:Host"],
            UserName = config["RabbitMQ:Username"],
            Password = config["RabbitMQ:Password"]
        };
        
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        // Declare exchange
        _channel.ExchangeDeclare("adam-data", ExchangeType.Topic, durable: true);
    }

    public async Task PublishAsync<T>(string topic, T message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            
            _channel.BasicPublish(
                exchange: "adam-data",
                routingKey: topic,
                basicProperties: null,
                body: body);
                
            _logger.LogDebug("Published message to {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Topic}", topic);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

**Message Publisher Service**
```csharp
public class AdamDataPublisher : BackgroundService
{
    private readonly IAdamLoggerService _adamLogger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<AdamDataPublisher> _logger;

    public AdamDataPublisher(
        IAdamLoggerService adamLogger,
        IMessagePublisher messagePublisher,
        ILogger<AdamDataPublisher> logger)
    {
        _adamLogger = adamLogger;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Batch data for efficient publishing
        var dataBuffer = new List<AdamDataReading>();
        
        var subscription = _adamLogger.DataStream
            .Buffer(TimeSpan.FromSeconds(5)) // Batch every 5 seconds
            .Where(batch => batch.Any())
            .Subscribe(async batch =>
            {
                try
                {
                    foreach (var reading in batch)
                    {
                        var topic = $"adam.data.{reading.DeviceId}.{reading.Channel}";
                        await _messagePublisher.PublishAsync(topic, new
                        {
                            reading.DeviceId,
                            reading.Channel,
                            reading.RawValue,
                            reading.ProcessedValue,
                            reading.Timestamp,
                            reading.Quality,
                            reading.Rate
                        });
                    }
                    
                    _logger.LogInformation("Published {Count} readings", batch.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish batch of {Count} readings", batch.Count);
                }
            });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            subscription.Dispose();
        }
    }
}
```

---

## 7. Configuration Examples

### Overview
Various configuration patterns and examples for different deployment scenarios.

### Production Configuration
```json
{
  "AdamLogger": {
    "PollIntervalMs": 1000,
    "HealthCheckIntervalMs": 30000,
    "MaxConcurrentDevices": 10,
    "EnablePerformanceCounters": true,
    "EnableDetailedLogging": false,
    "DataBufferSize": 1000,
    "BatchSize": 100,
    "Devices": [
      {
        "DeviceId": "PROD_LINE_A_001",
        "IpAddress": "192.168.100.10",
        "Port": 502,
        "UnitId": 1,
        "TimeoutMs": 5000,
        "MaxRetries": 3,
        "RetryDelayMs": 1000,
        "Tags": {
          "location": "Production Line A",
          "area": "Assembly"
        },
        "Channels": [
          {
            "ChannelNumber": 0,
            "Name": "PartCounter",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "Enabled": true,
            "MinValue": 0,
            "MaxValue": 4294967295,
            "ScaleFactor": 1.0,
            "Offset": 0.0,
            "DecimalPlaces": 0,
            "Tags": {
              "unit": "parts",
              "type": "counter"
            }
          }
        ]
      }
    ]
  }
}
```

### Development Configuration
```json
{
  "AdamLogger": {
    "PollIntervalMs": 2000,
    "HealthCheckIntervalMs": 10000,
    "MaxConcurrentDevices": 2,
    "EnablePerformanceCounters": false,
    "EnableDetailedLogging": true,
    "Devices": [
      {
        "DeviceId": "DEV_SIMULATOR_001",
        "IpAddress": "127.0.0.1",
        "Port": 5020,
        "UnitId": 1,
        "TimeoutMs": 2000,
        "MaxRetries": 1,
        "Channels": [
          {
            "ChannelNumber": 0,
            "Name": "TestCounter",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "Enabled": true,
            "MinValue": 0,
            "MaxValue": 1000000
          }
        ]
      }
    ]
  }
}
```

---

## 8. Testing and Development

### Overview
Patterns for testing applications that use the ADAM Logger.

### Unit Testing with Mocks
```csharp
[Test]
public async Task ProcessDataReading_ValidReading_ShouldProcessCorrectly()
{
    // Arrange
    var mockAdamLogger = new Mock<IAdamLoggerService>();
    var testReading = new AdamDataReading
    {
        DeviceId = "TEST_001",
        Channel = 0,
        RawValue = 12345,
        Timestamp = DateTimeOffset.UtcNow,
        Quality = DataQuality.Good
    };

    mockAdamLogger.Setup(x => x.DataStream)
        .Returns(Observable.Return(testReading));

    var processor = new DataProcessor(mockAdamLogger.Object);

    // Act
    var result = await processor.ProcessNextReading();

    // Assert
    Assert.That(result.DeviceId, Is.EqualTo("TEST_001"));
    Assert.That(result.Quality, Is.EqualTo(DataQuality.Good));
}
```

### Integration Testing
```csharp
[Test]
public async Task AdamLogger_RealDevice_ShouldConnectAndReadData()
{
    // This test requires a real ADAM device or simulator
    var config = new AdamLoggerConfig
    {
        PollIntervalMs = 1000,
        Devices = new List<AdamDeviceConfig>
        {
            new AdamDeviceConfig
            {
                DeviceId = "TEST_DEVICE",
                IpAddress = "192.168.1.100", // Use your test device IP
                Port = 502,
                UnitId = 1,
                Channels = new List<ChannelConfig>
                {
                    new ChannelConfig
                    {
                        ChannelNumber = 0,
                        RegisterAddress = 0,
                        RegisterCount = 2,
                        Enabled = true
                    }
                }
            }
        }
    };

    var services = new ServiceCollection();
    services.AddAdamLogger(_ => config);
    services.AddLogging();

    var provider = services.BuildServiceProvider();
    var adamLogger = provider.GetRequiredService<IAdamLoggerService>();

    var dataReceived = new List<AdamDataReading>();
    var subscription = adamLogger.DataStream
        .Take(5) // Take first 5 readings
        .Subscribe(dataReceived.Add);

    try
    {
        await adamLogger.StartAsync();
        await Task.Delay(10000); // Wait for data
        
        Assert.That(dataReceived.Count, Is.GreaterThan(0));
    }
    finally
    {
        subscription.Dispose();
        await adamLogger.StopAsync();
    }
}
```

---

## Support and Best Practices

### Performance Tips
- Use appropriate polling intervals (1-5 seconds for most applications)
- Configure batch sizes for high-throughput scenarios
- Monitor memory usage with large data buffers
- Use connection pooling for multiple devices

### Security Considerations
- Use VPN or secure networks for device communication
- Implement proper authentication for web APIs
- Secure configuration files and connection strings
- Regular security updates and monitoring

### Troubleshooting
- Enable detailed logging for development
- Monitor device health metrics
- Check network connectivity and firewall settings
- Validate Modbus device configuration

For more examples and detailed documentation, refer to the test projects and source code in the repository.