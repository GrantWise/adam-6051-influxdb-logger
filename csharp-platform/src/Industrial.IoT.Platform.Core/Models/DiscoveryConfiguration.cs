// Industrial.IoT.Platform.Core - Protocol Discovery Configuration
// Configuration model for protocol discovery operations

using System.ComponentModel.DataAnnotations;

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Configuration for protocol discovery operations
/// </summary>
public class DiscoveryConfiguration
{
    /// <summary>
    /// Confidence threshold for automatic protocol selection (0-100)
    /// </summary>
    [Range(50.0, 99.0, ErrorMessage = "ConfidenceThreshold must be between 50% and 99%")]
    public double ConfidenceThreshold { get; set; } = Constants.DefaultConfidenceThreshold;

    /// <summary>
    /// Timeout for discovery operations in seconds
    /// </summary>
    [Range(10, 300, ErrorMessage = "TimeoutSeconds must be between 10 seconds and 5 minutes")]
    public int TimeoutSeconds { get; set; } = Constants.DefaultDiscoveryTimeoutSeconds;

    /// <summary>
    /// Maximum number of discovery iterations
    /// </summary>
    [Range(1, 20, ErrorMessage = "MaxIterations must be between 1 and 20")]
    public int MaxIterations { get; set; } = Constants.DefaultMaxDiscoveryIterations;

    /// <summary>
    /// Enable verbose logging during discovery
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Baseline capture timeout in milliseconds
    /// </summary>
    [Range(1000, 300000)]
    public int BaselineCaptureTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Minimum frames required for analysis
    /// </summary>
    [Range(5, 1000)]
    public int MinimumFramesForAnalysis { get; set; } = 10;

    /// <summary>
    /// Maximum buffered frames during discovery
    /// </summary>
    [Range(50, 10000)]
    public int MaxBufferedFrames { get; set; } = 1000;

    /// <summary>
    /// Additional parameters for discovery algorithm
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}