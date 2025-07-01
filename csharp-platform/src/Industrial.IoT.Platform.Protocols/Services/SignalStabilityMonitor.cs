// Industrial.IoT.Platform.Protocols - Signal Stability Monitor
// Robust monitoring and handling of unstable RS232 signals in industrial environments
// Addresses common issues: bad grounding, poor shielding, loose connections

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Protocols.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Protocols.Services;

/// <summary>
/// Monitors RS232 signal stability and provides adaptive filtering for industrial environments
/// Handles common issues: electrical noise, connection problems, timing variations
/// </summary>
public sealed class SignalStabilityMonitor : IDisposable
{
    private readonly ILogger<SignalStabilityMonitor> _logger;
    private readonly Subject<SignalStabilityReport> _stabilityReports = new();
    private readonly ConcurrentQueue<SignalSample> _recentSamples = new();
    private readonly Timer _analysisTimer;
    private readonly SignalStabilityConfiguration _config;
    
    private volatile SignalStabilityState _currentState = SignalStabilityState.Unknown;
    private double _currentStabilityScore = 0.0;
    private volatile bool _isDisposed;

    // Statistical tracking
    private readonly object _statsLock = new();
    private SignalStatistics _statistics = new();

    /// <summary>
    /// Current signal stability state
    /// </summary>
    public SignalStabilityState CurrentState => _currentState;

    /// <summary>
    /// Current stability score (0-100)
    /// </summary>
    public double StabilityScore => _currentStabilityScore;

    /// <summary>
    /// Observable stream of stability reports
    /// </summary>
    public IObservable<SignalStabilityReport> StabilityReports => _stabilityReports.AsObservable();

    /// <summary>
    /// Initialize signal stability monitor
    /// </summary>
    /// <param name="logger">Logging service</param>
    /// <param name="config">Stability monitoring configuration</param>
    public SignalStabilityMonitor(
        ILogger<SignalStabilityMonitor> logger,
        SignalStabilityConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? SignalStabilityConfiguration.Default;

        // Start periodic analysis
        _analysisTimer = new Timer(PerformStabilityAnalysis, null, 
            TimeSpan.FromMilliseconds(_config.AnalysisIntervalMs),
            TimeSpan.FromMilliseconds(_config.AnalysisIntervalMs));

        _logger.LogInformation("Signal stability monitor initialized with {BufferSize} sample buffer", 
            _config.SampleBufferSize);
    }

