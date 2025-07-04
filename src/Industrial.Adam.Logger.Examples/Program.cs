// Industrial.Adam.Logger.Examples - Simple Console Demo
// This demonstrates basic usage of the ADAM Logger library
// For comprehensive examples, see EXAMPLES.md in the csharp folder

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Extensions;
using Industrial.Adam.Logger.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Examples;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("ADAM-6051 InfluxDB Logger - Console Demo");
        Console.WriteLine("========================================");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add ADAM Logger service with clean configuration
                services.AddAdamLogger(config =>
                {
                    // Set sensible defaults
                    config.PollIntervalMs = 2000;
                    config.HealthCheckIntervalMs = 10000;
                    config.MaxConcurrentDevices = 1;

                    // Configure InfluxDB with environment-aware URL
                    var isProduction = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Production";
                    config.InfluxDb = new InfluxDbConfig
                    {
                        Url = isProduction ? "http://influxdb:8086" : "http://localhost:8086",
                        Token = "adam-super-secret-token",
                        Organization = "adam_org",
                        Bucket = "adam_counters", 
                        Measurement = "counter_data",
                        WriteBatchSize = 50,
                        FlushIntervalMs = 5000
                    };

                    // Add demo device for local development
                    if (!isProduction)
                    {
                        config.DemoMode = true;
                        config.Devices.Add(new AdamDeviceConfig
                        {
                            DeviceId = "DEMO_ADAM_001",
                            IpAddress = "127.0.0.1",
                            Port = 502,
                            UnitId = 1,
                            TimeoutMs = 2000,
                            MaxRetries = 1,
                            Channels = new List<ChannelConfig>
                            {
                                new()
                                {
                                    ChannelNumber = 0,
                                    Name = "DemoCounter",
                                    StartRegister = 0,
                                    RegisterCount = 2,
                                    Enabled = true,
                                    MinValue = 0,
                                    MaxValue = 4294967295,
                                    ScaleFactor = 1.0,
                                    Offset = 0.0,
                                    DecimalPlaces = 0
                                }
                            }
                        });
                    }
                });

                // Apply environment variable overrides (clean approach)
                services.PostConfigure<AdamLoggerConfig>(config =>
                {
                    context.Configuration.GetSection("AdamLogger").Bind(config);
                });

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddConsole();
                });
            })
            .UseConsoleLifetime()
            .Build();

        // Get the ADAM Logger service
        var adamLogger = host.Services.GetRequiredService<IAdamLoggerService>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine("Starting ADAM Logger service...");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        // Subscribe to data stream
        var dataSubscription = adamLogger.DataStream.Subscribe(
            data =>
            {
                Console.WriteLine($"[{data.Timestamp:HH:mm:ss.fff}] {data.DeviceId} Ch{data.Channel}: " +
                                 $"Raw={data.RawValue}, Processed={data.ProcessedValue}, Quality={data.Quality}");
                if (data.Rate.HasValue)
                {
                    Console.WriteLine($"    Rate: {data.Rate.Value:F2} units/min");
                }
            },
            error =>
            {
                logger.LogError(error, "Error in data stream");
            });

        // Subscribe to health updates
        var healthSubscription = adamLogger.HealthStream.Subscribe(
            health =>
            {
                Console.WriteLine($"[HEALTH] {health.DeviceId}: {health.Status} " +
                                 $"(Connected: {health.IsConnected}, " +
                                 $"Reads: {health.TotalReads}, " +
                                 $"Failures: {health.ConsecutiveFailures})");
            },
            error =>
            {
                logger.LogError(error, "Error in health stream");
            });

        try
        {
            // Start the service and run until cancelled
            await host.RunAsync();
        }
        finally
        {
            Console.WriteLine("\nShutting down...");
            dataSubscription.Dispose();
            healthSubscription.Dispose();
        }
    }
}