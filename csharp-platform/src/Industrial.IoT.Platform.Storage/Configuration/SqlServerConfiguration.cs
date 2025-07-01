// Industrial.IoT.Platform.Storage - SQL Server Configuration
// Configuration model for SQL Server transactional repository following existing pattern quality

using System.ComponentModel.DataAnnotations;

namespace Industrial.IoT.Platform.Storage.Configuration;

/// <summary>
/// Configuration for SQL Server transactional repository
/// Following validation patterns from existing ADAM logger configuration
/// </summary>
public sealed class SqlServerConfiguration
{
    /// <summary>
    /// SQL Server name or connection endpoint
    /// </summary>
    [Required(ErrorMessage = "SQL Server name is required")]
    [MinLength(1, ErrorMessage = "SQL Server name cannot be empty")]
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Database name
    /// </summary>
    [Required(ErrorMessage = "Database name is required")]
    [MinLength(1, ErrorMessage = "Database name cannot be empty")]
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server user name (if using SQL Server authentication)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SQL Server password (if using SQL Server authentication)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Use integrated security (Windows authentication)
    /// </summary>
    public bool UseIntegratedSecurity { get; set; } = true;

    /// <summary>
    /// Enable connection encryption
    /// </summary>
    public bool EncryptConnection { get; set; } = true;