    /// <summary>
    /// Add new signal sample for analysis
    /// Call this for every received data frame
    /// </summary>
    /// <param name="data">Raw signal data</param>
    /// <param name="timestamp">Sample timestamp</param>
    /// <param name="isValid">Whether the data appears valid</param>
    public void AddSample(byte[] data, DateTime timestamp, bool isValid = true)
    {
        if (_isDisposed || data == null) return;

        var sample = new SignalSample
        {
            Data = data,
            Timestamp = timestamp,
            IsValid = isValid,
            FrameLength = data.Length,
            HasNullBytes = data.Contains((byte)0),
            HasControlChars = HasControlCharacters(data),
            SignalStrength = CalculateSignalStrength(data)
        };

        _recentSamples.Enqueue(sample);

        // Maintain buffer size
        while (_recentSamples.Count > _config.SampleBufferSize)
        {
            _recentSamples.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Filter incoming data based on current stability state
    /// Returns filtered data or null if data should be rejected
    /// </summary>
    /// <param name="rawData">Raw input data</param>
    /// <returns>Filtered data or null if rejected</returns>
    public byte[]? FilterIncomingData(byte[] rawData)
    {
        if (_isDisposed || rawData == null || rawData.Length == 0)
            return null;

        switch (_currentState)
        {
            case SignalStabilityState.Stable:
                return rawData; // Pass through stable signals

            case SignalStabilityState.Noisy:
                return FilterNoisySignal(rawData);

            case SignalStabilityState.Intermittent:
                return FilterIntermittentSignal(rawData);

            case SignalStabilityState.Corrupted:
                return FilterCorruptedSignal(rawData);

            case SignalStabilityState.Disconnected:
                return null; // Reject disconnected signals

            default:
                return _config.AllowUnknownSignals ? rawData : null;
        }
    }

    /// <summary>
    /// Get current signal diagnostics
    /// </summary>
    /// <returns>Detailed signal diagnostics</returns>
    public SignalDiagnostics GetDiagnostics()
    {
        var samples = _recentSamples.ToArray();
        
        lock (_statsLock)
        {
            return new SignalDiagnostics
            {
                CurrentState = _currentState,
                StabilityScore = _currentStabilityScore,
                SampleCount = samples.Length,
                ValidSampleRate = samples.Length > 0 ? (double)samples.Count(s => s.IsValid) / samples.Length : 0,
                AverageFrameLength = samples.Length > 0 ? samples.Average(s => s.FrameLength) : 0,
                FrameLengthVariation = CalculateFrameLengthVariation(samples),
                TimingConsistency = CalculateTimingConsistency(samples),
                SignalQuality = _statistics.SignalQuality,
                ErrorRate = _statistics.ErrorRate,
                LastUpdate = DateTime.UtcNow,
                RecommendedActions = GetRecommendedActions()
            };
        }
    }

    /// <summary>
    /// Reset stability monitoring (useful after hardware changes)
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("Resetting signal stability monitor");
        
        // Clear sample buffer
        while (_recentSamples.TryDequeue(out _)) { }
        
        lock (_statsLock)
        {
            _statistics = new SignalStatistics();
        }
        
        _currentState = SignalStabilityState.Unknown;
        _currentStabilityScore = 0.0;
    }

    #region Private Methods

    /// <summary>
    /// Periodic stability analysis (runs on timer)
    /// </summary>
    private void PerformStabilityAnalysis(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var samples = _recentSamples.ToArray();
            if (samples.Length < _config.MinSamplesForAnalysis)
                return;

            var analysis = AnalyzeSignalStability(samples);
            UpdateStabilityState(analysis);

            // Publish stability report
            var report = new SignalStabilityReport
            {
                Timestamp = DateTime.UtcNow,
                State = _currentState,
                Score = _currentStabilityScore,
                Analysis = analysis,
                SampleCount = samples.Length,
                RecommendedActions = GetRecommendedActions()
            };

            _stabilityReports.OnNext(report);

            _logger.LogDebug("Signal stability analysis: State={State}, Score={Score:F1}, Samples={SampleCount}",
                _currentState, _currentStabilityScore, samples.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signal stability analysis");
        }
    }

    /// <summary>
    /// Comprehensive signal stability analysis
    /// </summary>
    private StabilityAnalysis AnalyzeSignalStability(SignalSample[] samples)
    {
        var validSamples = samples.Where(s => s.IsValid).ToArray();
        var recentSamples = samples.Where(s => s.Timestamp > DateTime.UtcNow.AddSeconds(-30)).ToArray();

        // Calculate key metrics
        var frameLengthConsistency = CalculateFrameLengthConsistency(samples);
        var timingConsistency = CalculateTimingConsistency(samples);
        var dataQuality = CalculateDataQuality(samples);
        var signalStrength = samples.Length > 0 ? samples.Average(s => s.SignalStrength) : 0;

        // Detect specific issues
        var hasCorruption = DetectDataCorruption(samples);
        var hasDropouts = DetectSignalDropouts(samples);
        var hasNoise = DetectElectricalNoise(samples);
        var hasTimingIssues = DetectTimingIssues(samples);

        // Calculate overall stability score
        var stabilityScore = CalculateOverallStability(
            frameLengthConsistency, timingConsistency, dataQuality, signalStrength);

        return new StabilityAnalysis
        {
            FrameLengthConsistency = frameLengthConsistency,
            TimingConsistency = timingConsistency,
            DataQuality = dataQuality,
            SignalStrength = signalStrength,
            HasCorruption = hasCorruption,
            HasDropouts = hasDropouts,
            HasNoise = hasNoise,
            HasTimingIssues = hasTimingIssues,
            OverallScore = stabilityScore,
            ValidSampleRate = samples.Length > 0 ? (double)validSamples.Length / samples.Length : 0
        };
    }

    /// <summary>
    /// Update stability state based on analysis
    /// </summary>
    private void UpdateStabilityState(StabilityAnalysis analysis)
    {
        var previousState = _currentState;
        var newState = DetermineStabilityState(analysis);
        
        _currentState = newState;
        _currentStabilityScore = analysis.OverallScore;

        // Update statistics
        lock (_statsLock)
        {
            _statistics.SignalQuality = analysis.DataQuality;
            _statistics.ErrorRate = 100 - analysis.ValidSampleRate;
            _statistics.LastAnalysis = DateTime.UtcNow;
        }

        // Log state changes
        if (newState != previousState)
        {
            _logger.LogInformation("Signal stability state changed: {PreviousState} -> {NewState} (Score: {Score:F1})",
                previousState, newState, analysis.OverallScore);
        }
    }

    /// <summary>
    /// Determine stability state from analysis results
    /// </summary>
    private SignalStabilityState DetermineStabilityState(StabilityAnalysis analysis)
    {
        // No recent samples = disconnected
        if (analysis.ValidSampleRate < 0.1)
            return SignalStabilityState.Disconnected;

        // High corruption = corrupted signal
        if (analysis.HasCorruption && analysis.DataQuality < 30)
            return SignalStabilityState.Corrupted;

        // Frequent dropouts = intermittent connection
        if (analysis.HasDropouts && analysis.ValidSampleRate < 70)
            return SignalStabilityState.Intermittent;

        // Electrical noise but mostly readable
        if (analysis.HasNoise && analysis.DataQuality > 60)
            return SignalStabilityState.Noisy;

        // Good signal quality
        if (analysis.OverallScore >= _config.StabilityThreshold)
            return SignalStabilityState.Stable;

        // Timing issues or other problems
        if (analysis.HasTimingIssues)
            return SignalStabilityState.Intermittent;

        return SignalStabilityState.Unstable;
    }

    #endregion

    #region Signal Analysis Methods

    private bool HasControlCharacters(byte[] data)
    {
        // Check for unexpected control characters (excluding common ones like CR, LF)
        return data.Any(b => b < 32 && b != 13 && b != 10 && b != 9);
    }

    private double CalculateSignalStrength(byte[] data)
    {
        // Simple signal strength based on data consistency and valid character ratio
        var validChars = data.Count(b => b >= 32 || b == 13 || b == 10 || b == 9);
        return data.Length > 0 ? (double)validChars / data.Length * 100 : 0;
    }

    private double CalculateFrameLengthConsistency(SignalSample[] samples)
    {
        if (samples.Length < 2) return 100;

        var lengths = samples.Select(s => s.FrameLength).ToArray();
        var mean = lengths.Average();
        var variance = lengths.Average(l => Math.Pow(l - mean, 2));
        var stdDev = Math.Sqrt(variance);

        // Coefficient of variation - lower is more consistent
        var cv = mean > 0 ? stdDev / mean : 1.0;
        return Math.Max(0, 100 - (cv * 50)); // Scale appropriately
    }

    private double CalculateTimingConsistency(SignalSample[] samples)
    {
        if (samples.Length < 3) return 100;

        var orderedSamples = samples.OrderBy(s => s.Timestamp).ToArray();
        var intervals = new List<double>();

        for (int i = 1; i < orderedSamples.Length; i++)
        {
            var interval = (orderedSamples[i].Timestamp - orderedSamples[i - 1].Timestamp).TotalMilliseconds;
            if (interval > 0 && interval < 10000) // Reasonable range
                intervals.Add(interval);
        }

        if (!intervals.Any()) return 50;

        var mean = intervals.Average();
        var variance = intervals.Average(i => Math.Pow(i - mean, 2));
        var stdDev = Math.Sqrt(variance);

        var cv = mean > 0 ? stdDev / mean : 1.0;
        return Math.Max(0, 100 - (cv * 100));
    }

    private double CalculateDataQuality(SignalSample[] samples)
    {
        if (!samples.Any()) return 0;

        var qualityFactors = new List<double>();

        // Valid sample ratio
        qualityFactors.Add((double)samples.Count(s => s.IsValid) / samples.Length * 100);

        // Absence of null bytes
        qualityFactors.Add((double)samples.Count(s => !s.HasNullBytes) / samples.Length * 100);

        // Absence of unexpected control characters
        qualityFactors.Add((double)samples.Count(s => !s.HasControlChars) / samples.Length * 100);

        // Average signal strength
        qualityFactors.Add(samples.Average(s => s.SignalStrength));

        return qualityFactors.Average();
    }

    private bool DetectDataCorruption(SignalSample[] samples)
    {
        if (samples.Length < 5) return false;

        var corruptionIndicators = 0;

        // High rate of null bytes
        if (samples.Count(s => s.HasNullBytes) > samples.Length * 0.3)
            corruptionIndicators++;

        // High rate of control characters
        if (samples.Count(s => s.HasControlChars) > samples.Length * 0.2)
            corruptionIndicators++;

        // Extreme frame length variations
        var lengths = samples.Select(s => s.FrameLength).ToArray();
        if (lengths.Max() > lengths.Average() * 3)
            corruptionIndicators++;

        return corruptionIndicators >= 2;
    }

    private bool DetectSignalDropouts(SignalSample[] samples)
    {
        if (samples.Length < 10) return false;

        var orderedSamples = samples.OrderBy(s => s.Timestamp).ToArray();
        var largeGaps = 0;

        for (int i = 1; i < orderedSamples.Length; i++)
        {
            var gap = (orderedSamples[i].Timestamp - orderedSamples[i - 1].Timestamp).TotalMilliseconds;
            if (gap > _config.DropoutThresholdMs)
                largeGaps++;
        }

        return largeGaps > samples.Length * 0.1; // More than 10% large gaps
    }

    private bool DetectElectricalNoise(SignalSample[] samples)
    {
        if (samples.Length < 5) return false;

        // Look for inconsistent signal strength
        var strengths = samples.Select(s => s.SignalStrength).ToArray();
        var mean = strengths.Average();
        var variance = strengths.Average(s => Math.Pow(s - mean, 2));
        
        return variance > 400; // High variance in signal strength
    }

    private bool DetectTimingIssues(SignalSample[] samples)
    {
        return CalculateTimingConsistency(samples) < 50;
    }

    private double CalculateOverallStability(
        double frameLengthConsistency,
        double timingConsistency,
        double dataQuality,
        double signalStrength)
    {
        // Weighted average of stability factors
        return (dataQuality * 0.4) + 
               (frameLengthConsistency * 0.25) + 
               (timingConsistency * 0.2) + 
               (signalStrength * 0.15);
    }

    private double CalculateFrameLengthVariation(SignalSample[] samples)
    {
        if (samples.Length < 2) return 0;
        
        var lengths = samples.Select(s => s.FrameLength).ToArray();
        var mean = lengths.Average();
        var variance = lengths.Average(l => Math.Pow(l - mean, 2));
        
        return mean > 0 ? Math.Sqrt(variance) / mean * 100 : 0;
    }

    #endregion

    #region Signal Filtering Methods

    private byte[]? FilterNoisySignal(byte[] rawData)
    {
        // Remove obvious noise: null bytes, excessive control characters
        var filtered = rawData.Where(b => b != 0 && (b >= 32 || b == 13 || b == 10)).ToArray();
        
        // Reject if too much was filtered out
        if (filtered.Length < rawData.Length * 0.7)
            return null;
            
        return filtered;
    }

    private byte[]? FilterIntermittentSignal(byte[] rawData)
    {
        // More aggressive filtering for intermittent signals
        // Only accept data that looks like valid protocol frames
        var text = Encoding.ASCII.GetString(rawData);
        
        // Basic validation: should contain some digits (weight data)
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"\d"))
            return null;
            
