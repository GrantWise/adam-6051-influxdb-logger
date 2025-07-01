// Industrial.IoT.Platform.Storage - Entity Framework DbContext
// Database context for SQL Server transactional data following existing ADAM logger patterns

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Industrial.IoT.Platform.Storage.Configuration;
using Industrial.IoT.Platform.Storage.Data.Entities;
using System.Text.Json;

namespace Industrial.IoT.Platform.Storage.Data;

/// <summary>
/// Entity Framework database context for Industrial IoT Platform
/// Manages scale data, protocol templates, and device configurations
/// Following existing ADAM logger database patterns for consistency
/// </summary>
public sealed class IndustrialIoTDbContext : DbContext
{
    private readonly ILogger<IndustrialIoTDbContext>? _logger;
    private readonly SqlServerConfiguration? _configuration;

    /// <summary>
    /// Scale data readings from ADAM-4571 devices
    /// </summary>
    public DbSet<ScaleDataEntity> ScaleData { get; set; } = null!;

    /// <summary>
    /// Protocol templates for scale communication
    /// </summary>
    public DbSet<ProtocolTemplateEntity> ProtocolTemplates { get; set; } = null!;

    /// <summary>
    /// Device configuration settings
    /// </summary>
    public DbSet<DeviceConfigurationEntity> DeviceConfigurations { get; set; } = null!;

    /// <summary>
    /// Initialize DbContext with configuration options
    /// </summary>
    /// <param name="options">Entity Framework context options</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="configuration">SQL Server configuration</param>
    public IndustrialIoTDbContext(
        DbContextOptions<IndustrialIoTDbContext> options,
        ILogger<IndustrialIoTDbContext>? logger = null,
        SqlServerConfiguration? configuration = null) : base(options)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Configure Entity Framework model relationships and constraints
    /// </summary>
    /// <param name="modelBuilder">Model builder for entity configuration</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ScaleDataEntity
        ConfigureScaleDataEntity(modelBuilder);

        // Configure ProtocolTemplateEntity
        ConfigureProtocolTemplateEntity(modelBuilder);

        // Configure DeviceConfigurationEntity
        ConfigureDeviceConfigurationEntity(modelBuilder);

