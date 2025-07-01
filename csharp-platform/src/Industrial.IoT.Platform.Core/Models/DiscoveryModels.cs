// Industrial.IoT.Platform.Core - Protocol Discovery Models
// Data models for protocol discovery operations following existing pattern quality

using Industrial.IoT.Platform.Core.Interfaces;

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Represents a single step in the protocol discovery process
/// </summary>
public sealed record DiscoveryStep
{
    /// <summary>
    /// Step number in the discovery sequence
    /// </summary>
    public required int StepNumber { get; init; }

    /// <summary>
    /// Type of action performed in this step
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// User-provided input for this step (e.g., known weight value)
    /// </summary>
    public string? UserInput { get; init; }

    /// <summary>
    /// Expected value for correlation analysis
    /// </summary>
    public double? ExpectedValue { get; init; }

    /// <summary>
    /// Raw data captured from device during this step
    /// </summary>
    public required IReadOnlyList<byte[]> CapturedData { get; init; }

    /// <summary>
    /// Parsed string representations of captured data
    /// </summary>
    public required IReadOnlyList<string> ParsedData { get; init; }

    /// <summary>
    /// Timestamp when this step was executed
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Duration taken to complete this step
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Confidence score calculated for this step (0-100)
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Any error encountered during this step
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of processing a discovery step
/// </summary>
public sealed record DiscoveryStepResult
{
    /// <summary>
    /// Whether the step was processed successfully
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Updated confidence score after processing this step (0-100)
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Whether discovery can be completed with current confidence
    /// </summary>
    public required bool CanComplete { get; init; }

    /// <summary>
    /// Guidance for the next step in discovery process
    /// </summary>
    public string? NextStepGuidance { get; init; }

    /// <summary>
    /// Detected patterns or insights from this step
    /// </summary>
    public IReadOnlyList<string> DetectedPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Error message if step processing failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Result of template matching against device data
/// </summary>
public sealed record TemplateMatchResult
{
    /// <summary>
    /// Whether template matching was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Confidence score for the template match (0-100)
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Template that was tested
    /// </summary>
    public required IProtocolTemplate Template { get; init; }

    /// <summary>
    /// Number of successful parse attempts
    /// </summary>
    public int SuccessfulParses { get; init; }

    /// <summary>
    /// Total number of parse attempts
    /// </summary>
    public int TotalAttempts { get; init; }

    /// <summary>
    /// Parse success rate as percentage
    /// </summary>
    public double ParseSuccessRate => TotalAttempts > 0 ? (double)SuccessfulParses / TotalAttempts * 100 : 0;

    /// <summary>
    /// Detected data consistency score
    /// </summary>
    public double DataConsistency { get; init; }

    /// <summary>
    /// Format characteristics match score
    /// </summary>
    public double FormatMatch { get; init; }

    /// <summary>
    /// Sample parsed values demonstrating template accuracy
    /// </summary>
    public IReadOnlyList<object> SampleValues { get; init; } = Array.Empty<object>();

    /// <summary>
    /// Parsing errors encountered during template testing
    /// </summary>
    public IReadOnlyList<string> ParsingErrors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Error message if template matching failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of validating a protocol template against live data
/// </summary>
public sealed record TemplateValidationResult
{
    /// <summary>
    /// Whether template validation was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Overall validation confidence score (0-100)
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Template that was validated
    /// </summary>
    public required IProtocolTemplate Template { get; init; }

    /// <summary>
    /// Duration of validation test
    /// </summary>
    public required TimeSpan TestDuration { get; init; }

    /// <summary>
    /// Number of data samples processed during validation
    /// </summary>
    public int SamplesProcessed { get; init; }

    /// <summary>
    /// Number of samples that parsed successfully
    /// </summary>
    public int SuccessfulParses { get; init; }

    /// <summary>
    /// Parse success rate as percentage
    /// </summary>
    public double ParseSuccessRate => SamplesProcessed > 0 ? (double)SuccessfulParses / SamplesProcessed * 100 : 0;

    /// <summary>
    /// Data quality assessment scores
    /// </summary>
    public ValidationMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Issues or warnings identified during validation
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Detailed metrics from template validation
/// </summary>
public sealed record ValidationMetrics
{
    /// <summary>
    /// Data consistency score (0-100)
    /// </summary>
    public double DataConsistency { get; init; }

    /// <summary>
    /// Format stability score (0-100)
    /// </summary>
    public double FormatStability { get; init; }

    /// <summary>
    /// Value range appropriateness score (0-100)
    /// </summary>
    public double ValueRangeScore { get; init; }

    /// <summary>
    /// Communication reliability score (0-100)
    /// </summary>
    public double CommunicationReliability { get; init; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; init; }
}

/// <summary>
/// Event arguments for discovery progress updates
/// </summary>
public sealed class DiscoveryProgressEventArgs : EventArgs
{
    /// <summary>
    /// Discovery session identifier
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Current step number
    /// </summary>
    public required int CurrentStep { get; init; }

    /// <summary>
    /// Total expected steps (if known)
    /// </summary>
    public int? TotalSteps { get; init; }

    /// <summary>
    /// Current confidence score (0-100)
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Progress message for user feedback
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Current session state
    /// </summary>
    public required DiscoverySessionState State { get; init; }
}

/// <summary>
/// Event arguments for confidence score updates
/// </summary>
public sealed class ConfidenceUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Discovery session identifier
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Previous confidence score
    /// </summary>
    public double PreviousConfidence { get; init; }

    /// <summary>
    /// Current confidence score (0-100)
    /// </summary>
    public required double CurrentConfidence { get; init; }

    /// <summary>
    /// Factors contributing to confidence calculation
    /// </summary>
    public IReadOnlyDictionary<string, double> ConfidenceFactors { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Whether confidence threshold has been reached
    /// </summary>
    public bool ThresholdReached { get; init; }
}