// Industrial.Adam.ScaleLogger - Weighing Transaction Entity
// Core entity for static weighing operations following industrial patterns

using Industrial.Adam.ScaleLogger.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Industrial.Adam.ScaleLogger.Data.Entities;

/// <summary>
/// Entity representing a single weighing transaction
/// Optimized for static weighing operations (item placed → weighed → recorded)
/// </summary>
[Table("WeighingTransactions")]
public sealed class WeighingTransaction
{
    /// <summary>
    /// Primary key (auto-increment)
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Unique transaction identifier for external references
    /// </summary>
    [Required]
    public Guid TransactionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Scale device identifier
    /// </summary>
    [Required]
    [StringLength(50)]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Human-readable device name
    /// </summary>
    [StringLength(100)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// Scale channel number
    /// </summary>
    [Required]
    public int Channel { get; set; }

    /// <summary>
    /// Weight value as measured
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal WeightValue { get; set; }

    /// <summary>
    /// Weight unit (kg, lb, oz, etc.)
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Unit { get; set; } = "kg";

    /// <summary>
    /// Whether the weight reading was stable
    /// </summary>
    [Required]
    public bool IsStable { get; set; }

    /// <summary>
    /// Data quality assessment
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Quality { get; set; } = DataQuality.Good.ToString();

    /// <summary>
    /// When the weighing occurred
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Operator who performed the weighing
    /// </summary>
    [StringLength(50)]
    public string? OperatorId { get; set; }

    /// <summary>
    /// Product being weighed
    /// </summary>
    [StringLength(100)]
    public string? ProductCode { get; set; }

    /// <summary>
    /// Batch or lot number
    /// </summary>
    [StringLength(100)]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Work order or job number
    /// </summary>
    [StringLength(100)]
    public string? WorkOrder { get; set; }

    /// <summary>
    /// Physical location of the scale
    /// </summary>
    [StringLength(100)]
    public string? Location { get; set; }

    /// <summary>
    /// Raw response from the scale device
    /// </summary>
    public string? RawValue { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    [Column(TypeName = "jsonb")] // PostgreSQL JSONB, falls back to TEXT for SQLite
    public string? Metadata { get; set; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to device
    /// </summary>
    public ScaleDevice? Device { get; set; }
}

/// <summary>
/// Entity representing scale device configuration and status
/// </summary>
[Table("ScaleDevices")]
public sealed class ScaleDevice
{
    /// <summary>
    /// Device identifier (primary key)
    /// </summary>
    [Key]
    [StringLength(50)]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Human-readable device name
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// Physical location
    /// </summary>
    [StringLength(100)]
    public string? Location { get; set; }

    /// <summary>
    /// Device manufacturer
    /// </summary>
    [StringLength(100)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [StringLength(100)]
    public string? Model { get; set; }

    /// <summary>
    /// Whether the device is currently active
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Device configuration as JSON
    /// </summary>
    [Column(TypeName = "jsonb")] // PostgreSQL JSONB, falls back to TEXT for SQLite
    public string? Configuration { get; set; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to weighing transactions
    /// </summary>
    public ICollection<WeighingTransaction> WeighingTransactions { get; set; } = new List<WeighingTransaction>();
}

/// <summary>
/// Entity for tracking system events and operational logs
/// </summary>
[Table("SystemEvents")]
public sealed class SystemEvent
{
    /// <summary>
    /// Primary key (auto-increment)
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Event identifier for correlation
    /// </summary>
    [Required]
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Event type (DeviceConnect, DeviceDisconnect, WeighingError, etc.)
    /// </summary>
    [Required]
    [StringLength(50)]
    public required string EventType { get; set; }

    /// <summary>
    /// Device associated with the event
    /// </summary>
    [StringLength(50)]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Event severity level
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Severity { get; set; } = "Information";

    /// <summary>
    /// Event message
    /// </summary>
    [Required]
    [StringLength(500)]
    public required string Message { get; set; }

    /// <summary>
    /// Additional event details as JSON
    /// </summary>
    [Column(TypeName = "jsonb")] // PostgreSQL JSONB, falls back to TEXT for SQLite
    public string? Details { get; set; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to device
    /// </summary>
    public ScaleDevice? Device { get; set; }
}