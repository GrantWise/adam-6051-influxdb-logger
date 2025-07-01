// Industrial.IoT.Platform.Storage - Protocol Template Entity
// Entity Framework model for protocol template storage following existing ADAM logger patterns

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Industrial.IoT.Platform.Storage.Data.Entities;

/// <summary>
/// Entity Framework model for protocol template definitions
/// Stores communication templates for various scale manufacturers and models
/// </summary>
[Table("ProtocolTemplates")]
// Indexes configured in DbContext
public sealed class ProtocolTemplateEntity
{
    /// <summary>
    /// Primary key for the protocol template
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Unique template identifier (e.g., "mettler_toledo_standard")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the template
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Scale manufacturer (Mettler Toledo, Sartorius, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Specific model or model family this template supports
    /// </summary>
    [MaxLength(50)]
    public string? Model { get; set; }

    /// <summary>
    /// Protocol description and usage notes
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Template version for tracking updates and compatibility
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Communication settings as JSON (baud rate, parity, stop bits, etc.)
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string CommunicationSettingsJson { get; set; } = string.Empty;

    /// <summary>
    /// Command templates for requesting data from the scale
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string CommandTemplatesJson { get; set; } = string.Empty;

    /// <summary>
    /// Response parsing patterns and data extraction rules
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string ResponsePatternsJson { get; set; } = string.Empty;

    /// <summary>
    /// Regular expressions for validating scale responses
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ValidationRulesJson { get; set; }

    /// <summary>
    /// Error handling patterns specific to this protocol
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorHandlingJson { get; set; }

    /// <summary>
    /// Protocol-specific configuration parameters
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Template priority for auto-discovery (higher numbers tried first)
    /// </summary>
    [Range(1, 100)]
    public int Priority { get; set; } = 50;

    /// <summary>
    /// Confidence threshold for successful template matching (0-100)
    /// </summary>
    [Range(0, 100)]
    public double ConfidenceThreshold { get; set; } = 75.0;

    /// <summary>
    /// Maximum time to wait for scale response (milliseconds)
    /// </summary>
    [Range(100, 30000)]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Number of retry attempts for failed communications
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether this template is active and available for discovery
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this template is built-in (cannot be deleted)
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// Supported baud rates as comma-separated values
    /// </summary>
    [MaxLength(200)]
    public string? SupportedBaudRates { get; set; }

    /// <summary>
    /// Environmental optimization settings (CleanRoom, Factory, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? EnvironmentalOptimization { get; set; }

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? TagsJson { get; set; }

    /// <summary>
    /// Template author/maintainer information
    /// </summary>
    [MaxLength(100)]
    public string? Author { get; set; }

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
    /// Last time this template was successfully used
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Number of times this template has been successfully used
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Success rate of this template (0-100)
    /// </summary>
    [Range(0, 100)]
    public double SuccessRate { get; set; } = 0.0;

    /// <summary>
    /// Calculate the effective priority based on usage statistics
    /// Higher priority for frequently used, successful templates
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public double EffectivePriority => 
        Priority + (SuccessRate * 0.3) + Math.Min(Math.Log10(UsageCount + 1) * 10, 20);

    /// <summary>
    /// Indicates if the template has been tested and validated
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool IsValidated => UsageCount > 0 && SuccessRate >= ConfidenceThreshold;

    /// <summary>
    /// Age of the template in days
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public int AgeInDays => (int)(DateTimeOffset.UtcNow - CreatedAt).TotalDays;

    /// <summary>
    /// Indicates if the template is recently used (within last 30 days)
    /// </summary>
    [NotMapped] // Calculated property, not stored
    public bool IsRecentlyUsed => 
        LastUsedAt.HasValue && (DateTimeOffset.UtcNow - LastUsedAt.Value).TotalDays <= 30;
}