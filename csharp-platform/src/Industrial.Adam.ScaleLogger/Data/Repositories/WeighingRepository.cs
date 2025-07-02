// Industrial.Adam.ScaleLogger - Weighing Repository Implementation
// Entity Framework Core implementation for weighing data operations

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data.Entities;
using Industrial.Adam.ScaleLogger.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Industrial.Adam.ScaleLogger.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of weighing repository
/// Following proven ADAM-6051 repository patterns
/// </summary>
public sealed class WeighingRepository : IWeighingRepository
{
    private readonly ScaleLoggerDbContext _context;
    private readonly ILogger<WeighingRepository> _logger;

    public WeighingRepository(ScaleLoggerDbContext context, ILogger<WeighingRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WeighingTransaction> SaveWeighingAsync(ScaleDataReading reading, CancellationToken cancellationToken = default)
    {
        if (reading == null) throw new ArgumentNullException(nameof(reading));
        if (string.IsNullOrWhiteSpace(reading.DeviceId)) throw new ArgumentException("DeviceId cannot be null or empty", nameof(reading));

        // Ensure device exists (auto-create if needed for referential integrity)
        await EnsureDeviceExistsAsync(reading, cancellationToken);

        var transaction = new WeighingTransaction
        {
            DeviceId = reading.DeviceId,
            DeviceName = reading.DeviceName,
            Channel = reading.Channel,
            WeightValue = reading.StandardizedWeightKg,
            Unit = reading.Unit,
            IsStable = reading.IsStable,
            Quality = reading.Quality.ToString(),
            Timestamp = reading.Timestamp,
            RawValue = reading.RawValue,
            Metadata = reading.Metadata?.Any() == true ? JsonSerializer.Serialize(reading.Metadata) : null
        };

        _context.WeighingTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved weighing transaction {TransactionId} for device {DeviceId}: {Weight} {Unit}",
            transaction.TransactionId, reading.DeviceId, reading.StandardizedWeightKg, reading.Unit);

        return transaction;
    }

    public async Task<int> SaveWeighingsAsync(IEnumerable<ScaleDataReading> readings, CancellationToken cancellationToken = default)
    {
        if (readings == null) throw new ArgumentNullException(nameof(readings));

        var readingsList = readings.ToList();
        if (!readingsList.Any())
        {
            return 0;
        }

        // Ensure all devices exist for referential integrity
        foreach (var reading in readingsList)
        {
            if (string.IsNullOrWhiteSpace(reading.DeviceId)) 
                throw new ArgumentException($"DeviceId cannot be null or empty in reading", nameof(readings));
            
            await EnsureDeviceExistsAsync(reading, cancellationToken);
        }

        var transactions = readingsList.Select(reading => new WeighingTransaction
        {
            DeviceId = reading.DeviceId,
            DeviceName = reading.DeviceName,
            Channel = reading.Channel,
            WeightValue = reading.StandardizedWeightKg,
            Unit = reading.Unit,
            IsStable = reading.IsStable,
            Quality = reading.Quality.ToString(),
            Timestamp = reading.Timestamp,
            RawValue = reading.RawValue,
            Metadata = reading.Metadata?.Any() == true ? JsonSerializer.Serialize(reading.Metadata) : null
        }).ToList();

        _context.WeighingTransactions.AddRange(transactions);
        var savedCount = await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved {Count} weighing transactions", savedCount);

        return savedCount;
    }

