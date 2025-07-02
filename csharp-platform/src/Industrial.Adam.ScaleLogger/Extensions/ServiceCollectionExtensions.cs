// Industrial.Adam.ScaleLogger - Dependency Injection Extensions
// Following proven ADAM-6051 service registration patterns

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data.Repositories;
using Industrial.Adam.ScaleLogger.Infrastructure;
using Industrial.Adam.ScaleLogger.Interfaces;
using Industrial.Adam.ScaleLogger.Services;
using Industrial.Adam.ScaleLogger.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Extensions;

/// <summary>
/// Extension methods for registering Scale Logger services
/// Following proven ADAM-6051 dependency injection patterns
/// 
/// Usage:
/// services.AddScaleLogger(configuration);
/// services.AddScaleLoggerDatabase(configuration); // Register repositories
/// 
/// Or with explicit database provider:
/// services.AddScaleLoggerPostgreSQL(connectionString);
/// services.AddScaleLogger(configuration);
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Scale Logger services to dependency injection container
    /// </summary>
    public static IServiceCollection AddScaleLogger(this IServiceCollection services, 
        IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Bind configuration
        var config = new Adam4571Config();
        configuration.GetSection("ScaleLogger").Bind(config);
        services.AddSingleton(config);
        services.AddSingleton(config.Discovery);

        // Register core services
        services.AddSingleton<RetryPolicyService>();
        services.AddSingleton<ScaleDeviceManager>();
        services.AddSingleton<ProtocolDiscoveryService>();
        
        // Register repositories (will be provided by AddScaleLoggerDatabase)
        // services.AddScoped<IWeighingRepository, WeighingRepository>();
        // services.AddScoped<IDeviceRepository, DeviceRepository>();
        // services.AddScoped<ISystemEventRepository, SystemEventRepository>();
        
        services.AddSingleton<IScaleLoggerService, ScaleLoggerService>();

        return services;
    }

    /// <summary>
    /// Add Scale Logger services with custom configuration
    /// </summary>
    public static IServiceCollection AddScaleLogger(this IServiceCollection services, 
        Adam4571Config config)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (config == null) throw new ArgumentNullException(nameof(config));

        // Register configuration
        services.AddSingleton(config);
        services.AddSingleton(config.Discovery);

        // Register core services
        services.AddSingleton<RetryPolicyService>();
        services.AddSingleton<ScaleDeviceManager>();
        services.AddSingleton<ProtocolDiscoveryService>();
        
        // Register repositories (will be provided by AddScaleLoggerDatabase)
        // services.AddScoped<IWeighingRepository, WeighingRepository>();
        // services.AddScoped<IDeviceRepository, DeviceRepository>();
        // services.AddScoped<ISystemEventRepository, SystemEventRepository>();
        
        services.AddSingleton<IScaleLoggerService, ScaleLoggerService>();

        return services;
    }

    /// <summary>
    /// Add Scale Logger services with configuration factory
    /// </summary>
    public static IServiceCollection AddScaleLogger(this IServiceCollection services,
        Func<IServiceProvider, Adam4571Config> configFactory)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configFactory == null) throw new ArgumentNullException(nameof(configFactory));

        // Register configuration factory
        services.AddSingleton(configFactory);
        services.AddSingleton<Adam4571Config>(provider => configFactory(provider));
        services.AddSingleton<ProtocolDiscoveryConfig>(provider => configFactory(provider).Discovery);

        // Register core services
        services.AddSingleton<RetryPolicyService>();
        services.AddSingleton<ScaleDeviceManager>();
        services.AddSingleton<ProtocolDiscoveryService>();
        
        // Register repositories (will be provided by AddScaleLoggerDatabase)
        // services.AddScoped<IWeighingRepository, WeighingRepository>();
        // services.AddScoped<IDeviceRepository, DeviceRepository>();
        // services.AddScoped<ISystemEventRepository, SystemEventRepository>();
        
        services.AddSingleton<IScaleLoggerService, ScaleLoggerService>();

        return services;
    }

    /// <summary>
    /// Add Scale Logger as hosted service for background operation
    /// </summary>
    public static IServiceCollection AddScaleLoggerHostedService(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScaleLogger(configuration);
        services.AddHostedService<ScaleLoggerHostedService>();
        return services;
    }

    /// <summary>
    /// Add Scale Logger as hosted service with custom configuration
    /// </summary>
    public static IServiceCollection AddScaleLoggerHostedService(this IServiceCollection services,
        Adam4571Config config)
    {
        services.AddScaleLogger(config);
        services.AddHostedService<ScaleLoggerHostedService>();
        return services;
    }
}

/// <summary>
/// Hosted service wrapper for Scale Logger following ADAM-6051 patterns
/// </summary>
internal sealed class ScaleLoggerHostedService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IScaleLoggerService _scaleLogger;
    private readonly ILogger<ScaleLoggerHostedService> _logger;

    public ScaleLoggerHostedService(
        IScaleLoggerService scaleLogger,
        ILogger<ScaleLoggerHostedService> logger)
    {
        _scaleLogger = scaleLogger ?? throw new ArgumentNullException(nameof(scaleLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting Scale Logger Hosted Service");
            
            var started = await _scaleLogger.StartAsync(stoppingToken);
            if (!started)
            {
                _logger.LogError("Failed to start Scale Logger Service");
                return;
            }

            // Keep service running until cancellation requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scale Logger Hosted Service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scale Logger Hosted Service encountered an error");
            throw;
        }
        finally
        {
            try
            {
                await _scaleLogger.StopAsync(CancellationToken.None);
                _logger.LogInformation("Scale Logger Hosted Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Scale Logger Service");
            }
        }
    }

    public override void Dispose()
    {
        _scaleLogger?.Dispose();
        base.Dispose();
    }
}