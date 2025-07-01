// Industrial.IoT.Platform.Protocols - Template Repository
// Repository for managing protocol templates with JSON persistence

using System.Text.Json;
using Industrial.IoT.Platform.Protocols.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Protocols.Services;

/// <summary>
/// Repository for managing protocol templates
/// Handles loading, saving, and caching of protocol templates
/// </summary>
public sealed class TemplateRepository : IDisposable
{
    private readonly ILogger<TemplateRepository> _logger;
    private readonly Dictionary<string, ProtocolTemplate> _templateCache = new();
    private readonly object _cacheLock = new();
    private readonly string _templatesDirectory;
    private volatile bool _isDisposed;

    /// <summary>
    /// Initialize template repository
    /// </summary>
    /// <param name="logger">Logging service</param>
    /// <param name="templatesDirectory">Directory containing template files</param>
    public TemplateRepository(ILogger<TemplateRepository> logger, string? templatesDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templatesDirectory = templatesDirectory ?? Path.Combine(Path.GetDirectoryName(typeof(TemplateRepository).Assembly.Location) ?? AppContext.BaseDirectory, "Templates");
        
        // Ensure templates directory exists
        if (!Directory.Exists(_templatesDirectory))
        {
            Directory.CreateDirectory(_templatesDirectory);
            _logger.LogInformation("Created templates directory: {Directory}", _templatesDirectory);
        }
        
        _logger.LogInformation("Template repository initialized with directory: {Directory}", _templatesDirectory);
    }

    /// <summary>
    /// Get all available protocol templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of protocol templates</returns>
    public async Task<IReadOnlyList<ProtocolTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check if templates are cached
        lock (_cacheLock)
        {
            if (_templateCache.Any())
            {
                return _templateCache.Values.ToList();
            }
        }

        // Load templates from disk
        await LoadTemplatesAsync(cancellationToken);

