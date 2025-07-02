// Industrial.Adam.ScaleLogger - Database Configuration
// Configuration for PostgreSQL and SQLite database providers

using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.ScaleLogger.Configuration;

/// <summary>
/// Database configuration supporting PostgreSQL and SQLite
/// </summary>
public sealed class DatabaseConfig
{
    /// <summary>
    /// Database provider type
    /// </summary>
    [Required]
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;

    /// <summary>
    /// Database connection string
    /// </summary>
    [Required]
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Whether to automatically run migrations on startup
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Whether to enable sensitive data logging (development only)
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    [Range(30, 300)]
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum retry attempts for transient failures
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int RetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Connection pooling configuration
    /// </summary>
    public ConnectionPoolConfig ConnectionPool { get; set; } = new();
}

/// <summary>
/// Database provider enumeration
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// PostgreSQL database (recommended for production)
    /// </summary>
    PostgreSQL,

    /// <summary>
    /// SQLite database (recommended for development/single-scale)
    /// </summary>
    SQLite
}

/// <summary>
/// Connection pooling configuration
/// </summary>
public sealed class ConnectionPoolConfig
{
    /// <summary>
    /// Maximum number of connections in the pool
    /// </summary>
    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 20;

    /// <summary>
    /// Minimum number of connections in the pool
    /// </summary>
    [Range(0, 50)]
    public int MinPoolSize { get; set; } = 1;

    /// <summary>
    /// Connection lifetime in seconds
    /// </summary>
    [Range(60, 3600)]
    public int ConnectionLifetimeSeconds { get; set; } = 300;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(5, 60)]
    public int ConnectionTimeoutSeconds { get; set; } = 15;
}