// Industrial.IoT.Platform.Protocols - Discovery Models
// Additional models for protocol discovery functionality

using System.ComponentModel.DataAnnotations;

namespace Industrial.IoT.Platform.Protocols.Models;

/// <summary>
/// Configuration for interactive discovery guidance
/// </summary>
public sealed record InteractiveGuidance
{
    /// <summary>
    /// Minimum number of interactive steps required
    /// </summary>
    [Range(3, 20)]
    public int MinimumSteps { get; init; } = 5;

    /// <summary>
    /// Discovery steps to execute
    /// </summary>
    public required IReadOnlyList<StepGuidance> Steps { get; init; }

    /// <summary>
    /// Maximum time per step in milliseconds
    /// </summary>
    [Range(5000, 60000)]
    public int MaxStepTimeMs { get; init; } = 15000;

    /// <summary>
    /// Enable automatic step validation
    /// </summary>
    public bool EnableAutoValidation { get; init; } = true;
}

/// <summary>
/// Guidance for individual discovery step
/// </summary>
public sealed record StepGuidance
{
    /// <summary>
    /// Action to perform in this step
    /// </summary>
    public required StepAction Action { get; init; }

    /// <summary>
    /// Expected weight for this step (if applicable)
    /// </summary>
    public double? ExpectedWeight { get; init; }

    /// <summary>
    /// Data capture time for this step in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int CaptureTimeMs { get; init; } = 5000;

    /// <summary>
    /// Instructions for the operator
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Whether this step is optional
    /// </summary>
    public bool IsOptional { get; init; } = false;
}

/// <summary>
/// Result of protocol discovery session
/// </summary>
public sealed record DiscoveryResult
{
    /// <summary>
    /// Discovery session identifier
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Whether discovery was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Best matching template found
    /// </summary>
    public ProtocolTemplate? BestTemplate { get; init; }

    /// <summary>
    /// Final confidence score
    /// </summary>
    [Range(0.0, 100.0)]
    public required double Confidence { get; init; }

    /// <summary>
    /// Total discovery duration
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of frames captured during discovery
    /// </summary>
    public required int CapturedFrames { get; init; }

    /// <summary>
    /// Number of templates tested
    /// </summary>
    public required int TestedTemplates { get; init; }

    /// <summary>
    /// Number of interactive steps completed
    /// </summary>
    public required int InteractiveSteps { get; init; }

    /// <summary>
    /// Error message if discovery failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata about the discovery process
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}