// Industrial.Adam.ScaleLogger - Database Context
// Entity Framework Core context for PostgreSQL and SQLite support

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Industrial.Adam.ScaleLogger.Data;

/// <summary>
/// Entity Framework Core database context for scale logger
/// Supports both PostgreSQL (production) and SQLite (development/single-scale)
/// </summary>
public sealed class ScaleLoggerDbContext : DbContext
{
    private readonly DatabaseConfig _config;

    public ScaleLoggerDbContext(DbContextOptions<ScaleLoggerDbContext> options, IOptions<DatabaseConfig> config)
        : base(options)
    {
        _config = config.Value;
    }

    /// <summary>
    /// Weighing transactions table
    /// </summary>
    public DbSet<WeighingTransaction> WeighingTransactions { get; set; } = null!;

    /// <summary>
    /// Scale devices table
    /// </summary>
    public DbSet<ScaleDevice> ScaleDevices { get; set; } = null!;

    /// <summary>
    /// System events table
    /// </summary>
    public DbSet<SystemEvent> SystemEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureWeighingTransactions(modelBuilder);
        ConfigureScaleDevices(modelBuilder);
        ConfigureSystemEvents(modelBuilder);
    }

    private static void ConfigureWeighingTransactions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WeighingTransaction>();

        // Primary key
        entity.HasKey(e => e.Id);

        // Unique constraints
        entity.HasIndex(e => e.TransactionId).IsUnique();

        // Performance indexes
        entity.HasIndex(e => e.Timestamp);
        entity.HasIndex(e => e.DeviceId);
        entity.HasIndex(e => e.ProductCode);
        entity.HasIndex(e => e.BatchNumber);
        entity.HasIndex(e => e.WorkOrder);
        entity.HasIndex(e => new { e.DeviceId, e.Timestamp });

        // Foreign key relationship
        entity.HasOne(e => e.Device)
              .WithMany(d => d.WeighingTransactions)
              .HasForeignKey(e => e.DeviceId)
              .OnDelete(DeleteBehavior.Restrict); // Don't delete transactions when device is removed

        // JSON metadata conversion
        entity.Property(e => e.Metadata)
              .HasConversion(
                  v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                  v => v == null ? null : JsonSerializer.Deserialize<string>(v, (JsonSerializerOptions?)null));

        // Default values
        entity.Property(e => e.TransactionId).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        entity.Property(e => e.Timestamp).HasDefaultValueSql("NOW()");
    }

    private static void ConfigureScaleDevices(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ScaleDevice>();

        // Primary key
        entity.HasKey(e => e.DeviceId);

        // Indexes
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.Location);
        entity.HasIndex(e => e.IsActive);

        // JSON configuration conversion
        entity.Property(e => e.Configuration)
              .HasConversion(
                  v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                  v => v == null ? null : JsonSerializer.Deserialize<string>(v, (JsonSerializerOptions?)null));

        // Default values
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
    }

    private static void ConfigureSystemEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SystemEvent>();

        // Primary key
        entity.HasKey(e => e.Id);

        // Unique constraints
        entity.HasIndex(e => e.EventId).IsUnique();

        // Performance indexes
        entity.HasIndex(e => e.Timestamp);
        entity.HasIndex(e => e.EventType);
        entity.HasIndex(e => e.DeviceId);
        entity.HasIndex(e => e.Severity);
        entity.HasIndex(e => new { e.EventType, e.Timestamp });

        // Foreign key relationship (optional)
        entity.HasOne(e => e.Device)
              .WithMany()
              .HasForeignKey(e => e.DeviceId)
              .OnDelete(DeleteBehavior.SetNull); // Keep events when device is removed

        // JSON details conversion
        entity.Property(e => e.Details)
              .HasConversion(
                  v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                  v => v == null ? null : JsonSerializer.Deserialize<string>(v, (JsonSerializerOptions?)null));

        // Default values
        entity.Property(e => e.EventId).HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL
        entity.Property(e => e.Timestamp).HasDefaultValueSql("NOW()");
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<ScaleDevice>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}