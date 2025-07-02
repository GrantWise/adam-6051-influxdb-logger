// Industrial.Adam.ScaleLogger - Example Application
// Demonstrates usage following proven ADAM-6051 patterns

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Industrial ADAM-4571 Scale Logger - Example Application");
        Console.WriteLine("Following proven ADAM-6051 patterns for industrial reliability");
        Console.WriteLine();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Create host
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add Scale Logger as hosted service
                services.AddScaleLoggerHostedService(configuration);
            })
            .Build();

        // Handle shutdown gracefully
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
            Console.WriteLine("\nShutdown requested...");
        };

        try
        {
            Console.WriteLine("Starting Scale Logger Service...");
            Console.WriteLine("Press Ctrl+C to stop");
            
            await host.RunAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Service stopped gracefully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}