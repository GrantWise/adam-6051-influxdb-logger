// Industrial.IoT.Platform.Protocols - Protocol Discovery Engine
// Core service for discovering and validating scale protocols
// Enhanced port of Python discovery algorithm with C# improvements

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Protocols.Models;
using Microsoft.Extensions.Logging;
using DiscoveryConfiguration = Industrial.IoT.Platform.Core.Models.DiscoveryConfiguration;
using DiscoveryStep = Industrial.IoT.Platform.Protocols.Models.DiscoveryStep;

namespace Industrial.IoT.Platform.Protocols.Services;

/// <summary>
/// Core protocol discovery engine implementing two-phase discovery algorithm
/// Enhanced version of Python ProtocolDiscovery with improved reliability and performance
/// </summary>
public sealed class ProtocolDiscoveryEngine : IProtocolDiscoveryService, IDisposable
{
    private readonly ILogger<ProtocolDiscoveryEngine> _logger;
    private readonly TemplateRepository _templateRepository;
    private readonly SignalStabilityMonitor _stabilityMonitor;
    private readonly ConcurrentDictionary<Guid, DiscoverySession> _activeSessions = new();
    private volatile bool _isDisposed;

    /// <summary>
    /// Initialize protocol discovery engine
    /// </summary>
    /// <param name="logger">Logging service</param>
    /// <param name="templateRepository">Template repository for known protocols</param>
    /// <param name="stabilityMonitor">Signal stability monitor for RS232 reliability</param>
    public ProtocolDiscoveryEngine(
        ILogger<ProtocolDiscoveryEngine> logger,
        TemplateRepository templateRepository,
        SignalStabilityMonitor stabilityMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _stabilityMonitor = stabilityMonitor ?? throw new ArgumentNullException(nameof(stabilityMonitor));
    }

    /// <summary>
    /// Start new protocol discovery session
    /// Implements Python start_discovery() with enhanced error handling
    /// </summary>
    /// <param name="transport">Transport provider for data capture</param>
    /// <param name="configuration">Discovery configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New discovery session</returns>
    public async Task<DiscoverySession> StartDiscoveryAsync(
        ITransportProvider transport,
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (transport == null) throw new ArgumentNullException(nameof(transport));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        _logger.LogInformation("Starting protocol discovery session");

        var session = new DiscoverySession(transport, configuration);
        _activeSessions[session.SessionId] = session;

        try
        {
            // Phase 1: Initialize and capture baseline data
            session.AdvancePhase(DiscoveryPhase.CapturingData);
            _logger.LogInformation("Phase 1: Capturing baseline data for session {SessionId}", session.SessionId);
            await CaptureBaselineDataAsync(session, cancellationToken);

            // Phase 2: Test known templates if we have captured data
            if (session.CapturedFrameCount > 0)
            {
                session.AdvancePhase(DiscoveryPhase.TestingTemplates);
                _logger.LogInformation("Phase 2: Testing {TemplateCount} known templates for session {SessionId}", 
                    (await _templateRepository.GetAllTemplatesAsync(cancellationToken)).Count, session.SessionId);
                await TestKnownTemplatesAsync(session, cancellationToken);

                // Evaluate if template discovery was successful
                var bestConfidence = session.BestConfidence;
                var requiredConfidence = session.Configuration.ConfidenceThreshold;

                if (bestConfidence >= requiredConfidence)
                {
                    session.AdvancePhase(DiscoveryPhase.Completed);
                    _logger.LogInformation("Template discovery successful for session {SessionId}. Best: {Template} ({Confidence:F1}%)", 
                        session.SessionId, session.BestTemplate?.Name, bestConfidence);
                }
                else
                {
                    session.AdvancePhase(DiscoveryPhase.InteractiveDiscovery);
                    _logger.LogInformation("Template discovery incomplete for session {SessionId}. Best confidence: {Confidence:F1}% (required: {Required:F1}%). Interactive discovery needed.", 
                        session.SessionId, bestConfidence, requiredConfidence);
                }
            }
            else
            {
                session.AdvancePhase(DiscoveryPhase.InteractiveDiscovery);
                _logger.LogWarning("No data captured for session {SessionId}. Interactive discovery required.", session.SessionId);
            }

            _logger.LogInformation("Discovery session {SessionId} started successfully. Phase: {Phase}", 
                session.SessionId, session.Phase);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start discovery session");
            session.AdvancePhase(DiscoveryPhase.Failed);
            throw;
        }
    }

