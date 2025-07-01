// Industrial.IoT.Platform.Core - Protocol Discovery Abstractions
// Interfaces for automatic protocol discovery following patterns from Python implementation

using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Core interface for protocol discovery engines enabling automatic detection of unknown device protocols
/// Based on proven Python implementation for ADAM-4571 scale discovery
/// </summary>
public interface IProtocolDiscovery
{
    /// <summary>
    /// Protocol discovery engine name/identifier
    /// </summary>
    string DiscoveryEngineName { get; }

    /// <summary>
    /// Supported transport types for this discovery engine
    /// </summary>
    IReadOnlyList<string> SupportedTransports { get; }

    /// <summary>
    /// Whether this discovery engine supports known template matching
    /// </summary>
    bool SupportsTemplateMatching { get; }

    /// <summary>
    /// Start a new protocol discovery session
    /// </summary>
    /// <param name="transport">Transport provider for device communication</param>
    /// <param name="configuration">Discovery-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token to abort discovery</param>
    /// <returns>Discovery session for tracking progress and state</returns>
    Task<IDiscoverySession> StartDiscoveryAsync(
        ITransportProvider transport, 
        IDiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Try known protocol templates first before interactive discovery
    /// </summary>
    /// <param name="transport">Transport provider for device communication</param>
    /// <param name="templates">Available protocol templates to test</param>
    /// <param name="confidenceThreshold">Minimum confidence score (0-100) to accept template</param>
    /// <param name="cancellationToken">Cancellation token to abort template testing</param>
    /// <returns>Template matching result with confidence score</returns>
    Task<TemplateMatchResult> TryKnownTemplatesAsync(
        ITransportProvider transport,
        IReadOnlyList<IProtocolTemplate> templates,
        double confidenceThreshold = 85.0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a discovery step with user input (for interactive discovery)
    /// </summary>
    /// <param name="session">Active discovery session</param>
    /// <param name="userInput">User-provided input (e.g., known weight value)</param>
    /// <param name="streamData">Captured data from device during this step</param>
    /// <param name="cancellationToken">Cancellation token to abort processing</param>
    /// <returns>Discovery result with confidence score and next step guidance</returns>
    Task<DiscoveryStepResult> ProcessDiscoveryStepAsync(
        IDiscoverySession session,
        string userInput,
        IReadOnlyList<byte[]> streamData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete the discovery session and generate final protocol template
    /// </summary>
    /// <param name="session">Active discovery session to finalize</param>
    /// <param name="cancellationToken">Cancellation token to abort finalization</param>
    /// <returns>Final protocol template with confidence assessment</returns>
    Task<IProtocolTemplate> FinalizeDiscoveryAsync(
        IDiscoverySession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a protocol template against live device data
    /// </summary>
    /// <param name="template">Protocol template to validate</param>
    /// <param name="transport">Transport provider for device communication</param>
    /// <param name="testDuration">Duration to capture test data</param>
    /// <param name="cancellationToken">Cancellation token to abort validation</param>
    /// <returns>Validation result with confidence score</returns>
    Task<TemplateValidationResult> ValidateTemplateAsync(
        IProtocolTemplate template,
        ITransportProvider transport,
        TimeSpan testDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when discovery progress is updated
    /// </summary>
    event EventHandler<DiscoveryProgressEventArgs> ProgressUpdated;

    /// <summary>
    /// Event raised when real-time confidence scores are calculated
    /// </summary>
    event EventHandler<ConfidenceUpdatedEventArgs> ConfidenceUpdated;
}

/// <summary>
/// Configuration for protocol discovery operations
/// </summary>
public interface IDiscoveryConfiguration
{
    /// <summary>
    /// Minimum confidence threshold to accept discovery results (0-100)
    /// </summary>
    double ConfidenceThreshold { get; }

    /// <summary>
    /// Maximum number of discovery iterations before giving up
    /// </summary>
    int MaxIterations { get; }

    /// <summary>
    /// Timeout for capturing data frames from device
    /// </summary>
    TimeSpan FrameTimeout { get; }

    /// <summary>
    /// Minimum number of data samples required for analysis
    /// </summary>
    int MinSamples { get; }

    /// <summary>
    /// Window size for stability detection (number of consecutive stable readings)
    /// </summary>
    int StabilityWindow { get; }

    /// <summary>
    /// Device-specific discovery parameters
    /// </summary>
    IReadOnlyDictionary<string, object> Parameters { get; }
}

/// <summary>
/// Active discovery session tracking state and progress
/// </summary>
public interface IDiscoverySession
{
    /// <summary>
    /// Unique identifier for this discovery session
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Timestamp when discovery session was started
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Current step number in the discovery process
    /// </summary>
    int CurrentStep { get; }

    /// <summary>
    /// Current overall confidence score (0-100)
    /// </summary>
    double CurrentConfidence { get; }

    /// <summary>
    /// Current session state
    /// </summary>
    DiscoverySessionState State { get; }

    /// <summary>
    /// Collected discovery steps with user input and captured data
    /// </summary>
    IReadOnlyList<DiscoveryStep> Steps { get; }

    /// <summary>
    /// Any error that occurred during discovery
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Dispose of session resources
    /// </summary>
    void Dispose();
}

/// <summary>
/// Discovery session states
/// </summary>
public enum DiscoverySessionState
{
    /// <summary>
    /// Session is active and capturing data
    /// </summary>
    Active = 0,

    /// <summary>
    /// Session completed successfully with valid protocol
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Session failed due to error or timeout
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Session was cancelled by user
    /// </summary>
    Cancelled = 3
}