        lock (_cacheLock)
        {
            return _templateCache.Values.ToList();
        }
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Protocol template if found</returns>
    public async Task<ProtocolTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(templateId))
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

        // Check cache first
        lock (_cacheLock)
        {
            if (_templateCache.TryGetValue(templateId, out var cachedTemplate))
                return cachedTemplate;
        }

        // Load templates if not cached
        await LoadTemplatesAsync(cancellationToken);

        lock (_cacheLock)
        {
            _templateCache.TryGetValue(templateId, out var template);
            return template;
        }
    }

    /// <summary>
    /// Save protocol template to repository
    /// </summary>
    /// <param name="template">Template to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the save operation</returns>
    public async Task SaveTemplateAsync(ProtocolTemplate template, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (template == null) throw new ArgumentNullException(nameof(template));

        // Validate template before saving
        var validationErrors = template.Validate();
        if (validationErrors.Any())
        {
            var errorMessage = string.Join(", ", validationErrors);
            throw new InvalidOperationException($"Template validation failed: {errorMessage}");
        }

        var fileName = $"{template.TemplateId}.json";
        var filePath = Path.Combine(_templatesDirectory, fileName);

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(template, jsonOptions);
            await File.WriteAllTextAsync(filePath, jsonContent, cancellationToken);

            // Update cache
            lock (_cacheLock)
            {
                _templateCache[template.TemplateId] = template;
            }

            _logger.LogInformation("Saved template {TemplateId} to {FilePath}", template.TemplateId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template {TemplateId} to {FilePath}", template.TemplateId, filePath);
            throw;
        }
    }

    /// <summary>
    /// Delete template from repository
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if template was deleted</returns>
    public async Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(templateId))
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

        var fileName = $"{templateId}.json";
        var filePath = Path.Combine(_templatesDirectory, fileName);

        try
        {
            if (!File.Exists(filePath))
                return false;

            await Task.Run(() => File.Delete(filePath), cancellationToken);

            // Remove from cache
            lock (_cacheLock)
            {
                _templateCache.Remove(templateId);
            }

            _logger.LogInformation("Deleted template {TemplateId} from {FilePath}", templateId, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete template {TemplateId} from {FilePath}", templateId, filePath);
            throw;
        }
    }

    /// <summary>
    /// Reload templates from disk
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the reload operation</returns>
    public async Task ReloadTemplatesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_cacheLock)
        {
            _templateCache.Clear();
        }

        await LoadTemplatesAsync(cancellationToken);
        _logger.LogInformation("Reloaded templates from disk");
    }

    /// <summary>
    /// Load templates from disk into cache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the load operation</returns>
    private async Task LoadTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_templatesDirectory))
        {
            _logger.LogWarning("Templates directory does not exist: {Directory}", _templatesDirectory);
            return;
        }

        var jsonFiles = Directory.GetFiles(_templatesDirectory, "*.json");
        var loadedTemplates = new Dictionary<string, ProtocolTemplate>();

        foreach (var filePath in jsonFiles)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                var template = DeserializeTemplate(jsonContent, filePath);

                if (template != null)
                {
                    loadedTemplates[template.TemplateId] = template;
                    _logger.LogDebug("Loaded template {TemplateId} from {FilePath}", template.TemplateId, filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load template from {FilePath}", filePath);
            }
        }

        lock (_cacheLock)
        {
            _templateCache.Clear();
            foreach (var kvp in loadedTemplates)
            {
                _templateCache[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation("Loaded {Count} templates from {Directory}", loadedTemplates.Count, _templatesDirectory);
    }

    /// <summary>
    /// Deserialize protocol template from JSON
    /// </summary>
    /// <param name="jsonContent">JSON content</param>
    /// <param name="filePath">File path for error reporting</param>
    /// <returns>Deserialized template or null if failed</returns>
    private ProtocolTemplate? DeserializeTemplate(string jsonContent, string filePath)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            // First deserialize to a dynamic object to handle different JSON formats
            var jsonDocument = JsonDocument.Parse(jsonContent);
            var template = ConvertJsonToTemplate(jsonDocument.RootElement);

            // Validate the loaded template
            var validationErrors = template.Validate();
            if (validationErrors.Any())
            {
                _logger.LogWarning("Template validation failed for {FilePath}: {Errors}", 
                    filePath, string.Join(", ", validationErrors));
                return null;
            }

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize template from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Convert JSON element to protocol template
    /// Handles conversion from Python JSON format to C# objects
    /// </summary>
    /// <param name="jsonElement">JSON element</param>
    /// <returns>Protocol template</returns>
    private ProtocolTemplate ConvertJsonToTemplate(JsonElement jsonElement)
    {
        var templateId = jsonElement.GetProperty("templateId").GetString() ??
                        jsonElement.GetProperty("template_id").GetString() ??
                        throw new InvalidOperationException("Template ID is required");

        var name = jsonElement.GetProperty("name").GetString() ?? "Unknown Template";
        var description = GetStringProperty(jsonElement, "description") ?? "";
        var delimiter = jsonElement.GetProperty("delimiter").GetString() ?? "\r\n";
        var encoding = GetStringProperty(jsonElement, "encoding") ?? "ASCII";

        // Handle fields array
        var fieldsJson = jsonElement.GetProperty("fields");
        var fields = new List<ProtocolField>();

        foreach (var fieldJson in fieldsJson.EnumerateArray())
        {
            var field = ConvertJsonToField(fieldJson);
            fields.Add(field);
        }

        var confidenceScore = GetDoubleProperty(jsonElement, "confidenceScore") ??
                             GetDoubleProperty(jsonElement, "confidence_score") ?? 0.0;

        var discoveryDate = GetDateTimeProperty(jsonElement, "discoveryDate") ??
                           GetDateTimeProperty(jsonElement, "discovery_date") ?? DateTime.UtcNow;

        return new ProtocolTemplate
        {
            TemplateId = templateId,
            Name = name,
            Description = description,
            Delimiter = delimiter,
            Encoding = encoding,
            Fields = fields,
            ConfidenceScore = confidenceScore,
            DiscoveryDate = discoveryDate,
            Manufacturer = GetStringProperty(jsonElement, "manufacturer"),
            ModelPattern = GetStringProperty(jsonElement, "modelPattern") ?? GetStringProperty(jsonElement, "model_pattern")
        };
    }

    /// <summary>
    /// Convert JSON element to protocol field
    /// </summary>
    /// <param name="jsonElement">JSON element</param>
    /// <returns>Protocol field</returns>
    private ProtocolField ConvertJsonToField(JsonElement jsonElement)
    {
        var name = jsonElement.GetProperty("name").GetString() ?? throw new InvalidOperationException("Field name is required");
        var start = jsonElement.GetProperty("start").GetInt32();
        var length = jsonElement.GetProperty("length").GetInt32();

        var fieldTypeString = jsonElement.GetProperty("fieldType").GetString() ??
                             jsonElement.GetProperty("field_type").GetString() ?? "text";

        var fieldType = fieldTypeString.ToLowerInvariant() switch
        {
            "numeric" => FieldType.Numeric,
            "lookup" => FieldType.Lookup,
            "text" => FieldType.Text,
            _ => FieldType.Text
        };

        var decimalPlaces = GetIntProperty(jsonElement, "decimalPlaces") ?? GetIntProperty(jsonElement, "decimal_places");

        // Handle lookup values
        Dictionary<string, string>? lookupValues = null;
        if (jsonElement.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Object)
        {
            lookupValues = new Dictionary<string, string>();
            foreach (var property in valuesElement.EnumerateObject())
            {
                lookupValues[property.Name] = property.Value.GetString() ?? "";
            }
        }

        return new ProtocolField
        {
            Name = name,
            Start = start,
            Length = length,
            FieldType = fieldType,
            DecimalPlaces = decimalPlaces,
            LookupValues = lookupValues,
            ValidationPattern = GetStringProperty(jsonElement, "validationPattern") ?? GetStringProperty(jsonElement, "validation_pattern"),
            IsRequired = GetBoolProperty(jsonElement, "isRequired") ?? GetBoolProperty(jsonElement, "is_required") ?? true,
            Description = GetStringProperty(jsonElement, "description")
        };
    }

    #region Helper Methods

    private string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    private double? GetDoubleProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetDouble() : null;
    }

    private int? GetIntProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetInt32() : null;
    }

    private bool? GetBoolProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetBoolean() : null;
    }

    private DateTime? GetDateTimeProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var dateString = prop.GetString();
            if (DateTime.TryParse(dateString, out var date))
                return date;
        }
        return null;
    }

    #endregion

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(TemplateRepository));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        
        lock (_cacheLock)
        {
            _templateCache.Clear();
        }
        
        _logger.LogInformation("Template repository disposed");
    }
}