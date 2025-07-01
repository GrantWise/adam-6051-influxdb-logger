// Industrial.IoT.Platform.Protocols - Protocol Discovery Service
// Main service for protocol discovery operations following existing ADAM logger patterns

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Protocols.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Protocols.Services;

/// <summary>
/// Protocol discovery service implementing standard patterns from the existing ADAM logger
/// Provides managed discovery operations with health monitoring and reactive streams
/// </summary>
public sealed class ProtocolDiscoveryService : BackgroundService, IHealthCheck, IDisposable
{
    private readonly ILogger<ProtocolDiscoveryService> _logger;
    private readonly ProtocolDiscoveryEngine _discoveryEngine;
    private readonly SignalStabilityMonitor _stabilityMonitor;
    private readonly TemplateRepository _templateRepository;

    // Reactive streams for real-time updates (following ADAM logger patterns)
    private readonly Subject<DiscoveryProgress> _progressSubject = new();
    private readonly Subject<DiscoveryResult> _resultsSubject = new();
    private readonly Subject<SignalStabilityReport> _stabilitySubject = new();

    // Service state management
    private readonly Dictionary<Guid, DiscoverySession> _activeSessions = new();
    private readonly object _sessionsLock = new();
    private volatile bool _isHealthy = true;
    private volatile string? _lastError;

    // Health monitoring statistics
    private int _totalDiscoverySessions;
    private int _successfulDiscoveries;
    private TimeSpan _averageDiscoveryTime = TimeSpan.Zero;

    /// <summary>
    /// Observable stream of discovery progress updates
    /// </summary>
    public IObservable<DiscoveryProgress> ProgressStream => _progressSubject.AsObservable();

    /// <summary>
    /// Observable stream of discovery results
    /// </summary>
    public IObservable<DiscoveryResult> ResultsStream => _resultsSubject.AsObservable();

    /// <summary>
    /// Observable stream of signal stability reports
    /// </summary>
    public IObservable<SignalStabilityReport> StabilityStream => _stabilitySubject.AsObservable();

    /// <summary>
    /// Number of currently active discovery sessions
    /// </summary>
    public int ActiveSessionCount
    {
        get
        {
            lock (_sessionsLock)
            {
                return _activeSessions.Count;
            }
        }
    }

    /// <summary>
    /// Success rate of discovery operations (0-100)
    /// </summary>
    public double SuccessRate => _totalDiscoverySessions > 0 ? 
        (double)_successfulDiscoveries / _totalDiscoverySessions * 100 : 0;

