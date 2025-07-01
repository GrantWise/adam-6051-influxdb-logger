// Industrial.IoT.Platform.Protocols - Service Collection Extensions
// Dependency injection configuration for protocol discovery and signal stability

using Industrial.IoT.Platform.Protocols.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Protocols.Extensions;

/// <summary>
/// Extension methods for configuring protocol discovery and signal stability services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add protocol discovery services with signal stability monitoring
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddProtocolDiscovery(this IServiceCollection services)
    {
        // Template repository for protocol templates
        services.AddSingleton<TemplateRepository>();
        
        // Signal stability monitoring (singleton for shared state across services)
        services.AddSingleton<SignalStabilityMonitor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SignalStabilityMonitor>>();
            var config = SignalStabilityConfiguration.Default;
            
            // Configure for industrial environment
            config = config with
            {
                SampleBufferSize = 500,           // Larger buffer for industrial monitoring
                AnalysisIntervalMs = 1000,        // More frequent analysis for real-time feedback  
                StabilityThreshold = 85.0,        // Higher threshold for industrial reliability
                DropoutThresholdMs = 3000,        // Detect dropouts faster
                AllowUnknownSignals = false       // Strict filtering for production
            };
            
            return new SignalStabilityMonitor(logger, config);
        });
        
        // Protocol discovery engine (scoped for discovery sessions)
        services.AddScoped<ProtocolDiscoveryEngine>();
        
        // Protocol discovery service (main service interface)
        services.AddScoped<ProtocolDiscoveryService>();
        
        return services;
    }
    
    /// <summary>
    /// Configure signal stability monitoring for specific environments
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="environment">Industrial environment type</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureSignalStabilityForEnvironment(
        this IServiceCollection services, 
        IndustrialEnvironment environment)
    {
        services.AddSingleton<SignalStabilityMonitor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SignalStabilityMonitor>>();
            var config = GetEnvironmentSpecificConfig(environment);
            return new SignalStabilityMonitor(logger, config);
        });
        
        return services;
    }
    
    /// <summary>
    /// Get signal stability configuration optimized for specific industrial environments
    /// </summary>
    /// <param name="environment">Industrial environment type</param>
    /// <returns>Optimized configuration</returns>
    private static SignalStabilityConfiguration GetEnvironmentSpecificConfig(IndustrialEnvironment environment)
    {
        return environment switch
        {
            IndustrialEnvironment.CleanRoom => new SignalStabilityConfiguration
            {
                SampleBufferSize = 300,
                AnalysisIntervalMs = 2000,
                StabilityThreshold = 95.0,        // Very high standards for clean environments
                DropoutThresholdMs = 5000,
                AllowUnknownSignals = false
            },
            
            IndustrialEnvironment.Factory => new SignalStabilityConfiguration
            {
                SampleBufferSize = 500,
                AnalysisIntervalMs = 1000,
                StabilityThreshold = 80.0,        // Moderate interference expected
                DropoutThresholdMs = 3000,
                AllowUnknownSignals = false
            },
            
            IndustrialEnvironment.Warehouse => new SignalStabilityConfiguration
            {
                SampleBufferSize = 400,
                AnalysisIntervalMs = 1500,
                StabilityThreshold = 75.0,        // Some forklift/machinery interference
                DropoutThresholdMs = 4000,
                AllowUnknownSignals = true
            },
            
            IndustrialEnvironment.Harsh => new SignalStabilityConfiguration
            {
                SampleBufferSize = 600,
                AnalysisIntervalMs = 500,         // More frequent monitoring
                StabilityThreshold = 65.0,        // Lower threshold for harsh conditions
                DropoutThresholdMs = 2000,        // Faster dropout detection
                AllowUnknownSignals = true
            },
            
            IndustrialEnvironment.Laboratory => new SignalStabilityConfiguration
            {
                SampleBufferSize = 200,
                AnalysisIntervalMs = 3000,
                StabilityThreshold = 90.0,        // High precision requirements
                DropoutThresholdMs = 10000,
                AllowUnknownSignals = false
            },
            
            _ => SignalStabilityConfiguration.Default
        };
    }
}

/// <summary>
/// Industrial environment types for signal stability optimization
/// </summary>
public enum IndustrialEnvironment
{
    /// <summary>
    /// Clean room environment with minimal interference
    /// </summary>
    CleanRoom,
    
    /// <summary>
    /// Standard factory floor with moderate interference
    /// </summary>
    Factory,
    
    /// <summary>
    /// Warehouse environment with mobile equipment interference
    /// </summary>
    Warehouse,
    
    /// <summary>
    /// Harsh industrial environment with high interference
    /// </summary>
    Harsh,
    
    /// <summary>
    /// Laboratory environment requiring high precision
    /// </summary>
    Laboratory
}