        return rawData;
    }

    private byte[]? FilterCorruptedSignal(byte[] rawData)
    {
        // Very strict filtering for corrupted signals
        // Only pass data that meets strict quality criteria
        
        // Reject if contains null bytes
        if (rawData.Contains((byte)0))
            return null;
            
        // Reject if too many control characters
        var controlChars = rawData.Count(b => b < 32 && b != 13 && b != 10);
        if (controlChars > rawData.Length * 0.1)
            return null;
            
        return rawData;
    }

    #endregion

    private IReadOnlyList<string> GetRecommendedActions()
    {
        var actions = new List<string>();

        switch (_currentState)
        {
            case SignalStabilityState.Disconnected:
                actions.Add("Check cable connections");
                actions.Add("Verify power to device");
                actions.Add("Test cable continuity");
                break;

            case SignalStabilityState.Corrupted:
                actions.Add("Check cable shielding");
                actions.Add("Verify ground connections");
                actions.Add("Check for electromagnetic interference");
                actions.Add("Replace RS232 cable");
                break;

            case SignalStabilityState.Intermittent:
                actions.Add("Secure loose connections");
                actions.Add("Check connector pins");
                actions.Add("Verify baud rate settings");
                break;

            case SignalStabilityState.Noisy:
                actions.Add("Improve cable shielding");
                actions.Add("Check grounding");
                actions.Add("Move away from interference sources");
                break;

            case SignalStabilityState.Unstable:
                actions.Add("Check signal timing");
                actions.Add("Verify device configuration");
                actions.Add("Monitor for pattern in instability");
                break;
        }

        return actions;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        _analysisTimer?.Dispose();
        _stabilityReports.OnCompleted();
        _stabilityReports.Dispose();
        
        _logger.LogInformation("Signal stability monitor disposed");
    }
}