    /// <summary>
    /// Continue interactive discovery with user guidance
    /// Implements Python interactive_discovery() with structured steps
    /// </summary>
    /// <param name="sessionId">Active session identifier</param>
    /// <param name="userGuidance">User guidance for discovery steps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated discovery session</returns>
    public async Task<DiscoverySession> ContinueInteractiveDiscoveryAsync(
        Guid sessionId,
        InteractiveGuidance userGuidance,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeSessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Discovery session {sessionId} not found or expired");

        if (!session.IsActive)
            throw new InvalidOperationException($"Discovery session {sessionId} is not active");

        _logger.LogInformation("Continuing interactive discovery for session {SessionId}", sessionId);

        try
        {
            session.AdvancePhase(DiscoveryPhase.InteractiveDiscovery);
            _logger.LogInformation("Starting interactive discovery for session {SessionId} with {StepCount} guided steps", 
                sessionId, userGuidance.Steps.Count);

            // Execute guided discovery steps with ground truth correlation
            await ExecuteGuidedStepsAsync(session, userGuidance, cancellationToken);

            // Analyze collected data for ground truth correlation
            var correlationResults = AnalyzeGroundTruthCorrelation(session, userGuidance);
            
            _logger.LogInformation("Ground truth correlation analysis completed for session {SessionId}. Correlation score: {Score:F1}%", 
                sessionId, correlationResults.OverallCorrelation);

            // If we have enough data and good correlation, try to generate a new template
            if (session.Steps.Count >= userGuidance.MinimumSteps && correlationResults.OverallCorrelation >= 70.0)
            {
                session.AdvancePhase(DiscoveryPhase.GeneratingTemplate);
                _logger.LogInformation("Generating new template for session {SessionId} based on {StepCount} successful steps", 
                    sessionId, session.Steps.Count(s => s.Status == StepStatus.Completed));
                await GenerateTemplateFromStepsAsync(session, correlationResults, cancellationToken);
            }
            else if (correlationResults.OverallCorrelation < 70.0)
            {
                _logger.LogWarning("Ground truth correlation too low for session {SessionId}: {Score:F1}%. Template generation skipped.", 
                    sessionId, correlationResults.OverallCorrelation);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive discovery failed for session {SessionId}", sessionId);
            session.AdvancePhase(DiscoveryPhase.Failed);
            throw;
        }
    }

    /// <summary>
    /// Get discovery session by ID
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Discovery session if found</returns>
    public DiscoverySession? GetSession(Guid sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Complete discovery session and finalize results
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
        ThrowIfDisposed();

        if (!_activeSessions.TryRemove(sessionId, out var session))
            throw new InvalidOperationException($"Discovery session {sessionId} not found");

        try
        {
            var result = new DiscoveryResult
            {
                SessionId = sessionId,
                Success = session.IsSuccessful(),
                BestTemplate = session.BestTemplate,
                Confidence = session.BestConfidence,
                Duration = session.Duration,
                CapturedFrames = session.CapturedFrameCount,
                TestedTemplates = session.TemplateResults.Count,
                InteractiveSteps = session.Steps.Count
            };

            if (result.Success && saveTemplate && session.BestTemplate != null)
            {
                await _templateRepository.SaveTemplateAsync(session.BestTemplate, cancellationToken);
                _logger.LogInformation("Saved discovered template {TemplateId} with confidence {Confidence:F1}%", 
                    session.BestTemplate.TemplateId, session.BestConfidence);
            }

            session.AdvancePhase(result.Success ? DiscoveryPhase.Completed : DiscoveryPhase.Failed);
            _logger.LogInformation("Discovery session {SessionId} completed. Success: {Success}, Confidence: {Confidence:F1}%", 
                sessionId, result.Success, result.Confidence);

            return result;
        }
        finally
        {
            session.Dispose();
        }
    }

    /// <summary>
    /// Cancel active discovery session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    public void CancelDiscovery(Guid sessionId)
    {
        if (_activeSessions.TryRemove(sessionId, out var session))
        {
            session.Cancel();
            session.Dispose();
            _logger.LogInformation("Discovery session {SessionId} cancelled", sessionId);
        }
    }

    #region Private Methods

    /// <summary>
    /// Capture baseline data from transport with signal stability monitoring
    /// Enhanced version of Python capture_baseline() with RS232 reliability
    /// </summary>
    private async Task CaptureBaselineDataAsync(DiscoverySession session, CancellationToken cancellationToken)
    {
        var config = session.Configuration;
        var startTime = DateTime.UtcNow;
        
        // Subscribe to transport data with stability monitoring
        void OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            var timestamp = DateTime.UtcNow;
            
            // Add sample to stability monitor
            _stabilityMonitor.AddSample(e.Data, timestamp, e.Data.Length > 0);
            
            // Filter data based on signal stability
            var filteredData = _stabilityMonitor.FilterIncomingData(e.Data);
            if (filteredData != null)
            {
                session.AddCapturedFrame(filteredData, timestamp);
            }
            else
            {
                _logger.LogDebug("Filtered out unstable data for session {SessionId}", session.SessionId);
            }
        }

        session.Transport.DataReceived += OnDataReceived;

        // Subscribe to stability reports for discovery guidance
        var stabilitySubscription = _stabilityMonitor.StabilityReports.Subscribe(
            report => 
            {
                _logger.LogInformation("Signal stability: {State} (Score: {Score:F1}%) for session {SessionId}",
                    report.State, report.Score, session.SessionId);
                
                // Log recommended actions if signal is unstable
                if (report.State != SignalStabilityState.Stable && report.RecommendedActions.Any())
                {
                    _logger.LogWarning("Signal stability issues detected. Recommended actions: {Actions}",
                        string.Join(", ", report.RecommendedActions));
                }
            }
        );

        try
        {
            // Wait for data capture or timeout
            var timeout = TimeSpan.FromMilliseconds(config.BaselineCaptureTimeoutMs);
            var endTime = startTime.Add(timeout);

            while (DateTime.UtcNow < endTime && session.CapturedFrameCount < config.MinimumFramesForAnalysis)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                
                // Check if signal is too unstable to continue
                if (_stabilityMonitor.CurrentState == SignalStabilityState.Disconnected)
                {
                    _logger.LogWarning("Signal disconnected during baseline capture for session {SessionId}", session.SessionId);
                    break;
                }
            }

            var diagnostics = _stabilityMonitor.GetDiagnostics();
            _logger.LogInformation("Baseline capture completed for session {SessionId}. Frames: {FrameCount}, Signal Quality: {Quality:F1}%", 
                session.SessionId, session.CapturedFrameCount, diagnostics.SignalQuality);
        }
        finally
        {
            session.Transport.DataReceived -= OnDataReceived;
            stabilitySubscription.Dispose();
        }
    }

    /// <summary>
    /// Test all known protocol templates against captured data
    /// Implements Python test_templates() with parallel processing and progress reporting
    /// </summary>
    private async Task TestKnownTemplatesAsync(DiscoverySession session, CancellationToken cancellationToken)
    {
        var templates = await _templateRepository.GetAllTemplatesAsync(cancellationToken);
        var capturedFrames = session.GetCapturedFrames();

        _logger.LogDebug("Testing {TemplateCount} known templates against {FrameCount} captured frames", 
            templates.Count, capturedFrames.Count);

        // Test templates with progress tracking
        var completedTemplates = 0;
        var testTasks = templates.Select(async template =>
        {
            var result = await TestTemplateAsync(template, capturedFrames, cancellationToken);
            
            // Update progress after each template test
            var progress = Interlocked.Increment(ref completedTemplates);
            var progressPercent = Math.Min(90, 20 + (int)((double)progress / templates.Count * 60)); // 20-80% range for template testing
            
            _logger.LogDebug("Template testing progress: {Progress}% ({Completed}/{Total}) - {TemplateId}: {Confidence:F1}%", 
                progressPercent, progress, templates.Count, template.TemplateId, result?.Confidence ?? 0);
            
            return result;
        });

        var testResults = await Task.WhenAll(testTasks);

        // Add results to session and identify best matches
        var successfulResults = testResults.Where(r => r != null).OrderByDescending(r => r!.Confidence).ToList();
        
        foreach (var result in successfulResults)
        {
            session.AddTemplateResult(result!);
        }

        // Log best matches for debugging
        var topMatches = successfulResults.Take(3).ToList();
        if (topMatches.Any())
        {
            _logger.LogInformation("Top template matches for session {SessionId}: {Matches}", 
                session.SessionId, 
                string.Join(", ", topMatches.Select(r => $"{r!.Template.Name}: {r.Confidence:F1}%")));
        }

        _logger.LogInformation("Template testing completed for session {SessionId}. Best confidence: {Confidence:F1}%", 
            session.SessionId, session.BestConfidence);
    }

    /// <summary>
    /// Test individual template against captured frames
    /// Enhanced version of Python validate_template() with detailed statistics
    /// </summary>
    private Task<TemplateTestResult?> TestTemplateAsync(
        ProtocolTemplate template, 
        IReadOnlyList<DataFrame> frames, 
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var stats = new TemplateValidationStats();
            var successfulParses = 0;
            var sampleData = new List<ParsedFrame>();
            var frameLengths = new List<int>();

            foreach (var frame in frames.Take(50)) // Limit test frames for performance
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var frameText = frame.ToString(Encoding.GetEncoding(template.Encoding));
                frameLengths.Add(frameText.Length);

                var parseResult = ParseFrameWithTemplate(template, frameText);
                if (parseResult.IsValid)
                {
                    successfulParses++;
                    if (sampleData.Count < 5) // Keep sample data for validation
                        sampleData.Add(parseResult);
                }
            }

            // Calculate validation statistics
            var validationStats = new TemplateValidationStats
            {
                TotalFrames = frames.Count,
                SuccessfulParses = successfulParses,
                FrameConsistency = CalculateFrameConsistency(frameLengths),
                FormatMatch = CalculateFormatMatch(template, frames),
                DataQuality = CalculateDataQuality(sampleData),
                AverageFrameLength = frameLengths.Any() ? frameLengths.Average() : 0,
                FrameLengthStdDev = CalculateStandardDeviation(frameLengths),
                ValidationDuration = DateTime.UtcNow - startTime
            };

            var confidence = validationStats.CalculateConfidence();

            return Task.FromResult<TemplateTestResult?>(new TemplateTestResult
            {
                Template = template,
                Confidence = confidence,
                ValidationStats = validationStats,
                TestDuration = DateTime.UtcNow - startTime,
                IsSuccessful = confidence >= 50.0, // Minimum threshold for consideration
                SampleData = sampleData
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to test template {TemplateId}", template.TemplateId);
            return Task.FromResult<TemplateTestResult?>(new TemplateTestResult
            {
                Template = template,
                Confidence = 0,
                TestDuration = DateTime.UtcNow - startTime,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Parse data frame using template field definitions
    /// </summary>
    private ParsedFrame ParseFrameWithTemplate(ProtocolTemplate template, string frameText)
    {
        var fieldValues = new Dictionary<string, object?>();
        var parseErrors = new List<string>();
        var isValid = true;

        foreach (var field in template.Fields)
        {
            var parseResult = field.ParseValue(frameText);
            if (parseResult.Success)
            {
                fieldValues[field.Name] = parseResult.Value;
            }
            else
            {
                fieldValues[field.Name] = null;
                if (field.IsRequired)
                {
                    isValid = false;
                    parseErrors.Add($"{field.Name}: {parseResult.ErrorMessage}");
                }
            }
        }

        return new ParsedFrame
        {
            RawFrame = frameText,
            FieldValues = fieldValues,
            IsValid = isValid,
            ParseErrors = parseErrors
        };
    }

    /// <summary>
    /// Execute guided discovery steps with user interaction
    /// </summary>
    private async Task ExecuteGuidedStepsAsync(
        DiscoverySession session, 
        InteractiveGuidance guidance, 
        CancellationToken cancellationToken)
    {
        foreach (var stepGuidance in guidance.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = new DiscoveryStep
            {
                StepNumber = session.CurrentStep + 1,
                Action = stepGuidance.Action,
                ExpectedWeight = stepGuidance.ExpectedWeight,
                Instructions = stepGuidance.Instructions,
                Status = StepStatus.InProgress
            };

            // Capture data for this step
            var capturedData = await CaptureStepDataAsync(session, stepGuidance, cancellationToken);
            
            // Analyze captured data
            var analysis = AnalyzeStepData(capturedData, stepGuidance);

            var completedStep = step with
            {
                CapturedData = capturedData,
                Analysis = analysis,
                Status = analysis.Confidence >= 70 ? StepStatus.Completed : StepStatus.Failed
            };

            session.AddStep(completedStep);
            
            _logger.LogDebug("Completed discovery step {StepNumber} for session {SessionId}. Confidence: {Confidence:F1}%", 
                step.StepNumber, session.SessionId, analysis.Confidence);
        }
    }

    /// <summary>
    /// Capture data for a specific discovery step
    /// </summary>
    private async Task<IReadOnlyList<string>> CaptureStepDataAsync(
        DiscoverySession session,
        StepGuidance stepGuidance,
        CancellationToken cancellationToken)
    {
        var capturedData = new List<string>();
        var captureStartTime = DateTime.UtcNow;
        var captureDuration = TimeSpan.FromMilliseconds(stepGuidance.CaptureTimeMs);

        void OnStepDataReceived(object? sender, DataReceivedEventArgs e)
        {
            var text = Encoding.ASCII.GetString(e.Data).Trim();
            if (!string.IsNullOrEmpty(text))
                capturedData.Add(text);
        }

        session.Transport.DataReceived += OnStepDataReceived;

        try
        {
            await Task.Delay(captureDuration, cancellationToken);
        }
        finally
        {
            session.Transport.DataReceived -= OnStepDataReceived;
        }

        return capturedData;
    }

    /// <summary>
    /// Analyze data captured during a discovery step
    /// </summary>
    private StepAnalysisResult AnalyzeStepData(IReadOnlyList<string> capturedData, StepGuidance stepGuidance)
    {
        if (!capturedData.Any())
        {
            return new StepAnalysisResult
            {
                Confidence = 0,
                DetectedPatterns = Array.Empty<string>()
            };
        }

        // Simple analysis - can be enhanced with more sophisticated algorithms
        var patterns = capturedData.GroupBy(d => d.Length)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"Length {g.Key}: {g.Count()} frames")
            .ToArray();

        var confidence = Math.Min(100, capturedData.Count * 20); // Simple confidence based on data volume

        return new StepAnalysisResult
        {
            DetectedPatterns = patterns,
            Confidence = confidence,
            IsStable = capturedData.Count >= 3,
            FormatConsistency = CalculateFormatConsistency(capturedData)
        };
    }

    /// <summary>
    /// Analyze ground truth correlation between expected and observed values
    /// Implements sophisticated correlation analysis for reliable protocol discovery
    /// </summary>
    private GroundTruthCorrelationResult AnalyzeGroundTruthCorrelation(DiscoverySession session, InteractiveGuidance guidance)
    {
        var correlationScores = new List<double>();
        var weightCorrelations = new List<double>();
        var timingCorrelations = new List<double>();
        var consistencyScores = new List<double>();

        foreach (var step in session.Steps.Where(s => s.Status == StepStatus.Completed))
        {
            var guidanceStep = guidance.Steps.FirstOrDefault(g => g.Action == step.Action);
            if (guidanceStep?.ExpectedWeight == null) continue;

            // Extract numeric values from captured data
            var extractedWeights = ExtractNumericValues(step.CapturedData);
            if (!extractedWeights.Any()) continue;

            // Calculate correlation with expected weight
            var expectedWeight = guidanceStep.ExpectedWeight.Value;
            var weightCorrelation = CalculateWeightCorrelation(extractedWeights, expectedWeight);
            weightCorrelations.Add(weightCorrelation);

            // Calculate timing consistency
            var timingConsistency = CalculateTimingConsistency(step.CapturedData);
            timingCorrelations.Add(timingConsistency);

            // Calculate data consistency
            var consistency = CalculateDataConsistency(step.CapturedData);
            consistencyScores.Add(consistency);

            var overallStepScore = (weightCorrelation * 0.5) + (timingConsistency * 0.25) + (consistency * 0.25);
            correlationScores.Add(overallStepScore);
        }

        var overallCorrelation = correlationScores.Any() ? correlationScores.Average() : 0;
        var weightAccuracy = weightCorrelations.Any() ? weightCorrelations.Average() : 0;
        var timingReliability = timingCorrelations.Any() ? timingCorrelations.Average() : 0;
        var dataConsistency = consistencyScores.Any() ? consistencyScores.Average() : 0;

        return new GroundTruthCorrelationResult
        {
            OverallCorrelation = overallCorrelation,
            WeightAccuracy = weightAccuracy,
            TimingReliability = timingReliability,
            DataConsistency = dataConsistency,
            AnalyzedSteps = session.Steps.Count(s => s.Status == StepStatus.Completed),
            RecommendedAction = overallCorrelation >= 85 ? "Generate template" :
                              overallCorrelation >= 70 ? "Generate template with validation" :
                              overallCorrelation >= 50 ? "Collect more data" : "Review setup"
        };
    }

    /// <summary>
    /// Extract numeric values from captured data strings
    /// </summary>
    private IList<double> ExtractNumericValues(IReadOnlyList<string> capturedData)
    {
        var numericValues = new List<double>();
        var numericPattern = new System.Text.RegularExpressions.Regex(@"[-+]?\d*\.?\d+");

        foreach (var data in capturedData)
        {
            var matches = numericPattern.Matches(data);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (double.TryParse(match.Value, out var value))
                {
                    numericValues.Add(value);
                }
            }
        }

        return numericValues;
    }

    /// <summary>
    /// Calculate correlation between extracted weights and expected weight
    /// </summary>
    private double CalculateWeightCorrelation(IList<double> extractedWeights, double expectedWeight)
    {
        if (!extractedWeights.Any()) return 0;

        // Find the extracted weight closest to expected
        var closestWeight = extractedWeights.OrderBy(w => Math.Abs(w - expectedWeight)).First();
        var percentageError = Math.Abs(closestWeight - expectedWeight) / Math.Max(expectedWeight, 0.001) * 100;

        // Convert error to correlation score (lower error = higher score)
        return Math.Max(0, 100 - percentageError);
    }

    /// <summary>
    /// Calculate timing consistency of data capture
    /// </summary>
    private double CalculateTimingConsistency(IReadOnlyList<string> capturedData)
    {
        if (capturedData.Count <= 1) return 100;

        // Simple timing consistency based on data volume and regularity
        var expectedCount = 5; // Expected minimum data points
        var actualCount = capturedData.Count;
        
        var volumeScore = Math.Min(100, (double)actualCount / expectedCount * 100);
        var consistencyScore = CalculateFormatConsistency(capturedData);

        return (volumeScore * 0.3) + (consistencyScore * 0.7);
    }

    /// <summary>
    /// Calculate data consistency across captured frames
    /// </summary>
    private double CalculateDataConsistency(IReadOnlyList<string> capturedData)
    {
        return CalculateFormatConsistency(capturedData);
    }

    /// <summary>
    /// Generate new template from interactive discovery steps with correlation analysis
    /// Enhanced version with ground truth validation
    /// </summary>
    private async Task GenerateTemplateFromStepsAsync(
        DiscoverySession session, 
        GroundTruthCorrelationResult correlationResults, 
        CancellationToken cancellationToken)
    {
        // This is a simplified template generation - real implementation would be more sophisticated
        var allCapturedData = session.Steps
            .SelectMany(s => s.CapturedData)
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        if (!allCapturedData.Any())
            return;

        // Generate enhanced template with correlation validation
        var template = new ProtocolTemplate
        {
            TemplateId = $"discovered_{session.SessionId:N}",
            Name = $"Discovered Protocol {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Description = $"Template generated from interactive discovery (Correlation: {correlationResults.OverallCorrelation:F1}%)",
            Delimiter = "\r\n", // Most common delimiter
            Fields = GenerateFieldsFromData(allCapturedData),
            ConfidenceScore = correlationResults.TemplateConfidence,
            DiscoveryDate = DateTime.UtcNow
        };

        // Test the generated template
        var frames = allCapturedData.Select(d => new DataFrame(Encoding.ASCII.GetBytes(d), DateTime.UtcNow)).ToList();
        var testResult = await TestTemplateAsync(template, frames, cancellationToken);
        
        if (testResult != null && testResult.IsSuccessful)
        {
            session.AddTemplateResult(testResult);
            _logger.LogInformation("Generated template {TemplateId} with confidence {Confidence:F1}%", 
                template.TemplateId, testResult.Confidence);
        }
    }

    /// <summary>
    /// Generate field definitions from captured data patterns
    /// Simplified implementation - real version would use more sophisticated pattern recognition
    /// </summary>
    private IReadOnlyList<ProtocolField> GenerateFieldsFromData(IList<string> capturedData)
    {
        var fields = new List<ProtocolField>();
        
        if (!capturedData.Any())
            return fields;

        var sampleFrame = capturedData.First();
        
        // Simple pattern: assume numeric data is weight
        var weightMatch = System.Text.RegularExpressions.Regex.Match(sampleFrame, @"\d+\.?\d*");
        if (weightMatch.Success)
        {
            fields.Add(new ProtocolField
            {
                Name = "Weight",
                Start = weightMatch.Index,
                Length = weightMatch.Length,
                FieldType = FieldType.Numeric,
                DecimalPlaces = weightMatch.Value.Contains('.') ? 
                    weightMatch.Value.Split('.')[1].Length : 0
            });
        }

        return fields;
    }

    #region Statistical Calculations - Enhanced Confidence Scoring

    /// <summary>
    /// Calculate frame consistency score based on length variation and structure
    /// Enhanced version with multiple consistency metrics
    /// </summary>
    private double CalculateFrameConsistency(IList<int> frameLengths)
    {
        if (frameLengths.Count <= 1) return 100.0;
        
        var mean = frameLengths.Average();
        var stdDev = CalculateStandardDeviation(frameLengths);
        
        // Multiple consistency metrics for robust scoring
        var lengthConsistency = CalculateLengthConsistency(frameLengths, mean, stdDev);
        var variationScore = CalculateVariationScore(stdDev, mean);
        var uniformityScore = CalculateUniformityScore(frameLengths);
        
        // Weighted combination of consistency metrics
        var overallConsistency = (lengthConsistency * 0.4) + (variationScore * 0.3) + (uniformityScore * 0.3);
        
        return Math.Max(0, Math.Min(100, overallConsistency));
    }

    /// <summary>
    /// Calculate format match score with enhanced pattern recognition
    /// Improved version with field-level validation and delimiter consistency
    /// </summary>
    private double CalculateFormatMatch(ProtocolTemplate template, IReadOnlyList<DataFrame> frames)
    {
        if (!frames.Any()) return 0;
        
        var encoding = Encoding.GetEncoding(template.Encoding);
        var scores = new List<double>();
        
        foreach (var frame in frames.Take(20)) // Sample frames for performance
        {
            var frameText = frame.ToString(encoding);
            var frameScore = CalculateFrameMatchScore(template, frameText);
            scores.Add(frameScore);
        }
        
        // Calculate weighted average with consistency bonus
        var averageScore = scores.Average();
        var consistencyBonus = CalculateScoreConsistency(scores);
        
        return Math.Min(100, averageScore + (consistencyBonus * 0.1));
    }

    /// <summary>
    /// Calculate data quality score with enhanced validation
    /// Includes field-level validation, data type checking, and value range analysis
    /// </summary>
    private double CalculateDataQuality(IList<ParsedFrame> parsedFrames)
    {
        if (!parsedFrames.Any()) return 0;
        
        var validFrames = parsedFrames.Count(f => f.IsValid);
        var baseQuality = (double)validFrames / parsedFrames.Count * 100;
        
        // Enhanced quality metrics
        var fieldCompleteness = CalculateFieldCompleteness(parsedFrames);
        var dataTypeConsistency = CalculateDataTypeConsistency(parsedFrames);
        var valueReasonableness = CalculateValueReasonableness(parsedFrames);
        
        // Weighted quality score
        var enhancedQuality = (baseQuality * 0.4) + (fieldCompleteness * 0.25) + 
                              (dataTypeConsistency * 0.2) + (valueReasonableness * 0.15);
        
        return Math.Max(0, Math.Min(100, enhancedQuality));
    }

    /// <summary>
    /// Calculate format consistency for raw data strings
    /// </summary>
    private double CalculateFormatConsistency(IReadOnlyList<string> data)
    {
        if (data.Count <= 1) return 100.0;
        
        var lengths = data.Select(d => d.Length).ToList();
        var lengthConsistency = CalculateFrameConsistency(lengths);
        
        // Additional consistency checks
        var characterPatternConsistency = CalculateCharacterPatternConsistency(data);
        var delimiterConsistency = CalculateDelimiterConsistency(data);
        
        // Combined consistency score
        return (lengthConsistency * 0.5) + (characterPatternConsistency * 0.3) + (delimiterConsistency * 0.2);
    }

    /// <summary>
    /// Calculate standard deviation for integer collections
    /// </summary>
    private double CalculateStandardDeviation(IList<int> values)
    {
        if (values.Count <= 1) return 0;
        
        var mean = values.Average();
        var squaredDifferences = values.Select(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(squaredDifferences.Average());
    }

    /// <summary>
    /// Calculate standard deviation for double collections
    /// </summary>
    private double CalculateStandardDeviation(IList<double> values)
    {
        if (values.Count <= 1) return 0;
        
        var mean = values.Average();
        var squaredDifferences = values.Select(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(squaredDifferences.Average());
    }

    #region Enhanced Confidence Scoring Methods

    /// <summary>
    /// Calculate length consistency score
    /// </summary>
    private double CalculateLengthConsistency(IList<int> lengths, double mean, double stdDev)
    {
        if (stdDev == 0) return 100.0; // Perfect consistency
        
        // Calculate coefficient of variation
        var cv = stdDev / mean;
        
        // Convert to percentage score (lower CV = higher score)
        return Math.Max(0, 100.0 - (cv * 50));
    }

    /// <summary>
    /// Calculate variation score based on standard deviation
    /// </summary>
    private double CalculateVariationScore(double stdDev, double mean)
    {
        if (mean == 0) return 0;
        
        var normalizedStdDev = stdDev / mean;
        
        // Score decreases as variation increases
        return Math.Max(0, 100.0 - (normalizedStdDev * 100));
    }

    /// <summary>
    /// Calculate uniformity score based on distribution
    /// </summary>
    private double CalculateUniformityScore(IList<int> values)
    {
        var groups = values.GroupBy(v => v).ToList();
        var mostCommonCount = groups.Max(g => g.Count());
        var uniformityRatio = (double)mostCommonCount / values.Count;
        
        // Higher ratio means better uniformity
        return uniformityRatio * 100;
    }

    /// <summary>
    /// Calculate individual frame match score against template
    /// </summary>
    private double CalculateFrameMatchScore(ProtocolTemplate template, string frameText)
    {
        var score = 0.0;
        var totalFields = template.Fields.Count;
        
        if (totalFields == 0) return 0;
        
        foreach (var field in template.Fields)
        {
            var parseResult = field.ParseValue(frameText);
            if (parseResult.Success)
            {
                score += field.IsRequired ? 1.0 : 0.5;
            }
            else if (field.IsRequired)
            {
                score -= 0.5; // Penalty for failed required fields
            }
        }
        
        return Math.Max(0, (score / totalFields) * 100);
    }

    /// <summary>
    /// Calculate consistency of score distribution
    /// </summary>
    private double CalculateScoreConsistency(IList<double> scores)
    {
        if (scores.Count <= 1) return 0;
        
        var stdDev = CalculateStandardDeviation(scores);
        var mean = scores.Average();
        
        if (mean == 0) return 0;
        
        // Lower variation = higher consistency
        var cv = stdDev / mean;
        return Math.Max(0, 10.0 - (cv * 10));
    }

    /// <summary>
    /// Calculate field completeness across parsed frames
    /// </summary>
    private double CalculateFieldCompleteness(IList<ParsedFrame> frames)
    {
        if (!frames.Any()) return 0;
        
        var totalFields = frames.First().FieldValues.Count;
        if (totalFields == 0) return 100;
        
        var completenessScores = frames.Select(frame =>
        {
            var nonNullFields = frame.FieldValues.Values.Count(v => v != null);
            return (double)nonNullFields / totalFields * 100;
        }).ToList();
        
        return completenessScores.Average();
    }

    /// <summary>
    /// Calculate data type consistency across parsed frames
    /// </summary>
    private double CalculateDataTypeConsistency(IList<ParsedFrame> frames)
    {
        if (!frames.Any()) return 100;
        
        var fieldNames = frames.First().FieldValues.Keys.ToList();
        var consistencyScores = new List<double>();
        
        foreach (var fieldName in fieldNames)
        {
            var fieldValues = frames
                .Select(f => f.FieldValues.GetValueOrDefault(fieldName))
                .Where(v => v != null)
                .ToList();
            
            if (!fieldValues.Any())
            {
                consistencyScores.Add(0);
                continue;
            }
            
            // Check type consistency
            var firstType = fieldValues.First()?.GetType();
            var sameTypeCount = fieldValues.Count(v => v?.GetType() == firstType);
            var typeConsistency = (double)sameTypeCount / fieldValues.Count * 100;
            
            consistencyScores.Add(typeConsistency);
        }
        
        return consistencyScores.Average();
    }

    /// <summary>
    /// Calculate value reasonableness for numeric fields
    /// </summary>
    private double CalculateValueReasonableness(IList<ParsedFrame> frames)
    {
        if (!frames.Any()) return 100;
        
        var reasonablenessScores = new List<double>();
        var fieldNames = frames.First().FieldValues.Keys.ToList();
        
        foreach (var fieldName in fieldNames)
        {
            var numericValues = frames
                .Select(f => f.FieldValues.GetValueOrDefault(fieldName))
                .OfType<double>()
                .ToList();
            
            if (!numericValues.Any())
            {
                reasonablenessScores.Add(100); // Non-numeric fields are reasonable by default
                continue;
            }
            
            // Check for reasonable ranges and outliers
            var mean = numericValues.Average();
            var stdDev = CalculateStandardDeviation(numericValues);
            
            var outlierCount = numericValues.Count(v => Math.Abs(v - mean) > 3 * stdDev);
            var outlierRatio = (double)outlierCount / numericValues.Count;
            
            // Lower outlier ratio = higher reasonableness
            var reasonableness = Math.Max(0, 100 - (outlierRatio * 100));
            reasonablenessScores.Add(reasonableness);
        }
        
        return reasonablenessScores.Average();
    }

    /// <summary>
    /// Calculate character pattern consistency
    /// </summary>
    private double CalculateCharacterPatternConsistency(IReadOnlyList<string> data)
    {
        if (data.Count <= 1) return 100;
        
        // Analyze character patterns at each position
        var maxLength = data.Max(d => d.Length);
        var positionScores = new List<double>();
        
        for (int pos = 0; pos < maxLength; pos++)
        {
            var charactersAtPosition = data
                .Where(d => d.Length > pos)
                .Select(d => d[pos])
                .ToList();
            
            if (!charactersAtPosition.Any()) continue;
            
            // Calculate character type consistency at this position
            var digitCount = charactersAtPosition.Count(char.IsDigit);
            var letterCount = charactersAtPosition.Count(char.IsLetter);
            var spaceCount = charactersAtPosition.Count(char.IsWhiteSpace);
            var punctCount = charactersAtPosition.Count(char.IsPunctuation);
            
            var total = charactersAtPosition.Count;
            var dominantTypeRatio = Math.Max(Math.Max(digitCount, letterCount), Math.Max(spaceCount, punctCount)) / (double)total;
            
            positionScores.Add(dominantTypeRatio * 100);
        }
        
        return positionScores.Any() ? positionScores.Average() : 100;
    }

    /// <summary>
    /// Calculate delimiter consistency
    /// </summary>
    private double CalculateDelimiterConsistency(IReadOnlyList<string> data)
    {
        if (data.Count <= 1) return 100;
        
        // Check for consistent ending patterns
        var endings = data.Select(d => d.Length > 0 ? d.Substring(Math.Max(0, d.Length - 2)) : "").ToList();
        var mostCommonEnding = endings.GroupBy(e => e).OrderByDescending(g => g.Count()).First();
        
        var consistencyRatio = (double)mostCommonEnding.Count() / data.Count;
        return consistencyRatio * 100;
    }

    #endregion

    #endregion

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ProtocolDiscoveryEngine));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        // Cancel and dispose all active sessions
        foreach (var session in _activeSessions.Values)
        {
            session.Cancel();
            session.Dispose();
        }
        
        _activeSessions.Clear();
        _logger.LogInformation("Protocol discovery engine disposed");
    }

    #endregion
}