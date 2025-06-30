// Industrial.Adam.Logger - Dependency Injection Extensions
// Extension methods for registering the ADAM Logger service and its dependencies

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Industrial.Adam.Logger.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register ADAM Logger services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the ADAM Logger as a reusable service with all required dependencies
    /// </summary>
    /// <param name="services">Service collection to register services with</param>
    /// <param name="configureOptions">Configuration action for setting up ADAM Logger options</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// <code>
    /// services.AddAdamLogger(config =>
    /// {
    ///     config.PollIntervalMs = 1000;
    ///     config.Devices.Add(new AdamDeviceConfig
    ///     {
    ///         DeviceId = "LINE1_ADAM",
    ///         IpAddress = "192.168.1.100",
    ///         Channels = { /* channel configurations */ }
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAdamLogger(this IServiceCollection services, Action<AdamLoggerConfig> configureOptions)
    {
        // Register configuration
        services.Configure(configureOptions);
        
        // Register core services with default implementations
        services.AddSingleton<IDataValidator, DefaultDataValidator>();
        services.AddSingleton<IDataTransformer, DefaultDataTransformer>();
        services.AddSingleton<IDataProcessor, DefaultDataProcessor>();
        services.AddSingleton<IRetryPolicyService, RetryPolicyService>();
        services.AddSingleton<IAdamLoggerService, AdamLoggerService>();
        
        // Register as hosted service for automatic start/stop lifecycle management
        services.AddHostedService<AdamLoggerService>(provider => 
            (AdamLoggerService)provider.GetRequiredService<IAdamLoggerService>());
        
        // Register health check for monitoring
        services.AddHealthChecks()
            .AddCheck<AdamLoggerService>("adam_logger");

        return services;
    }

    /// <summary>
    /// Register a custom data processor implementation for application-specific logic
    /// </summary>
    /// <typeparam name="T">Custom data processor type that implements IDataProcessor</typeparam>
    /// <param name="services">Service collection to register with</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// <code>
    /// services.AddCustomDataProcessor&lt;MyCustomProcessor&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCustomDataProcessor<T>(this IServiceCollection services) 
        where T : class, IDataProcessor
    {
        services.AddSingleton<IDataProcessor, T>();
        return services;
    }

    /// <summary>
    /// Register a custom data validator implementation for application-specific validation
    /// </summary>
    /// <typeparam name="T">Custom data validator type that implements IDataValidator</typeparam>
    /// <param name="services">Service collection to register with</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// <code>
    /// services.AddCustomDataValidator&lt;MyCustomValidator&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCustomDataValidator<T>(this IServiceCollection services) 
        where T : class, IDataValidator
    {
        services.AddSingleton<IDataValidator, T>();
        return services;
    }

    /// <summary>
    /// Register a custom data transformer implementation for application-specific transformations
    /// </summary>
    /// <typeparam name="T">Custom data transformer type that implements IDataTransformer</typeparam>
    /// <param name="services">Service collection to register with</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// <code>
    /// services.AddCustomDataTransformer&lt;MyCustomTransformer&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCustomDataTransformer<T>(this IServiceCollection services) 
        where T : class, IDataTransformer
    {
        services.AddSingleton<IDataTransformer, T>();
        return services;
    }

    /// <summary>
    /// Register ADAM Logger with configuration loaded from IConfiguration
    /// </summary>
    /// <param name="services">Service collection to register with</param>
    /// <param name="configurationSectionName">Name of the configuration section (default: "AdamLogger")</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// <code>
    /// // Load configuration from appsettings.json "AdamLogger" section
    /// services.AddAdamLoggerFromConfiguration();
    /// 
    /// // Load from custom section name
    /// services.AddAdamLoggerFromConfiguration("MyAdamConfig");
    /// </code>
    /// </example>
    public static IServiceCollection AddAdamLoggerFromConfiguration(
        this IServiceCollection services, 
        string configurationSectionName = "AdamLogger")
    {
        // Configuration will be bound from IConfiguration in the service constructor
        services.AddOptions<AdamLoggerConfig>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register core services
        services.AddSingleton<IDataValidator, DefaultDataValidator>();
        services.AddSingleton<IDataTransformer, DefaultDataTransformer>();
        services.AddSingleton<IDataProcessor, DefaultDataProcessor>();
        services.AddSingleton<IRetryPolicyService, RetryPolicyService>();
        services.AddSingleton<IAdamLoggerService, AdamLoggerService>();
        
        // Register as hosted service
        services.AddHostedService<AdamLoggerService>(provider => 
            (AdamLoggerService)provider.GetRequiredService<IAdamLoggerService>());
        
        // Register health check
        services.AddHealthChecks()
            .AddCheck<AdamLoggerService>("adam_logger");

        return services;
    }
}