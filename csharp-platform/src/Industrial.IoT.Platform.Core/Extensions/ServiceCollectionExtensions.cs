// Industrial.IoT.Platform.Core - Service Collection Extensions
// Core dependency injection configuration for Industrial IoT Platform

using Microsoft.Extensions.DependencyInjection;

namespace Industrial.IoT.Platform.Core.Extensions;

/// <summary>
/// Extension methods for configuring Industrial IoT Platform core services
/// Foundation layer for dependency injection - does not reference higher layers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Industrial IoT Platform core services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddIndustrialIoTCore(this IServiceCollection services)
    {
        // Core platform services only - no dependencies on higher layers
        // Additional services will be registered by their respective layers
        
        return services;
    }
}