    public async Task<IReadOnlyList<WeighingTransaction>> GetWeighingsAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) throw new ArgumentException("DeviceId cannot be null or empty", nameof(deviceId));

        // Simple parameterized query to avoid EF Core SQLite DateTimeOffset translation issues
        var sql = "SELECT * FROM WeighingTransactions WHERE DeviceId = {0}";
        var parameters = new List<object> { deviceId };

        if (from.HasValue)
        {
            sql += " AND Timestamp >= {" + parameters.Count + "}";
            parameters.Add(from.Value);
        }

        if (to.HasValue)
        {
            sql += " AND Timestamp <= {" + parameters.Count + "}";
            parameters.Add(to.Value);
        }

        sql += " ORDER BY Timestamp DESC LIMIT {" + parameters.Count + "}";
        parameters.Add(limit);

        return await _context.WeighingTransactions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeighingTransaction>> GetWeighingsByProductAsync(
        string productCode,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = @"
            SELECT * FROM WeighingTransactions 
            WHERE ProductCode = @p0";

        var parameters = new List<object> { productCode };
        var parameterIndex = 1;

        if (from.HasValue)
        {
            sql += $" AND Timestamp >= @p{parameterIndex}";
            parameters.Add(from.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        if (to.HasValue)
        {
            sql += $" AND Timestamp <= @p{parameterIndex}";
            parameters.Add(to.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        sql += " ORDER BY Timestamp DESC";
        
        if (limit > 0)
        {
            sql += $" LIMIT {limit}";
        }

        return await _context.WeighingTransactions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeighingTransaction>> GetWeighingsByBatchAsync(
        string batchNumber,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = @"
            SELECT * FROM WeighingTransactions 
            WHERE BatchNumber = @p0";

        var parameters = new List<object> { batchNumber };
        var parameterIndex = 1;

        if (from.HasValue)
        {
            sql += $" AND Timestamp >= @p{parameterIndex}";
            parameters.Add(from.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        if (to.HasValue)
        {
            sql += $" AND Timestamp <= @p{parameterIndex}";
            parameters.Add(to.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        sql += " ORDER BY Timestamp DESC";
        
        if (limit > 0)
        {
            sql += $" LIMIT {limit}";
        }

        return await _context.WeighingTransactions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeighingTransaction>> GetLatestWeighingsAsync(CancellationToken cancellationToken = default)
    {
        // Use raw SQL for complex query that EF Core struggles with
        // This is a pragmatic industrial solution that prioritizes reliability
        var sql = @"
            SELECT wt.*
            FROM WeighingTransactions wt
            INNER JOIN (
                SELECT DeviceId, MAX(Id) as LatestId
                FROM WeighingTransactions
                GROUP BY DeviceId
            ) latest ON wt.DeviceId = latest.DeviceId AND wt.Id = latest.LatestId";

        return await _context.WeighingTransactions
            .FromSqlRaw(sql)
            .ToListAsync(cancellationToken);
    }

    public async Task<WeighingStatistics> GetWeighingStatisticsAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = @"
            SELECT * FROM WeighingTransactions 
            WHERE DeviceId = @p0";

        var parameters = new List<object> { deviceId };
        var parameterIndex = 1;

        if (from.HasValue)
        {
            sql += $" AND Timestamp >= @p{parameterIndex}";
            parameters.Add(from.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        if (to.HasValue)
        {
            sql += $" AND Timestamp <= @p{parameterIndex}";
            parameters.Add(to.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        var weighings = await _context.WeighingTransactions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);

        if (!weighings.Any())
        {
            throw new InvalidOperationException($"No weighings found for device {deviceId}");
        }

        return new WeighingStatistics
        {
            DeviceId = deviceId,
            TotalWeighings = weighings.Count,
            MinWeight = weighings.Min(w => w.WeightValue),
            MaxWeight = weighings.Max(w => w.WeightValue),
            AverageWeight = weighings.Average(w => w.WeightValue),
            FirstWeighing = weighings.Min(w => w.Timestamp),
            LastWeighing = weighings.Max(w => w.Timestamp),
            StableWeighings = weighings.Count(w => w.IsStable),
            GoodQualityWeighings = weighings.Count(w => w.Quality == DataQuality.Good.ToString())
        };
    }

    public async Task<int> DeleteOldWeighingsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = "SELECT * FROM WeighingTransactions WHERE Timestamp < @p0";
        var parameters = new object[] { olderThan.ToString("yyyy-MM-dd HH:mm:ss.fffK") };

        var oldWeighings = await _context.WeighingTransactions
            .FromSqlRaw(sql, parameters)
            .ToListAsync(cancellationToken);

        if (!oldWeighings.Any())
        {
            return 0;
        }

        _context.WeighingTransactions.RemoveRange(oldWeighings);
        var deletedCount = await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} old weighing transactions older than {Date}",
            deletedCount, olderThan);

        return deletedCount;
    }

    /// <summary>
    /// Ensure device exists in database for referential integrity
    /// Following proven ADAM-6051 patterns for atomic operations
    /// </summary>
    private async Task EnsureDeviceExistsAsync(ScaleDataReading reading, CancellationToken cancellationToken)
    {
        var existingDevice = await _context.ScaleDevices
            .FirstOrDefaultAsync(d => d.DeviceId == reading.DeviceId, cancellationToken);

        if (existingDevice == null)
        {
            var device = new ScaleDevice
            {
                DeviceId = reading.DeviceId,
                Name = reading.DeviceName ?? reading.DeviceId,
                Location = reading.Metadata?.TryGetValue("location", out var location) == true ? location.ToString() : null,
                Manufacturer = reading.Manufacturer,
                Model = reading.Model,
                IsActive = true
            };

            _context.ScaleDevices.Add(device);
            _logger.LogDebug("Auto-created device {DeviceId} for weighing transaction", reading.DeviceId);
        }
    }
}

/// <summary>
/// Entity Framework Core implementation of device repository
/// </summary>
public sealed class DeviceRepository : IDeviceRepository
{
    private readonly ScaleLoggerDbContext _context;
    private readonly ILogger<DeviceRepository> _logger;

    public DeviceRepository(ScaleLoggerDbContext context, ILogger<DeviceRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScaleDevice> UpsertDeviceAsync(ScaleDeviceConfig deviceConfig, CancellationToken cancellationToken = default)
    {
        var existingDevice = await _context.ScaleDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceConfig.DeviceId, cancellationToken);

        if (existingDevice != null)
        {
            // Update existing device
            existingDevice.Name = deviceConfig.Name;
            existingDevice.Location = deviceConfig.Location;
            existingDevice.Manufacturer = deviceConfig.Manufacturer;
            existingDevice.Model = deviceConfig.Model;
            existingDevice.Configuration = JsonSerializer.Serialize(deviceConfig);
            existingDevice.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogDebug("Updated device configuration for {DeviceId}", deviceConfig.DeviceId);
        }
        else
        {
            // Create new device
            existingDevice = new ScaleDevice
            {
                DeviceId = deviceConfig.DeviceId,
                Name = deviceConfig.Name,
                Location = deviceConfig.Location,
                Manufacturer = deviceConfig.Manufacturer,
                Model = deviceConfig.Model,
                Configuration = JsonSerializer.Serialize(deviceConfig)
            };

            _context.ScaleDevices.Add(existingDevice);
            _logger.LogDebug("Created new device {DeviceId}", deviceConfig.DeviceId);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return existingDevice;
    }

    public async Task<ScaleDevice?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ScaleDevice>> GetActiveDevicesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScaleDevices
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeactivateDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _context.ScaleDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);

        if (device == null)
        {
            return false;
        }

        device.IsActive = false;
        device.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deactivated device {DeviceId}", deviceId);

        return true;
    }
}

/// <summary>
/// Entity Framework Core implementation of system event repository
/// </summary>
public sealed class SystemEventRepository : ISystemEventRepository
{
    private readonly ScaleLoggerDbContext _context;
    private readonly ILogger<SystemEventRepository> _logger;

    public SystemEventRepository(ScaleLoggerDbContext context, ILogger<SystemEventRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SystemEvent> LogEventAsync(
        string eventType,
        string message,
        string? deviceId = null,
        string severity = "Information",
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var systemEvent = new SystemEvent
        {
            EventType = eventType,
            Message = message,
            DeviceId = deviceId,
            Severity = severity,
            Details = details != null ? JsonSerializer.Serialize(details) : null
        };

        _context.SystemEvents.Add(systemEvent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Logged system event {EventType} for device {DeviceId}: {Message}",
            eventType, deviceId, message);

        return systemEvent;
    }

    public async Task<IReadOnlyList<SystemEvent>> GetEventsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? eventType = null,
        string? deviceId = null,
        string? severity = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = "SELECT * FROM SystemEvents WHERE 1=1";
        var parameters = new List<object>();
        var parameterIndex = 0;

        if (from.HasValue)
        {
            sql += $" AND Timestamp >= @p{parameterIndex}";
            parameters.Add(from.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        if (to.HasValue)
        {
            sql += $" AND Timestamp <= @p{parameterIndex}";
            parameters.Add(to.Value.ToString("yyyy-MM-dd HH:mm:ss.fffK"));
            parameterIndex++;
        }

        if (!string.IsNullOrEmpty(eventType))
        {
            sql += $" AND EventType = @p{parameterIndex}";
            parameters.Add(eventType);
            parameterIndex++;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            sql += $" AND DeviceId = @p{parameterIndex}";
            parameters.Add(deviceId);
            parameterIndex++;
        }

        if (!string.IsNullOrEmpty(severity))
        {
            sql += $" AND Severity = @p{parameterIndex}";
            parameters.Add(severity);
            parameterIndex++;
        }

        sql += " ORDER BY Timestamp DESC";
        
        if (limit > 0)
        {
            sql += $" LIMIT {limit}";
        }

        return await _context.SystemEvents
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteOldEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        // Use raw SQL for DateTimeOffset operations to avoid EF Core translation issues with SQLite
        var sql = "SELECT * FROM SystemEvents WHERE Timestamp < @p0";
        var parameters = new object[] { olderThan.ToString("yyyy-MM-dd HH:mm:ss.fffK") };

        var oldEvents = await _context.SystemEvents
            .FromSqlRaw(sql, parameters)
            .ToListAsync(cancellationToken);

        if (!oldEvents.Any())
        {
            return 0;
        }

        _context.SystemEvents.RemoveRange(oldEvents);
        var deletedCount = await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} old system events older than {Date}",
            deletedCount, olderThan);

        return deletedCount;
    }
}