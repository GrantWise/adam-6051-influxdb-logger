// Industrial.IoT.Platform.Protocols - Ground Truth Correlation Result
// Result model for ground truth correlation analysis in interactive discovery

namespace Industrial.IoT.Platform.Protocols.Models;

/// <summary>
/// Result of ground truth correlation analysis
/// Provides detailed metrics on how well discovered data correlates with expected values
/// </summary>
public sealed record GroundTruthCorrelationResult
{
    /// <summary>
    /// Overall correlation score (0-100%)
    /// Weighted combination of all correlation metrics
    /// </summary>
    public required double OverallCorrelation { get; init; }

    /// <summary>
    /// Weight accuracy score (0-100%)
    /// How closely extracted weights match expected values
    /// </summary>
    public required double WeightAccuracy { get; init; }

    /// <summary>
    /// Timing reliability score (0-100%)
    /// Consistency of data capture timing and volume
    /// </summary>
    public required double TimingReliability { get; init; }

    /// <summary>
    /// Data consistency score (0-100%)
    /// Format and structure consistency across captured data
    /// </summary>
    public required double DataConsistency { get; init; }

    /// <summary>
    /// Number of discovery steps analyzed
    /// </summary>
    public required int AnalyzedSteps { get; init; }

    /// <summary>
    /// Recommended action based on correlation analysis
    /// </summary>
    public required string RecommendedAction { get; init; }

    /// <summary>
    /// Detailed correlation breakdown by step
    /// </summary>
    public IReadOnlyDictionary<string, double>? StepCorrelations { get; init; }

    /// <summary>
    /// Quality assessment based on correlation scores
    /// </summary>
    public CorrelationQuality Quality => OverallCorrelation switch
    {
        >= 90 => CorrelationQuality.Excellent,
        >= 80 => CorrelationQuality.Good,
        >= 70 => CorrelationQuality.Acceptable,
        >= 50 => CorrelationQuality.Poor,
        _ => CorrelationQuality.Unreliable
    };

    /// <summary>
    /// Whether correlation is sufficient for template generation
    /// </summary>
    public bool IsSufficientForTemplateGeneration => OverallCorrelation >= 70.0;

    /// <summary>
    /// Confidence level for generated template (if applicable)
    /// </summary>
    public double TemplateConfidence => Math.Min(100, OverallCorrelation * 1.1); // Slight boost for validated data
}

/// <summary>
/// Quality levels for correlation analysis
/// </summary>
public enum CorrelationQuality
{
    /// <summary>
    /// Unreliable correlation (< 50%)
    /// </summary>
    Unreliable = 0,

    /// <summary>
    /// Poor correlation (50-70%)
    /// </summary>
    Poor = 1,

    /// <summary>
    /// Acceptable correlation (70-80%)
    /// </summary>
    Acceptable = 2,

    /// <summary>
    /// Good correlation (80-90%)
    /// </summary>
    Good = 3,

    /// <summary>
    /// Excellent correlation (90%+)
    /// </summary>
    Excellent = 4
}