    /// <summary>
    /// Trust server certificate (for development only)
    /// </summary>
    public bool TrustServerCertificate { get; set; } = false;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(5, 300, ErrorMessage = "ConnectionTimeoutSeconds must be between 5 seconds and 5 minutes")]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    [Range(30, 3600, ErrorMessage = "CommandTimeoutSeconds must be between 30 seconds and 1 hour")]
    public int CommandTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum number of retries for failed operations
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds
    /// </summary>
    [Range(1, 60, ErrorMessage = "RetryDelaySeconds must be between 1 second and 60 seconds")]
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Connection pool minimum size
    /// </summary>
    [Range(0, 100, ErrorMessage = "MinPoolSize must be between 0 and 100")]
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Connection pool maximum size
    /// </summary>
    [Range(10, 1000, ErrorMessage = "MaxPoolSize must be between 10 and 1,000")]
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Enable connection pooling
    /// </summary>
    public bool EnableConnectionPooling { get; set; } = true;

    /// <summary>
    /// Enable sensitive data logging (for development only)
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// Enable detailed errors in Entity Framework
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// Multiple Active Result Sets (MARS) support
    /// </summary>
    public bool EnableMultipleActiveResultSets { get; set; } = true;

    /// <summary>
    /// Application name for connection identification
    /// </summary>
    public string ApplicationName { get; set; } = "Industrial.IoT.Platform";

    /// <summary>
    /// Connection load balancing timeout in seconds
    /// </summary>
    [Range(0, 300, ErrorMessage = "LoadBalanceTimeoutSeconds must be between 0 and 300 seconds")]
    public int LoadBalanceTimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Packet size for network communication
    /// </summary>
    [Range(512, 32768, ErrorMessage = "PacketSize must be between 512 and 32,768 bytes")]
    public int PacketSize { get; set; } = 8192;

    /// <summary>
    /// Enable asynchronous processing
    /// </summary>
    public bool EnableAsyncProcessing { get; set; } = true;

    /// <summary>
    /// Connection lifetime in seconds (0 = infinite)
    /// </summary>
    [Range(0, 86400, ErrorMessage = "ConnectionLifetimeSeconds must be between 0 and 24 hours")]
    public int ConnectionLifetimeSeconds { get; set; } = 3600; // 1 hour

    /// <summary>
    /// Connection idle timeout in seconds
    /// </summary>
    [Range(0, 3600, ErrorMessage = "ConnectionIdleTimeoutSeconds must be between 0 and 1 hour")]
    public int ConnectionIdleTimeoutSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Enable automatic database migration
    /// </summary>
    public bool EnableAutoMigration { get; set; } = false;

    /// <summary>
    /// Build SQL Server connection string
    /// </summary>
    /// <returns>Complete connection string</returns>
    public string GetConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            ConnectTimeout = ConnectionTimeoutSeconds,
            CommandTimeout = CommandTimeoutSeconds,
            Pooling = EnableConnectionPooling,
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize,
            MultipleActiveResultSets = EnableMultipleActiveResultSets,
            ApplicationName = ApplicationName,
            LoadBalanceTimeout = LoadBalanceTimeoutSeconds,
            PacketSize = PacketSize,
            ConnectRetryCount = MaxRetries,
            ConnectRetryInterval = Math.Max(1, RetryDelaySeconds),
            Encrypt = EncryptConnection,
            TrustServerCertificate = TrustServerCertificate
        };

        // Authentication
        if (UseIntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Username))
                throw new InvalidOperationException("Username is required when not using integrated security");
            
            builder.UserID = Username;
            builder.Password = Password ?? string.Empty;
        }

        // Connection lifetime (Load Balance Timeout is the correct property)
        if (ConnectionLifetimeSeconds > 0)
        {
            builder.LoadBalanceTimeout = ConnectionLifetimeSeconds;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Validate configuration and return validation results
    /// Following existing ADAM logger validation patterns
    /// </summary>
    /// <returns>Collection of validation results</returns>
    public IEnumerable<ValidationResult> ValidateConfiguration()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        // Standard data annotation validation
        Validator.TryValidateObject(this, context, results, true);

        // Custom business rule validation
        if (!UseIntegratedSecurity && string.IsNullOrWhiteSpace(Username))
        {
            results.Add(new ValidationResult(
                "Username is required when not using integrated security",
                new[] { nameof(Username), nameof(UseIntegratedSecurity) }));
        }

        if (MinPoolSize > MaxPoolSize)
        {
            results.Add(new ValidationResult(
                "MinPoolSize cannot be greater than MaxPoolSize",
                new[] { nameof(MinPoolSize), nameof(MaxPoolSize) }));
        }

        if (CommandTimeoutSeconds < ConnectionTimeoutSeconds)
        {
            results.Add(new ValidationResult(
                "CommandTimeoutSeconds should be greater than or equal to ConnectionTimeoutSeconds",
                new[] { nameof(CommandTimeoutSeconds), nameof(ConnectionTimeoutSeconds) }));
        }

        // Security validation
        if (!EncryptConnection)
        {
            results.Add(new ValidationResult(
                "Connection encryption is strongly recommended for production environments",
                new[] { nameof(EncryptConnection) }));
        }

        if (TrustServerCertificate && EncryptConnection)
        {
            results.Add(new ValidationResult(
                "TrustServerCertificate should only be enabled in development environments",
                new[] { nameof(TrustServerCertificate) }));
        }

        if (EnableSensitiveDataLogging)
        {
            results.Add(new ValidationResult(
                "Sensitive data logging should only be enabled in development environments",
                new[] { nameof(EnableSensitiveDataLogging) }));
        }

        // Performance validation
        if (MaxPoolSize > 500)
        {
            results.Add(new ValidationResult(
                $"MaxPoolSize ({MaxPoolSize}) is very high and may impact SQL Server performance",
                new[] { nameof(MaxPoolSize) }));
        }

        if (ConnectionLifetimeSeconds > 0 && ConnectionLifetimeSeconds < 300)
        {
            results.Add(new ValidationResult(
                "ConnectionLifetimeSeconds should be at least 5 minutes (300 seconds) to avoid excessive connection churn",
                new[] { nameof(ConnectionLifetimeSeconds) }));
        }

        return results;
    }

    /// <summary>
    /// Create default configuration for development/testing
    /// </summary>
    /// <returns>Default configuration instance</returns>
    public static SqlServerConfiguration CreateDefault()
    {
        return new SqlServerConfiguration
        {
            Server = "localhost",
            Database = "IndustrialIoTPlatform",
            UseIntegratedSecurity = true,
            EncryptConnection = false, // Simplified for local development
            TrustServerCertificate = true, // Allow for local development
            ConnectionTimeoutSeconds = 30,
            CommandTimeoutSeconds = 120,
            MaxRetries = 3,
            RetryDelaySeconds = 5,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            EnableConnectionPooling = true,
            EnableSensitiveDataLogging = true, // Enable for development
            EnableDetailedErrors = true, // Enable for development
            EnableMultipleActiveResultSets = true,
            ApplicationName = "Industrial.IoT.Platform.Development",
            LoadBalanceTimeoutSeconds = 0,
            PacketSize = 8192,
            EnableAsyncProcessing = true,
            ConnectionLifetimeSeconds = 3600,
            ConnectionIdleTimeoutSeconds = 300,
            EnableAutoMigration = true // Enable for development
        };
    }

    /// <summary>
    /// Create production-optimized configuration
    /// </summary>
    /// <returns>Production configuration instance</returns>
    public static SqlServerConfiguration CreateProduction()
    {
        return new SqlServerConfiguration
        {
            Server = "your-production-sql-server",
            Database = "IndustrialIoTPlatform",
            UseIntegratedSecurity = false, // Use SQL authentication for production
            Username = "your-username",
            Password = "your-password",
            EncryptConnection = true,
            TrustServerCertificate = false,
            ConnectionTimeoutSeconds = 30,
            CommandTimeoutSeconds = 300, // Longer timeouts for complex queries
            MaxRetries = 5,
            RetryDelaySeconds = 10,
            MinPoolSize = 10,
            MaxPoolSize = 200, // Higher pool size for production load
            EnableConnectionPooling = true,
            EnableSensitiveDataLogging = false, // Disable for security
            EnableDetailedErrors = false, // Disable for security
            EnableMultipleActiveResultSets = true,
            ApplicationName = "Industrial.IoT.Platform.Production",
            LoadBalanceTimeoutSeconds = 30,
            PacketSize = 16384, // Larger packet size for better throughput
            EnableAsyncProcessing = true,
            ConnectionLifetimeSeconds = 7200, // 2 hours
            ConnectionIdleTimeoutSeconds = 600, // 10 minutes
            EnableAutoMigration = false // Manual migrations in production
        };
    }

    /// <summary>
    /// Create high-availability configuration for enterprise deployments
    /// </summary>
    /// <returns>High-availability configuration instance</returns>
    public static SqlServerConfiguration CreateHighAvailability()
    {
        return new SqlServerConfiguration
        {
            Server = "your-ha-sql-cluster,1433;Failover Partner=your-failover-server,1433",
            Database = "IndustrialIoTPlatform",
            UseIntegratedSecurity = false,
            Username = "your-username",
            Password = "your-password",
            EncryptConnection = true,
            TrustServerCertificate = false,
            ConnectionTimeoutSeconds = 30,
            CommandTimeoutSeconds = 600, // Very long timeouts for complex operations
            MaxRetries = 10, // More retries for HA scenarios
            RetryDelaySeconds = 15,
            MinPoolSize = 20,
            MaxPoolSize = 500, // Large pool for high concurrency
            EnableConnectionPooling = true,
            EnableSensitiveDataLogging = false,
            EnableDetailedErrors = false,
            EnableMultipleActiveResultSets = true,
            ApplicationName = "Industrial.IoT.Platform.HA",
            LoadBalanceTimeoutSeconds = 60,
            PacketSize = 32768, // Maximum packet size for best throughput
            EnableAsyncProcessing = true,
            ConnectionLifetimeSeconds = 14400, // 4 hours
            ConnectionIdleTimeoutSeconds = 1800, // 30 minutes
            EnableAutoMigration = false
        };
    }
}