        // Configure global filters and conventions
        ConfigureGlobalConventions(modelBuilder);
    }

    /// <summary>
    /// Configure additional database options
    /// </summary>
    /// <param name="optionsBuilder">Options builder for configuration</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_configuration != null)
        {
            // Apply configuration-specific settings
            if (_configuration.EnableSensitiveDataLogging)
            {
                optionsBuilder.EnableSensitiveDataLogging();
            }

            if (_configuration.EnableDetailedErrors)
            {
                optionsBuilder.EnableDetailedErrors();
            }
        }

        // Enable logging if available
        if (_logger != null)
        {
            optionsBuilder.UseLoggerFactory(LoggerFactory.Create(builder => 
                builder.AddConsole()));
        }

        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Override SaveChanges to automatically update audit timestamps
    /// Following existing ADAM logger audit patterns
    /// </summary>
    /// <returns>Number of entities affected</returns>
    public override int SaveChanges()
    {
        UpdateAuditTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically update audit timestamps
    /// Following existing ADAM logger audit patterns
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities affected</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Configure ScaleDataEntity with indexes and constraints
    /// </summary>
    /// <param name="modelBuilder">Model builder for configuration</param>
    private static void ConfigureScaleDataEntity(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ScaleDataEntity>();

        // Primary key
        entity.HasKey(e => e.Id);

        // Indexes for performance
        entity.HasIndex(e => new { e.DeviceId, e.Timestamp })
              .HasDatabaseName("IX_ScaleData_DeviceId_Timestamp");

        entity.HasIndex(e => e.Timestamp)
              .HasDatabaseName("IX_ScaleData_Timestamp");

        entity.HasIndex(e => e.WeightKg)
              .HasDatabaseName("IX_ScaleData_WeightKg");

        entity.HasIndex(e => e.Quality)
              .HasDatabaseName("IX_ScaleData_Quality");

        // Configure decimal precision for weight measurements
        entity.Property(e => e.WeightKg)
              .HasPrecision(18, 6);

        // Configure JSON column for metadata
        entity.Property(e => e.MetadataJson)
              .HasConversion(
                  v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                  v => v == null ? null : JsonSerializer.Deserialize<string>(v, (JsonSerializerOptions?)null));

        // Set default values
        entity.Property(e => e.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.ModifiedAt)
              .HasDefaultValueSql("GETUTCDATE()");
    }

    /// <summary>
    /// Configure ProtocolTemplateEntity with indexes and constraints
    /// </summary>
    /// <param name="modelBuilder">Model builder for configuration</param>
    private static void ConfigureProtocolTemplateEntity(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProtocolTemplateEntity>();

        // Primary key
        entity.HasKey(e => e.Id);

        // Unique constraint on template name
        entity.HasIndex(e => e.TemplateName)
              .IsUnique()
              .HasDatabaseName("IX_ProtocolTemplates_TemplateName_Unique");

        // Indexes for performance
        entity.HasIndex(e => new { e.Manufacturer, e.Model })
              .HasDatabaseName("IX_ProtocolTemplates_Manufacturer_Model");

        entity.HasIndex(e => e.IsActive)
              .HasDatabaseName("IX_ProtocolTemplates_IsActive");

        entity.HasIndex(e => e.Priority)
              .HasDatabaseName("IX_ProtocolTemplates_Priority");

        // Set default values
        entity.Property(e => e.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.ModifiedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.IsActive)
              .HasDefaultValue(true);

        entity.Property(e => e.Priority)
              .HasDefaultValue(50);

        entity.Property(e => e.ConfidenceThreshold)
              .HasDefaultValue(75.0);
    }

    /// <summary>
    /// Configure DeviceConfigurationEntity with indexes and constraints
    /// </summary>
    /// <param name="modelBuilder">Model builder for configuration</param>
    private static void ConfigureDeviceConfigurationEntity(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DeviceConfigurationEntity>();

        // Primary key
        entity.HasKey(e => e.Id);

        // Unique constraint on device ID
        entity.HasIndex(e => e.DeviceId)
              .IsUnique()
              .HasDatabaseName("IX_DeviceConfigurations_DeviceId_Unique");

        // Indexes for performance
        entity.HasIndex(e => e.DeviceType)
              .HasDatabaseName("IX_DeviceConfigurations_DeviceType");

        entity.HasIndex(e => e.IsActive)
              .HasDatabaseName("IX_DeviceConfigurations_IsActive");

        entity.HasIndex(e => new { e.IpAddress, e.Port })
              .HasDatabaseName("IX_DeviceConfigurations_IpAddress_Port");

        // Set default values
        entity.Property(e => e.CreatedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.ModifiedAt)
              .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.IsActive)
              .HasDefaultValue(true);

        entity.Property(e => e.EnableHealthMonitoring)
              .HasDefaultValue(true);

        entity.Property(e => e.EnableStabilityMonitoring)
              .HasDefaultValue(true);

        entity.Property(e => e.StabilityThreshold)
              .HasDefaultValue(80.0);

        entity.Property(e => e.AcquisitionIntervalMs)
              .HasDefaultValue(1000);

        entity.Property(e => e.ConnectionTimeoutMs)
              .HasDefaultValue(10000);

        entity.Property(e => e.ReadTimeoutMs)
              .HasDefaultValue(5000);

        entity.Property(e => e.MaxRetries)
              .HasDefaultValue(3);

        entity.Property(e => e.RetryDelayMs)
              .HasDefaultValue(1000);
    }

    /// <summary>
    /// Configure global conventions and filters
    /// </summary>
    /// <param name="modelBuilder">Model builder for configuration</param>
    private static void ConfigureGlobalConventions(ModelBuilder modelBuilder)
    {
        // Configure all string properties to use Unicode (nvarchar)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string))
                {
                    property.SetIsUnicode(true);
                }
            }
        }

        // Configure DateTimeOffset properties to use UTC
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("datetimeoffset");
                }
            }
        }
    }

    /// <summary>
    /// Update audit timestamps for entities being saved
    /// Following existing ADAM logger audit patterns
    /// </summary>
    private void UpdateAuditTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in entries)
        {
            // Update ModifiedAt for all entities
            if (entry.Entity.GetType().GetProperty("ModifiedAt") != null)
            {
                entry.Property("ModifiedAt").CurrentValue = now;
            }

            // Set CreatedAt for new entities
            if (entry.State == EntityState.Added && 
                entry.Entity.GetType().GetProperty("CreatedAt") != null)
            {
                entry.Property("CreatedAt").CurrentValue = now;
            }
        }
    }

    /// <summary>
    /// Seed database with default protocol templates
    /// Following existing ADAM logger initialization patterns
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of templates seeded</returns>
    public async Task<int> SeedProtocolTemplatesAsync(CancellationToken cancellationToken = default)
    {
        // Check if templates already exist
        if (await ProtocolTemplates.AnyAsync(cancellationToken))
        {
            _logger?.LogDebug("Protocol templates already exist, skipping seed");
            return 0;
        }

        _logger?.LogInformation("Seeding default protocol templates");

        var templates = CreateDefaultProtocolTemplates();
        await ProtocolTemplates.AddRangeAsync(templates, cancellationToken);
        
        var count = await SaveChangesAsync(cancellationToken);
        _logger?.LogInformation("Seeded {Count} protocol templates", count);
        
        return count;
    }

    /// <summary>
    /// Create default protocol templates for common scale manufacturers
    /// Based on existing Python implementation templates
    /// </summary>
    /// <returns>Collection of default protocol templates</returns>
    private static List<ProtocolTemplateEntity> CreateDefaultProtocolTemplates()
    {
        return new List<ProtocolTemplateEntity>
        {
            // Mettler Toledo Standard Template
            new()
            {
                TemplateName = "mettler_toledo_standard",
                DisplayName = "Mettler Toledo Standard Protocol",
                Manufacturer = "Mettler Toledo",
                Description = "Standard Mettler Toledo scale protocol with SI/SIR commands",
                Version = "1.0.0",
                Priority = 90,
                ConfidenceThreshold = 80.0,
                IsBuiltIn = true,
                CommunicationSettingsJson = JsonSerializer.Serialize(new
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = "None",
                    StopBits = 1,
                    FlowControl = "None"
                }),
                CommandTemplatesJson = JsonSerializer.Serialize(new
                {
                    RequestWeight = "SI\r\n",
                    RequestWeightImmediate = "SIR\r\n",
                    Reset = "Z\r\n"
                }),
                ResponsePatternsJson = JsonSerializer.Serialize(new
                {
                    WeightPattern = @"S\s+S\s+([\d\.-]+)\s*(\w*)",
                    StablePattern = @"S\s+S\s+",
                    UnstablePattern = @"S\s+D\s+",
                    OverloadPattern = @"S\s+\+\s+",
                    UnderloadPattern = @"S\s+-\s+"
                }),
                Author = "System",
                SupportedBaudRates = "9600,19200,38400",
                EnvironmentalOptimization = "CleanRoom"
            },

            // Sartorius Standard Template
            new()
            {
                TemplateName = "sartorius_standard",
                DisplayName = "Sartorius Standard Protocol",
                Manufacturer = "Sartorius",
                Description = "Standard Sartorius scale protocol",
                Version = "1.0.0",
                Priority = 85,
                ConfidenceThreshold = 75.0,
                IsBuiltIn = true,
                CommunicationSettingsJson = JsonSerializer.Serialize(new
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = "None",
                    StopBits = 1,
                    FlowControl = "None"
                }),
                CommandTemplatesJson = JsonSerializer.Serialize(new
                {
                    RequestWeight = "P\r\n",
                    Tare = "T\r\n",
                    Zero = "Z\r\n"
                }),
                ResponsePatternsJson = JsonSerializer.Serialize(new
                {
                    WeightPattern = @"([\d\.-]+)\s*(\w*)",
                    StablePattern = @"[\d\.-]+\s*\w*\s*$",
                    ErrorPattern = @"Err"
                }),
                Author = "System",
                SupportedBaudRates = "9600,19200",
                EnvironmentalOptimization = "Laboratory"
            }
        };
    }
}