/// <summary>
/// Signal stability states
/// </summary>
public enum SignalStabilityState
{
    Unknown,
    Stable,
    Noisy,
    Intermittent,
    Corrupted,
    Disconnected,
    Unstable
}

/// <summary>
/// Configuration for signal stability monitoring
/// </summary>
public sealed record SignalStabilityConfiguration
{
    public int SampleBufferSize { get; init; } = 200;
    public int AnalysisIntervalMs { get; init; } = 2000;
    public int MinSamplesForAnalysis { get; init; } = 10;
    public double StabilityThreshold { get; init; } = 80.0;
    public int DropoutThresholdMs { get; init; } = 5000;
    public bool AllowUnknownSignals { get; init; } = true;

    public static SignalStabilityConfiguration Default => new();
}

/// <summary>
/// Individual signal sample for analysis
/// </summary>
public sealed record SignalSample
{
    public required byte[] Data { get; init; }
    public required DateTime Timestamp { get; init; }
    public required bool IsValid { get; init; }
    public required int FrameLength { get; init; }
    public required bool HasNullBytes { get; init; }
    public required bool HasControlChars { get; init; }
    public required double SignalStrength { get; init; }
}

/// <summary>
/// Signal stability analysis results
/// </summary>
public sealed record StabilityAnalysis
{
    public double FrameLengthConsistency { get; init; }
    public double TimingConsistency { get; init; }
    public double DataQuality { get; init; }
    public double SignalStrength { get; init; }
    public bool HasCorruption { get; init; }
    public bool HasDropouts { get; init; }
    public bool HasNoise { get; init; }
    public bool HasTimingIssues { get; init; }
    public double OverallScore { get; init; }
    public double ValidSampleRate { get; init; }
}

