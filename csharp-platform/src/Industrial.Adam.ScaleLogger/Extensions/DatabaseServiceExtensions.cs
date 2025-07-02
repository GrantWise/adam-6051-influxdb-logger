// Industrial.Adam.ScaleLogger - Database Service Extensions
// Dependency injection configuration for Entity Framework Core

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data;
using Industrial.Adam.ScaleLogger.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.ScaleLogger.Extensions;

/// <summary>
/// Extension methods for database service registration
/// Following proven ADAM-6051 dependency injection patterns
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Add database services with automatic provider detection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddScaleLoggerDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register database configuration
        services.Configure<DatabaseConfig>(configuration.GetSection("ScaleLogger:Database"));

        // Register DbContext with factory pattern for multi-provider support
        services.AddDbContext<ScaleLoggerDbContext>((serviceProvider, options) =>
        {
            var databaseConfig = serviceProvider.GetRequiredService<IOptions<DatabaseConfig>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<ScaleLoggerDbContext>>();

            ConfigureDbContextOptions(options, databaseConfig, logger);
        });

        // Register repositories
        services.AddScoped<IWeighingRepository, WeighingRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ISystemEventRepository, SystemEventRepository>();

        return services;
    }

    /// <summary>
    /// Add database services with explicit configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="databaseConfig">Database configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddScaleLoggerDatabase(
        this IServiceCollection services,
        DatabaseConfig databaseConfig)
    {
        // Register database configuration
        services.AddSingleton(Options.Create(databaseConfig));

        // Register DbContext
        services.AddDbContext<ScaleLoggerDbContext>((serviceProvider, options) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ScaleLoggerDbContext>>();
            ConfigureDbContextOptions(options, databaseConfig, logger);
        });

        // Register repositories
        services.AddScoped<IWeighingRepository, WeighingRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ISystemEventRepository, SystemEventRepository>();

        return services;
    }

    /// <summary>
    /// Add PostgreSQL database services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="configureOptions">Optional additional configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddScaleLoggerPostgreSQL(
        this IServiceCollection services,
        string connectionString,
        Action<DatabaseConfig>? configureOptions = null)
    {
        var config = new DatabaseConfig
        {
            Provider = DatabaseProvider.PostgreSQL,
            ConnectionString = connectionString
        };

        configureOptions?.Invoke(config);

        return services.AddScaleLoggerDatabase(config);
    }

    /// <summary>
    /// Add SQLite database services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQLite connection string</param>
    /// <param name="configureOptions">Optional additional configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddScaleLoggerSQLite(
        this IServiceCollection services,
        string connectionString,
        Action<DatabaseConfig>? configureOptions = null)
    {
        var config = new DatabaseConfig
        {
            Provider = DatabaseProvider.SQLite,
            ConnectionString = connectionString
        };

        configureOptions?.Invoke(config);

        return services.AddScaleLoggerDatabase(config);
    }

    /// <summary>
    /// Configure DbContext options based on provider
    /// </summary>
    /// <param name="options">DbContext options builder</param>
    /// <param name="config">Database configuration</param>
    /// <param name="logger">Logger instance</param>
    private static void ConfigureDbContextOptions(
        DbContextOptionsBuilder options,
        DatabaseConfig config,
        ILogger logger)
    {
        switch (config.Provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(config.ConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(config.CommandTimeoutSeconds);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: config.MaxRetryAttempts,
                        maxRetryDelay: TimeSpan.FromMilliseconds(config.RetryDelayMs),
                        errorCodesToAdd: null);
                });
                logger.LogDebug("Configured PostgreSQL database with connection timeout {Timeout}s", 
                    config.CommandTimeoutSeconds);
                break;

            case DatabaseProvider.SQLite:
                options.UseSqlite(config.ConnectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(config.CommandTimeoutSeconds);
                });
                logger.LogDebug("Configured SQLite database with connection timeout {Timeout}s", 
                    config.CommandTimeoutSeconds);
                break;

            default:
                throw new NotSupportedException($"Database provider {config.Provider} is not supported");
        }

        // Common configurations
        if (config.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
            logger.LogWarning("Sensitive data logging is enabled - this should only be used in development");
        }

        options.EnableServiceProviderCaching();
        options.EnableDetailedErrors();
    }

    /// <summary>
    /// Ensure database is created and migrated
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public static async Task EnsureDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScaleLoggerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScaleLoggerDbContext>>();
        var config = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseConfig>>().Value;

        try
        {
            if (config.AutoMigrate)
            {
                logger.LogInformation("Applying database migrations...");
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations completed successfully");
            }
            else
            {
                logger.LogInformation("Ensuring database exists...");
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("Database ensured successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }
}