    /// <summary>
    /// Initialize protocol discovery service
    /// </summary>
    /// <param name="logger">Logging service</param>
    /// <param name="discoveryEngine">Protocol discovery engine</param>
    /// <param name="stabilityMonitor">Signal stability monitor</param>
    /// <param name="templateRepository">Template repository</param>
    public ProtocolDiscoveryService(
        ILogger<ProtocolDiscoveryService> logger,
        ProtocolDiscoveryEngine discoveryEngine,
        SignalStabilityMonitor stabilityMonitor,
        TemplateRepository templateRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveryEngine = discoveryEngine ?? throw new ArgumentNullException(nameof(discoveryEngine));
        _stabilityMonitor = stabilityMonitor ?? throw new ArgumentNullException(nameof(stabilityMonitor));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));

        // Subscribe to stability reports for forwarding
        _stabilityMonitor.StabilityReports.Subscribe(
            report => _stabilitySubject.OnNext(report),
            ex => _logger.LogError(ex, "Error in stability monitoring stream")
        );

        _logger.LogInformation("Protocol discovery service initialized");
    }

    /// <summary>
    /// Start new protocol discovery session
    /// </summary>
    /// <param name="transport">Transport provider for device communication</param>
    /// <param name="configuration">Discovery configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Discovery session identifier</returns>
    public async Task<Guid> StartDiscoveryAsync(
        ITransportProvider transport,
        DiscoveryConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        if (transport == null) throw new ArgumentNullException(nameof(transport));

        var config = configuration ?? new DiscoveryConfiguration();
        var sessionId = Guid.NewGuid();

        try
        {
            _logger.LogInformation("Starting protocol discovery session {SessionId}", sessionId);

            // Start discovery session
            var session = await _discoveryEngine.StartDiscoveryAsync(transport, config, cancellationToken);

            // Track session
            lock (_sessionsLock)
            {
                _activeSessions[sessionId] = session;
            }

            // Report progress
            _progressSubject.OnNext(new DiscoveryProgress
            {
                SessionId = sessionId,
                Phase = session.Phase,
                Progress = 10, // Initial progress
                Message = "Discovery session started",
                Timestamp = DateTime.UtcNow
            });

            Interlocked.Increment(ref _totalDiscoverySessions);
            _logger.LogInformation("Discovery session {SessionId} started successfully", sessionId);

            return sessionId;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _isHealthy = false;
            _logger.LogError(ex, "Failed to start discovery session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Continue discovery session with interactive guidance
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="guidance">Interactive guidance configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated discovery session</returns>
    public async Task ContinueInteractiveDiscoveryAsync(
        Guid sessionId,
        InteractiveGuidance guidance,
        CancellationToken cancellationToken = default)
    {
        if (guidance == null) throw new ArgumentNullException(nameof(guidance));

        DiscoverySession? session;
        lock (_sessionsLock)
        {
            if (!_activeSessions.TryGetValue(sessionId, out session))
                throw new InvalidOperationException($"Discovery session {sessionId} not found");
        }

        try
        {
            _logger.LogInformation("Continuing interactive discovery for session {SessionId}", sessionId);

            await _discoveryEngine.ContinueInteractiveDiscoveryAsync(sessionId, guidance, cancellationToken);

            // Report progress
            _progressSubject.OnNext(new DiscoveryProgress
            {
                SessionId = sessionId,
                Phase = session.Phase,
                Progress = 60, // Interactive phase progress
                Message = $"Interactive discovery: {session.CurrentStep}/{guidance.Steps.Count} steps completed",
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Interactive discovery continued for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to continue interactive discovery for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Complete discovery session and get results
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="saveTemplate">Whether to save discovered template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final discovery results</returns>
    public async Task<DiscoveryResult> CompleteDiscoveryAsync(
        Guid sessionId,
        bool saveTemplate = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Completing discovery session {SessionId}", sessionId);

            var result = await _discoveryEngine.CompleteDiscoveryAsync(sessionId, saveTemplate, cancellationToken);

            // Remove from active sessions
            lock (_sessionsLock)
            {
                _activeSessions.Remove(sessionId);
            }

            // Update statistics
            if (result.Success)
            {
                Interlocked.Increment(ref _successfulDiscoveries);
            }

            // Update average discovery time
            var totalTime = TimeSpan.FromTicks(
                (_averageDiscoveryTime.Ticks * (_totalDiscoverySessions - 1) + result.Duration.Ticks) / _totalDiscoverySessions);
            _averageDiscoveryTime = totalTime;

            // Report final progress
            _progressSubject.OnNext(new DiscoveryProgress
            {
                SessionId = sessionId,
                Phase = DiscoveryPhase.Completed,
                Progress = 100,
                Message = result.Success ? 
                    $"Discovery completed successfully. Confidence: {result.Confidence:F1}%" :
                    "Discovery failed",
                Timestamp = DateTime.UtcNow
            });

            // Publish result
            _resultsSubject.OnNext(result);

            _logger.LogInformation("Discovery session {SessionId} completed. Success: {Success}, Confidence: {Confidence:F1}%",
                sessionId, result.Success, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to complete discovery session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Cancel active discovery session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    public void CancelDiscovery(Guid sessionId)
    {
        try
        {
            _discoveryEngine.CancelDiscovery(sessionId);

            lock (_sessionsLock)
            {
                _activeSessions.Remove(sessionId);
            }

            _progressSubject.OnNext(new DiscoveryProgress
            {
                SessionId = sessionId,
                Phase = DiscoveryPhase.Failed,
                Progress = 0,
                Message = "Discovery cancelled by user",
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Discovery session {SessionId} cancelled", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling discovery session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Get discovery session status
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Session status or null if not found</returns>
    public DiscoverySessionStatus? GetSessionStatus(Guid sessionId)
    {
        DiscoverySession? session;
        lock (_sessionsLock)
        {
            if (!_activeSessions.TryGetValue(sessionId, out session))
                return null;
        }

        return new DiscoverySessionStatus
        {
            SessionId = sessionId,
            Phase = session.Phase,
            IsActive = session.IsActive,
            Duration = session.Duration,
            CapturedFrames = session.CapturedFrameCount,
            BestConfidence = session.BestConfidence,
            CurrentStep = session.CurrentStep,
            TestedTemplates = session.TemplateResults.Count
        };
    }

    /// <summary>
    /// Get all available protocol templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of protocol templates</returns>
    public async Task<IReadOnlyList<ProtocolTemplate>> GetAvailableTemplatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _templateRepository.GetAllTemplatesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to get available templates");
            throw;
        }
    }

    /// <summary>
    /// Background service execution (following existing ADAM logger patterns)
    /// </summary>
    /// <param name="stoppingToken">Stopping token</param>
    /// <returns>Task representing background execution</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Protocol discovery service background task started");

        try
        {
            // Preload templates for faster discovery
            await _templateRepository.GetAllTemplatesAsync(stoppingToken);
            _logger.LogInformation("Protocol templates preloaded successfully");

            // Background health monitoring loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    
                    // Cleanup expired sessions
                    await CleanupExpiredSessionsAsync();
                    
                    // Reset health status if no recent errors
                    if (_lastError != null)
                    {
                        _isHealthy = true;
                        _lastError = null;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in discovery service background task");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in discovery service background task");
            _isHealthy = false;
            _lastError = ex.Message;
        }

        _logger.LogInformation("Protocol discovery service background task stopped");
    }

    /// <summary>
    /// Health check implementation
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["ActiveSessions"] = ActiveSessionCount,
                ["TotalSessions"] = _totalDiscoverySessions,
                ["SuccessfulSessions"] = _successfulDiscoveries,
                ["SuccessRate"] = $"{SuccessRate:F1}%",
                ["AverageDiscoveryTime"] = _averageDiscoveryTime.ToString(@"mm\:ss\.fff")
            };

            if (_isHealthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Protocol discovery service is healthy", data));
            }
            else
            {
                return Task.FromResult(HealthCheckResult.Degraded($"Protocol discovery service has issues: {_lastError}", null, data));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Protocol discovery service health check failed", ex));
        }
    }

    /// <summary>
    /// Cleanup expired or abandoned discovery sessions
    /// </summary>
    private Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = new List<Guid>();
        var maxAge = TimeSpan.FromHours(1); // Sessions expire after 1 hour

        lock (_sessionsLock)
        {
            foreach (var kvp in _activeSessions)
            {
                if (kvp.Value.Duration > maxAge || !kvp.Value.IsActive)
                {
                    expiredSessions.Add(kvp.Key);
                }
            }
        }

        foreach (var sessionId in expiredSessions)
        {
            try
            {
                _discoveryEngine.CancelDiscovery(sessionId);
                lock (_sessionsLock)
                {
                    _activeSessions.Remove(sessionId);
                }
                _logger.LogInformation("Cleaned up expired discovery session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up expired discovery session {SessionId}", sessionId);
            }
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired discovery sessions", expiredSessions.Count);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose of service resources
    /// </summary>
    public override void Dispose()
    {
        try
        {
            // Cancel all active sessions
            lock (_sessionsLock)
            {
                foreach (var sessionId in _activeSessions.Keys.ToList())
                {
                    try
                    {
                        _discoveryEngine.CancelDiscovery(sessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cancelling session {SessionId} during dispose", sessionId);
                    }
                }
                _activeSessions.Clear();
            }

            // Dispose reactive streams
            _progressSubject.OnCompleted();
            _progressSubject.Dispose();
            _resultsSubject.OnCompleted();
            _resultsSubject.Dispose();
            _stabilitySubject.OnCompleted();
            _stabilitySubject.Dispose();

            _logger.LogInformation("Protocol discovery service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing protocol discovery service");
        }
        finally
        {
            base.Dispose();
        }
    }
}

/// <summary>
/// Discovery progress information for real-time updates
/// </summary>
public sealed record DiscoveryProgress
{
    /// <summary>
    /// Discovery session identifier
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Current discovery phase
    /// </summary>
    public required DiscoveryPhase Phase { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public required int Progress { get; init; }

    /// <summary>
    /// Progress message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Progress timestamp
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Additional progress data
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Discovery session status information
/// </summary>
public sealed record DiscoverySessionStatus
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Current discovery phase
    /// </summary>
    public required DiscoveryPhase Phase { get; init; }

    /// <summary>
    /// Whether session is still active
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Session duration so far
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of captured frames
    /// </summary>
    public required int CapturedFrames { get; init; }

    /// <summary>
    /// Best confidence score achieved
    /// </summary>
    public required double BestConfidence { get; init; }

    /// <summary>
    /// Current interactive step
    /// </summary>
    public required int CurrentStep { get; init; }

    /// <summary>
    /// Number of templates tested
    /// </summary>
    public required int TestedTemplates { get; init; }
}