/// <summary>
/// Signal stability report
/// </summary>
public sealed record SignalStabilityReport
{
    public required DateTime Timestamp { get; init; }
    public required SignalStabilityState State { get; init; }
    public required double Score { get; init; }
    public required StabilityAnalysis Analysis { get; init; }
    public required int SampleCount { get; init; }
    public required IReadOnlyList<string> RecommendedActions { get; init; }
}

/// <summary>
/// Comprehensive signal diagnostics
/// </summary>
public sealed record SignalDiagnostics
{
    public required SignalStabilityState CurrentState { get; init; }
    public required double StabilityScore { get; init; }
    public required int SampleCount { get; init; }
    public required double ValidSampleRate { get; init; }
    public required double AverageFrameLength { get; init; }
    public required double FrameLengthVariation { get; init; }
    public required double TimingConsistency { get; init; }
    public required double SignalQuality { get; init; }
    public required double ErrorRate { get; init; }
    public required DateTime LastUpdate { get; init; }
    public required IReadOnlyList<string> RecommendedActions { get; init; }
}

/// <summary>
/// Internal signal statistics
/// </summary>
internal sealed record SignalStatistics
{
    public double SignalQuality { get; set; }
    public double ErrorRate { get; set; }
    public DateTime LastAnalysis { get; set; } = DateTime.UtcNow;
}