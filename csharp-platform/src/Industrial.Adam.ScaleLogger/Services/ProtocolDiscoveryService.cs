// Industrial.Adam.ScaleLogger - Protocol Discovery Service
// Simple template matching following proven patterns

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Services;

/// <summary>
/// Simple protocol discovery service using template matching
/// Following KISS principles - no complex correlation or signal analysis
/// </summary>
public sealed class ProtocolDiscoveryService : IDisposable
{
    private readonly ProtocolDiscoveryConfig _config;
    private readonly ILogger<ProtocolDiscoveryService> _logger;
    private readonly List<ProtocolTemplate> _templates = new();
    private volatile bool _disposed;

    public ProtocolDiscoveryService(ProtocolDiscoveryConfig config, ILogger<ProtocolDiscoveryService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadProtocolTemplates();
    }

    /// <summary>
    /// Discover working protocol for a scale device
    /// </summary>
    public async Task<ProtocolTemplate?> DiscoverProtocolAsync(string host, int port, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProtocolDiscoveryService));
        if (!_config.Enabled) return null;

        _logger.LogInformation("Starting protocol discovery for {Host}:{Port}", host, port);

        foreach (var template in _templates)
        {
            try
            {
                if (await TestProtocolAsync(host, port, template, cancellationToken))
                {
                    _logger.LogInformation("Discovered working protocol: {Protocol} for {Host}:{Port}", 
                        template.Name, host, port);
                    return template;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Protocol discovery cancelled for {Host}:{Port}", host, port);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Protocol {Protocol} failed for {Host}:{Port}", template.Name, host, port);
            }
        }

        _logger.LogWarning("No working protocol found for {Host}:{Port}", host, port);
        return null;
    }

    /// <summary>
    /// Test a specific protocol template against a scale
    /// </summary>
    private async Task<bool> TestProtocolAsync(string host, int port, ProtocolTemplate template, 
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var validReadings = 0;
        var totalAttempts = 0;

        _logger.LogDebug("Testing protocol {Protocol} for {Host}:{Port}", template.Name, host, port);

        for (int attempt = 0; attempt < _config.ValidationReadings; attempt++)
        {
            totalAttempts++;
            
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                using var stream = client.GetStream();

                // Send commands and read responses
                foreach (var command in template.Commands)
                {
                    var commandBytes = Encoding.ASCII.GetBytes(command + "\r\n");
                    await stream.WriteAsync(commandBytes, 0, commandBytes.Length, combined.Token);

                    // Read response
                    var buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, combined.Token);
                    var response = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                    // Check if response matches expected patterns
                    if (IsValidResponse(response, template))
                    {
                        validReadings++;
                        _logger.LogDebug("Valid response from {Protocol}: {Response}", template.Name, response);
                        break; // Success for this attempt
                    }
                }

                // Small delay between attempts
                await Task.Delay(500, combined.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Attempt {Attempt} failed for protocol {Protocol}", attempt + 1, template.Name);
            }
        }

        // Consider protocol working if we got at least 60% successful readings
        var successRate = (double)validReadings / totalAttempts;
        var success = successRate >= 0.6;

        _logger.LogDebug("Protocol {Protocol} test completed: {SuccessRate:P} success rate ({ValidReadings}/{TotalAttempts})",
            template.Name, successRate, validReadings, totalAttempts);

        return success;
    }

    /// <summary>
    /// Check if response matches expected patterns for the protocol
    /// </summary>
    private bool IsValidResponse(string response, ProtocolTemplate template)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;

        // Check against expected response patterns
        foreach (var expectedPattern in template.ExpectedResponses)
        {
            try
            {
                if (Regex.IsMatch(response, expectedPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Regex error checking pattern {Pattern}", expectedPattern);
            }
        }

        // Check if we can extract weight using the weight pattern
        try
        {
            var match = Regex.Match(response, template.WeightPattern);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var weight))
            {
                // Basic sanity check - weight should be reasonable
                return weight >= -1000 && weight <= 100000;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting weight from response using pattern {Pattern}", template.WeightPattern);
        }

        return false;
    }

    /// <summary>
    /// Load protocol templates from JSON files
    /// </summary>
    private void LoadProtocolTemplates()
    {
        try
        {
            var templatesPath = _config.TemplatesPath;
            if (!Directory.Exists(templatesPath))
            {
                _logger.LogWarning("Protocol templates directory not found: {TemplatesPath}", templatesPath);
                LoadBuiltInTemplates();
                return;
            }

            var templateFiles = Directory.GetFiles(templatesPath, "*.json");
            foreach (var file in templateFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var template = JsonSerializer.Deserialize<ProtocolTemplate>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (template != null)
                    {
                        _templates.Add(template);
                        _logger.LogDebug("Loaded protocol template: {TemplateName} from {File}", template.Name, file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load protocol template from {File}", file);
                }
            }

            if (!_templates.Any())
            {
                _logger.LogWarning("No protocol templates loaded from files, using built-in templates");
                LoadBuiltInTemplates();
            }
            else
            {
                _logger.LogInformation("Loaded {TemplateCount} protocol templates", _templates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading protocol templates, using built-in templates");
            LoadBuiltInTemplates();
        }
    }

    /// <summary>
    /// Load basic built-in protocol templates
    /// </summary>
    private void LoadBuiltInTemplates()
    {
        // Basic ADAM-4571 template
        _templates.Add(new ProtocolTemplate
        {
            Id = "adam-4571-basic",
            Name = "ADAM-4571 Basic",
            Manufacturer = "Advantech",
            Model = "ADAM-4571",
            Description = "Basic weight reading for ADAM-4571",
            Commands = new List<string> { "W", "#01" },
            ExpectedResponses = new List<string> { @"[\+\-]?\d+\.?\d*", @">\+?\d+\.?\d*" },
            WeightPattern = @"([\+\-]?\d+\.?\d*)",
            Unit = "kg"
        });

        // Generic scale template
        _templates.Add(new ProtocolTemplate
        {
            Id = "generic-scale",
            Name = "Generic Scale",
            Manufacturer = "Generic",
            Description = "Generic scale protocol",
            Commands = new List<string> { "W\r\n", "P\r\n", "?\r\n" },
            ExpectedResponses = new List<string> { @"\d+\.?\d*", @"[\+\-]?\d+\.?\d*\s*\w*" },
            WeightPattern = @"([\+\-]?\d+\.?\d*)",
            Unit = "kg"
        });

        _logger.LogInformation("Loaded {TemplateCount} built-in protocol templates", _templates.Count);
    }

    /// <summary>
    /// Get all available protocol templates
    /// </summary>
    public IReadOnlyList<ProtocolTemplate> GetAvailableTemplates()
    {
        return _templates.AsReadOnly();
    }

    /// <summary>
    /// Add a custom protocol template
    /// </summary>
    public void AddTemplate(ProtocolTemplate template)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProtocolDiscoveryService));
        
        _templates.Add(template);
        _logger.LogInformation("Added custom protocol template: {TemplateName}", template.Name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _templates.Clear();
    }
}