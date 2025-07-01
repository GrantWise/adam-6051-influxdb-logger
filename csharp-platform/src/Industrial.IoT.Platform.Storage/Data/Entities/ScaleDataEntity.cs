// Industrial.IoT.Platform.Storage - Scale Data Entity
// Entity Framework model for discrete scale data storage following existing ADAM logger patterns

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Industrial.IoT.Platform.Storage.Data.Entities;

/// <summary>
/// Entity Framework model for scale data readings from ADAM-4571 devices
/// Follows existing ADAM logger data model patterns for consistency
/// </summary>
[Table("ScaleData")]
// Indexes configured in DbContext
public sealed class ScaleDataEntity
{
    /// <summary>
    /// Primary key for the scale data record
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Unique identifier for the source ADAM-4571 device
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Serial port channel on the ADAM-4571 device (typically 1-8)
    /// </summary>
    [Range(1, 8)]
    public int Channel { get; set; }

    /// <summary>
    /// Timestamp when the scale reading was acquired (UTC)
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Weight measurement in kilograms
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal WeightKg { get; set; }

    /// <summary>
    /// Raw weight value as received from the scale device
    /// </summary>
    [MaxLength(100)]
    public string? RawWeight { get; set; }

    /// <summary>
    /// Scale unit of measurement (kg, lb, g, oz, etc.)
    /// </summary>
    [MaxLength(10)]
    public string? Unit { get; set; }

    /// <summary>
    /// Scale status flags (stable, unstable, overload, underload, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? Status { get; set; }

    /// <summary>
    /// Data quality assessment following ADAM logger patterns
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Quality { get; set; } = "Good";

    /// <summary>
    /// Time taken to acquire the reading from the device
    /// </summary>
    public TimeSpan AcquisitionTime { get; set; }

    /// <summary>
    /// Signal stability score (0-100) for RS232 connection quality
    /// </summary>
    [Range(0, 100)]
    public double? StabilityScore { get; set; }

    /// <summary>
    /// Error message if the reading failed or has issues
    /// </summary>
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Scale manufacturer (Mettler Toledo, Sartorius, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Scale model identifier
    /// </summary>
    [MaxLength(50)]
    public string? Model { get; set; }

    /// <summary>
    /// Serial number of the connected scale
    /// </summary>
    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Protocol template used for communication
    /// </summary>
    [MaxLength(50)]
    public string? ProtocolTemplate { get; set; }

    /// <summary>
    /// Additional metadata as JSON for extensibility
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Record creation timestamp (for auditing)
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Record last modified timestamp (for auditing)
    /// </summary>
    [Required]
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Calculated weight in standardized unit (always kg for consistency)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    [NotMapped] // Calculated property, not stored
    public decimal StandardizedWeightKg => 
        Unit?.ToUpperInvariant() switch
        {
            "LB" or "LBS" => WeightKg * 0.453592m,
            "G" or "GRAM" or "GRAMS" => WeightKg / 1000m,
            "OZ" or "OUNCE" or "OUNCES" => WeightKg * 0.0283495m,
            "T" or "TON" or "TONS" => WeightKg * 1000m,
            _ => WeightKg // Assume kg if unknown
        };

    /// <summary>
    /// Indicates if this is a stable reading based on scale status
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool IsStable => 
        Status?.ToUpperInvariant().Contains("STABLE") == true ||
        Status?.ToUpperInvariant().Contains("ST") == true ||
        (!string.IsNullOrEmpty(Status) && !Status.ToUpperInvariant().Contains("UNSTABLE"));

    /// <summary>
    /// Indicates if the reading represents an error condition
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool IsError => 
        Quality != "Good" || 
        !string.IsNullOrEmpty(ErrorMessage) ||
        Status?.ToUpperInvariant().Contains("ERROR") == true ||
        Status?.ToUpperInvariant().Contains("OVERLOAD") == true ||
        Status?.ToUpperInvariant().Contains("UNDERLOAD") == true;
}