// Industrial.IoT.Platform.Core - Protocol Template Interface
// Interface for protocol template definitions following existing ADAM logger patterns

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Interface for protocol template definitions
/// Stores communication templates for various scale manufacturers and models
/// Following existing ADAM logger template patterns for consistency
/// </summary>
public interface IProtocolTemplate
{
    /// <summary>
    /// Unique template identifier (e.g., "mettler_toledo_standard")
    /// </summary>
    string TemplateName { get; }

    /// <summary>
    /// Human-readable display name for the template
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Scale manufacturer (Mettler Toledo, Sartorius, etc.)
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Specific model or model family this template supports
    /// </summary>
    string? Model { get; }

    /// <summary>
    /// Protocol description and usage notes
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Template version for tracking updates and compatibility
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Communication settings (baud rate, parity, stop bits, etc.)
    /// </summary>
    ICommunicationSettings CommunicationSettings { get; }

    /// <summary>
    /// Command templates for requesting data from the scale
    /// </summary>
    ICommandTemplates CommandTemplates { get; }

    /// <summary>
    /// Response parsing patterns and data extraction rules
    /// </summary>
    IResponsePatterns ResponsePatterns { get; }

    /// <summary>
    /// Validation rules for scale responses
    /// </summary>
    IValidationRules? ValidationRules { get; }

    /// <summary>
    /// Error handling patterns specific to this protocol
    /// </summary>
    IErrorHandling? ErrorHandling { get; }

    /// <summary>
    /// Protocol-specific configuration parameters
    /// </summary>
    IReadOnlyDictionary<string, object>? Configuration { get; }

    /// <summary>
    /// Template priority for auto-discovery (higher numbers tried first)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Confidence threshold for successful template matching (0-100)
    /// </summary>
    double ConfidenceThreshold { get; }

    /// <summary>
    /// Maximum time to wait for scale response (milliseconds)
    /// </summary>
    int TimeoutMs { get; }

    /// <summary>
    /// Number of retry attempts for failed communications
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Whether this template is active and available for discovery
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Whether this template is built-in (cannot be deleted)
    /// </summary>
    bool IsBuiltIn { get; }

    /// <summary>
    /// Supported baud rates
    /// </summary>
    IReadOnlyList<int>? SupportedBaudRates { get; }

    /// <summary>
    /// Environmental optimization settings (CleanRoom, Factory, etc.)
    /// </summary>
    string? EnvironmentalOptimization { get; }

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    IReadOnlyDictionary<string, object>? Tags { get; }

    /// <summary>
    /// Template author/maintainer information
    /// </summary>
    string? Author { get; }
}

/// <summary>
/// Interface for communication settings
/// </summary>
public interface ICommunicationSettings
{
    /// <summary>
    /// Baud rate for serial communication
    /// </summary>
    int BaudRate { get; }

    /// <summary>
    /// Data bits
    /// </summary>
    int DataBits { get; }

    /// <summary>
    /// Parity setting
    /// </summary>
    string Parity { get; }

    /// <summary>
    /// Stop bits
    /// </summary>
    int StopBits { get; }

    /// <summary>
    /// Flow control setting
    /// </summary>
    string FlowControl { get; }
}

/// <summary>
/// Interface for command templates
/// </summary>
public interface ICommandTemplates
{
    /// <summary>
    /// Request weight command
    /// </summary>
    string RequestWeight { get; }

    /// <summary>
    /// Additional commands (tare, zero, print, etc.)
    /// </summary>
    IReadOnlyDictionary<string, string>? AdditionalCommands { get; }
}

/// <summary>
/// Interface for response patterns
/// </summary>
public interface IResponsePatterns
{
    /// <summary>
    /// Weight pattern regular expression
    /// </summary>
    string WeightPattern { get; }

    /// <summary>
    /// Stable reading pattern
    /// </summary>
    string? StablePattern { get; }

    /// <summary>
    /// Unstable reading pattern
    /// </summary>
    string? UnstablePattern { get; }

    /// <summary>
    /// Additional patterns for error conditions, etc.
    /// </summary>
    IReadOnlyDictionary<string, string>? AdditionalPatterns { get; }
}

/// <summary>
/// Interface for validation rules
/// </summary>
public interface IValidationRules
{
    /// <summary>
    /// Minimum valid weight
    /// </summary>
    double? MinWeight { get; }

    /// <summary>
    /// Maximum valid weight
    /// </summary>
    double? MaxWeight { get; }

    /// <summary>
    /// Additional validation patterns
    /// </summary>
    IReadOnlyDictionary<string, string>? ValidationPatterns { get; }
}

/// <summary>
/// Interface for error handling
/// </summary>
public interface IErrorHandling
{
    /// <summary>
    /// Error patterns and their meanings
    /// </summary>
    IReadOnlyDictionary<string, string> ErrorPatterns { get; }

    /// <summary>
    /// Recovery commands for specific errors
    /// </summary>
    IReadOnlyDictionary<string, string>? RecoveryCommands { get; }
}