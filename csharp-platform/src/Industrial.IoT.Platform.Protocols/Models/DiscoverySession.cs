// Industrial.IoT.Platform.Protocols - Discovery Session Models
// Enhanced session management for protocol discovery with robust state tracking

using System.Collections.Concurrent;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Protocols.Models;

/// <summary>
/// Represents an active protocol discovery session
/// Enhanced version of Python discovery workflow with better state management
/// </summary>
public sealed class DiscoverySession : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentQueue<DataFrame> _capturedFrames = new();
    private readonly object _stateLock = new();
    private volatile bool _isDisposed;

    /// <summary>
    /// Unique session identifier
    /// </summary>
    public Guid SessionId { get; } = Guid.NewGuid();

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Current discovery phase
    /// </summary>
    public DiscoveryPhase Phase { get; private set; } = DiscoveryPhase.Initializing;

    /// <summary>
    /// Discovery configuration
    /// </summary>
    public DiscoveryConfiguration Configuration { get; }

    /// <summary>
    /// Transport provider for data capture
    /// </summary>
    public ITransportProvider Transport { get; }

    /// <summary>
    /// Current discovery step (for interactive discovery)
    /// </summary>
    public int CurrentStep { get; private set; }

    /// <summary>
    /// Discovery steps performed so far
    /// </summary>
    public IReadOnlyList<DiscoveryStep> Steps => _steps.AsReadOnly();
    private readonly List<DiscoveryStep> _steps = new();

    /// <summary>
    /// Template test results
    /// </summary>
    public IReadOnlyList<TemplateTestResult> TemplateResults => _templateResults.AsReadOnly();
    private readonly List<TemplateTestResult> _templateResults = new();

    /// <summary>
    /// Best matching template found so far
    /// </summary>
    public ProtocolTemplate? BestTemplate { get; private set; }

    /// <summary>
    /// Current confidence score of best template
    /// </summary>
    public double BestConfidence { get; private set; }

    /// <summary>
    /// Session cancellation token
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// Whether session is active
    /// </summary>
    public bool IsActive => !_isDisposed && !CancellationToken.IsCancellationRequested;

    /// <summary>
    /// Total captured data frames
    /// </summary>
    public int CapturedFrameCount => _capturedFrames.Count;

    /// <summary>
    /// Session duration so far
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - CreatedAt;

    /// <summary>
    /// Initialize discovery session
    /// </summary>
    /// <param name="transport">Transport provider for data capture</param>
    /// <param name="configuration">Discovery configuration</param>
    public DiscoverySession(ITransportProvider transport, DiscoveryConfiguration configuration)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Add captured data frame to session
    /// Thread-safe operation for concurrent data capture
    /// </summary>
    /// <param name="data">Raw data bytes</param>
    /// <param name="timestamp">Capture timestamp</param>
    public void AddCapturedFrame(byte[] data, DateTime timestamp)
    {
        if (_isDisposed || data == null || data.Length == 0)
            return;

        var frame = new DataFrame(data, timestamp);
        _capturedFrames.Enqueue(frame);

        // Limit buffer size to prevent memory issues
        while (_capturedFrames.Count > Configuration.MaxBufferedFrames)
        {
            _capturedFrames.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Get captured frames for analysis
    /// Returns defensive copy to prevent modification
    /// </summary>
    /// <param name="maxFrames">Maximum number of frames to return</param>
    /// <returns>List of captured frames</returns>
    public IReadOnlyList<DataFrame> GetCapturedFrames(int maxFrames = int.MaxValue)
    {
        var frames = new List<DataFrame>();
        var frameArray = _capturedFrames.ToArray();
        
        var count = Math.Min(maxFrames, frameArray.Length);
        for (int i = Math.Max(0, frameArray.Length - count); i < frameArray.Length; i++)
        {
            frames.Add(frameArray[i]);
        }

        return frames;
    }

    /// <summary>
    /// Advance to next discovery phase
    /// Thread-safe state transition
    /// </summary>
    /// <param name="newPhase">New discovery phase</param>
    public void AdvancePhase(DiscoveryPhase newPhase)
    {
        lock (_stateLock)
        {
            if (_isDisposed)
                return;

            Phase = newPhase;
        }
    }

    /// <summary>
    /// Add template test result
    /// </summary>
    /// <param name="result">Template test result</param>
    public void AddTemplateResult(TemplateTestResult result)
    {
        if (_isDisposed || result == null)
            return;

        lock (_stateLock)
        {
            _templateResults.Add(result);
            
            // Update best template if this is better
            if (result.Confidence > BestConfidence)
            {
                BestTemplate = result.Template;
                BestConfidence = result.Confidence;
            }
        }
    }

    /// <summary>
    /// Add discovery step (for interactive discovery)
    /// </summary>
    /// <param name="step">Discovery step</param>
    public void AddStep(DiscoveryStep step)
    {
        if (_isDisposed || step == null)
            return;

        lock (_stateLock)
        {
            _steps.Add(step);
            CurrentStep = _steps.Count;
        }
    }

    /// <summary>
    /// Check if session meets success criteria
    /// </summary>
    /// <returns>True if discovery is successful</returns>
    public bool IsSuccessful()
    {
        return BestConfidence >= Configuration.ConfidenceThreshold;
    }

    /// <summary>
    /// Cancel the discovery session
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Dispose of session resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Discovery phase enumeration
/// Maps to Python discovery workflow phases
/// </summary>
public enum DiscoveryPhase
{
    /// <summary>
    /// Session initialization
    /// </summary>
    Initializing,

    /// <summary>
    /// Capturing baseline data
    /// </summary>
    CapturingData,

    /// <summary>
    /// Testing known templates
    /// </summary>
    TestingTemplates,

    /// <summary>
    /// Interactive discovery with user guidance
    /// </summary>
    InteractiveDiscovery,

    /// <summary>
    /// Generating new template
    /// </summary>
    GeneratingTemplate,

    /// <summary>
    /// Discovery completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Discovery failed or cancelled
    /// </summary>
    Failed
}

/// <summary>
/// Individual discovery step for interactive discovery
/// Direct port from Python DiscoveryStep with enhancements
/// </summary>
public sealed record DiscoveryStep
{
    /// <summary>
    /// Step number in sequence
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// Step action type
    /// </summary>
    public StepAction Action { get; init; }

    /// <summary>
    /// Expected weight for this step (if applicable)
    /// </summary>
    public double? ExpectedWeight { get; init; }

    /// <summary>
    /// Data frames captured during this step
    /// </summary>
    public IReadOnlyList<string> CapturedData { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Step timestamp
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Step completion status
    /// </summary>
    public StepStatus Status { get; init; } = StepStatus.Pending;

    /// <summary>
    /// Analysis results for this step
    /// </summary>
    public StepAnalysisResult? Analysis { get; init; }

    /// <summary>
    /// User instructions for this step
    /// </summary>
    public string? Instructions { get; init; }
}

/// <summary>
/// Step action types for interactive discovery
/// </summary>
public enum StepAction
{
    /// <summary>
    /// Capture baseline data with no weight
    /// </summary>
    Baseline,

    /// <summary>
    /// Add known weight to scale
    /// </summary>
    AddWeight,

    /// <summary>
    /// Remove weight from scale
    /// </summary>
    RemoveWeight,

    /// <summary>
    /// Verify current reading
    /// </summary>
    Verify,

    /// <summary>
    /// Test scale stability
    /// </summary>
    TestStability
}

/// <summary>
/// Step completion status
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// Step is pending execution
    /// </summary>
    Pending,

    /// <summary>
    /// Step is in progress
    /// </summary>
    InProgress,

    /// <summary>
    /// Step completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Step failed or was skipped
    /// </summary>
    Failed
}

/// <summary>
/// Analysis results for a discovery step
/// </summary>
public sealed record StepAnalysisResult
{
    /// <summary>
    /// Detected weight value
    /// </summary>
    public double? DetectedWeight { get; init; }

    /// <summary>
    /// Weight unit detected
    /// </summary>
    public string? DetectedUnit { get; init; }

    /// <summary>
    /// Stability assessment
    /// </summary>
    public bool IsStable { get; init; }

    /// <summary>
    /// Frame format consistency
    /// </summary>
    public double FormatConsistency { get; init; }

    /// <summary>
    /// Pattern recognition results
    /// </summary>
    public IReadOnlyList<string> DetectedPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Analysis confidence (0-100)
    /// </summary>
    public double Confidence { get; init; }
}

/// <summary>
/// Template testing result
/// Enhanced version of Python template validation
/// </summary>
public sealed record TemplateTestResult
{
    /// <summary>
    /// Template being tested
    /// </summary>
    public required ProtocolTemplate Template { get; init; }

    /// <summary>
    /// Test confidence score (0-100)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Validation statistics
    /// </summary>
    public TemplateValidationStats ValidationStats { get; init; } = new();

    /// <summary>
    /// Test duration
    /// </summary>
    public TimeSpan TestDuration { get; init; }

    /// <summary>
    /// Whether test was successful
    /// </summary>
    public bool IsSuccessful { get; init; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Sample parsed data from test
    /// </summary>
    public IReadOnlyList<ParsedFrame> SampleData { get; init; } = Array.Empty<ParsedFrame>();
}

/// <summary>
/// Raw data frame with timestamp
/// </summary>
public sealed record DataFrame
{
    /// <summary>
    /// Raw data bytes
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Capture timestamp
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Frame length
    /// </summary>
    public int Length => Data.Length;

    public DataFrame(byte[] data, DateTime timestamp)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Timestamp = timestamp;
    }

    /// <summary>
    /// Convert to string using specified encoding
    /// </summary>
    /// <param name="encoding">Text encoding to use</param>
    /// <returns>String representation</returns>
    public string ToString(System.Text.Encoding encoding)
    {
        try
        {
            return encoding.GetString(Data);
        }
        catch
        {
            // Fallback to ASCII on encoding errors
            return System.Text.Encoding.ASCII.GetString(Data);
        }
    }
}

/// <summary>
/// Parsed data frame with field values
/// </summary>
public sealed record ParsedFrame
{
    /// <summary>
    /// Original raw frame
    /// </summary>
    public required string RawFrame { get; init; }

    /// <summary>
    /// Parsed field values
    /// </summary>
    public required IReadOnlyDictionary<string, object?> FieldValues { get; init; }

    /// <summary>
    /// Parse timestamp
    /// </summary>
    public DateTime ParsedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether parsing was successful
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Parse errors if any
    /// </summary>
    public IReadOnlyList<string> ParseErrors { get; init; } = Array.Empty<string>();
}