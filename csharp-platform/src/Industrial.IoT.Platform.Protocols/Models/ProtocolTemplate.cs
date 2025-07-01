// Industrial.IoT.Platform.Protocols - Protocol Template Models
// Core data structures for protocol discovery and template management

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Industrial.IoT.Platform.Protocols.Models;

/// <summary>
/// Represents a discovered or predefined scale protocol template
/// Direct port from Python ProtocolTemplate with C# enhancements
/// </summary>
public sealed record ProtocolTemplate
{
    /// <summary>
    /// Unique identifier for this template
    /// </summary>
    [Required]
    public required string TemplateId { get; init; }

    /// <summary>
    /// Human-readable template name
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Description of the protocol and manufacturer
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Data frame delimiter (e.g., "\r\n", "\n", "\r")
    /// </summary>
    [Required]
    public required string Delimiter { get; init; }

    /// <summary>
    /// Text encoding for data frames (e.g., "ASCII", "UTF-8")
    /// </summary>
    public string Encoding { get; init; } = "ASCII";

    /// <summary>
    /// Ordered list of data fields in the protocol
    /// </summary>
    [Required]
    public required IReadOnlyList<ProtocolField> Fields { get; init; }

    /// <summary>
    /// Confidence score for this template (0-100)
    /// </summary>
    [Range(0.0, 100.0)]
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// When this template was discovered or last validated
    /// </summary>
    public DateTime DiscoveryDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Manufacturer identification (e.g., "Mettler Toledo", "Ohaus")
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Model pattern this template applies to
    /// </summary>
    public string? ModelPattern { get; init; }

    /// <summary>
    /// Template validation statistics
    /// </summary>
    public TemplateValidationStats? ValidationStats { get; init; }

    /// <summary>
    /// Validate template structure and field definitions
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(TemplateId))
            errors.Add("TemplateId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Delimiter))
            errors.Add("Delimiter is required");

        if (!Fields.Any())
            errors.Add("At least one field is required");

        // Validate field positions don't overlap
        var sortedFields = Fields.OrderBy(f => f.Start).ToList();
        for (int i = 0; i < sortedFields.Count - 1; i++)
        {
            var current = sortedFields[i];
            var next = sortedFields[i + 1];
            
            if (current.Start + current.Length > next.Start)
            {
                errors.Add($"Fields '{current.Name}' and '{next.Name}' have overlapping positions");
            }
        }

        // Validate individual fields
        foreach (var field in Fields)
        {
            errors.AddRange(field.Validate());
        }

        return errors;
    }

    /// <summary>
    /// Create a copy with updated confidence score
    /// </summary>
    /// <param name="newConfidence">New confidence score</param>
    /// <param name="validationStats">Optional validation statistics</param>
    /// <returns>Updated template</returns>
    public ProtocolTemplate WithConfidence(double newConfidence, TemplateValidationStats? validationStats = null)
    {
        return this with 
        { 
            ConfidenceScore = newConfidence,
            ValidationStats = validationStats ?? ValidationStats,
            DiscoveryDate = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Defines a single field within a protocol template
/// Enhanced version of Python ProtocolField with strong typing
/// </summary>
public sealed record ProtocolField
{
    /// <summary>
    /// Field name (e.g., "Weight", "Unit", "Stability")
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Starting character position (0-based)
    /// </summary>
    [Range(0, int.MaxValue)]
    public required int Start { get; init; }

    /// <summary>
    /// Field length in characters
    /// </summary>
    [Range(1, int.MaxValue)]
    public required int Length { get; init; }

    /// <summary>
    /// Field data type
    /// </summary>
    public required FieldType FieldType { get; init; }

    /// <summary>
    /// Number of decimal places for numeric fields
    /// </summary>
    [Range(0, 10)]
    public int? DecimalPlaces { get; init; }

    /// <summary>
    /// Lookup values for enumerated fields
    /// Key = raw value, Value = interpreted value
    /// </summary>
    public IReadOnlyDictionary<string, string>? LookupValues { get; init; }

    /// <summary>
    /// Regular expression pattern for field validation
    /// </summary>
    public string? ValidationPattern { get; init; }

    /// <summary>
    /// Whether this field is required for successful parsing
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    /// Field description for documentation
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Validate field definition
    /// </summary>
    /// <returns>List of validation errors</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add($"Field name is required");

        if (Start < 0)
            errors.Add($"Field '{Name}' start position cannot be negative");

        if (Length <= 0)
            errors.Add($"Field '{Name}' length must be positive");

        if (FieldType == FieldType.Numeric && DecimalPlaces.HasValue && DecimalPlaces < 0)
            errors.Add($"Field '{Name}' decimal places cannot be negative");

        if (FieldType == FieldType.Lookup && (LookupValues == null || !LookupValues.Any()))
            errors.Add($"Lookup field '{Name}' must have lookup values defined");

        return errors;
    }

    /// <summary>
    /// Extract and parse field value from data frame
    /// </summary>
    /// <param name="dataFrame">Raw data frame</param>
    /// <returns>Parsed field result</returns>
    public FieldParseResult ParseValue(string dataFrame)
    {
        try
        {
            // Check bounds
            if (Start >= dataFrame.Length)
                return FieldParseResult.CreateFailed($"Start position {Start} exceeds frame length {dataFrame.Length}");

            var endPos = Math.Min(Start + Length, dataFrame.Length);
            var rawValue = dataFrame.Substring(Start, endPos - Start).Trim();

            if (string.IsNullOrEmpty(rawValue) && IsRequired)
                return FieldParseResult.CreateFailed("Required field is empty");

            return FieldType switch
            {
                FieldType.Numeric => ParseNumericValue(rawValue),
                FieldType.Lookup => ParseLookupValue(rawValue),
                FieldType.Text => FieldParseResult.CreateSuccess(rawValue, rawValue),
                _ => FieldParseResult.CreateFailed($"Unknown field type: {FieldType}")
            };
        }
        catch (Exception ex)
        {
            return FieldParseResult.CreateFailed($"Parse error: {ex.Message}");
        }
    }

    private FieldParseResult ParseNumericValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return IsRequired ? FieldParseResult.CreateFailed("Required numeric field is empty") : FieldParseResult.CreateSuccess(null, rawValue);

        // Remove common non-numeric characters
        var cleanValue = rawValue.Replace(" ", "").Replace("+", "");
        
        if (double.TryParse(cleanValue, out var numericValue))
        {
            // Apply decimal places if specified
            if (DecimalPlaces.HasValue)
                numericValue = Math.Round(numericValue, DecimalPlaces.Value);

            return FieldParseResult.CreateSuccess(numericValue, rawValue);
        }

        return FieldParseResult.CreateFailed($"Cannot parse '{rawValue}' as numeric");
    }

    private FieldParseResult ParseLookupValue(string rawValue)
    {
        if (LookupValues == null)
            return FieldParseResult.CreateFailed("Lookup values not defined");

        if (string.IsNullOrWhiteSpace(rawValue))
            return IsRequired ? FieldParseResult.CreateFailed("Required lookup field is empty") : FieldParseResult.CreateSuccess(null, rawValue);

        // Try exact match first
        if (LookupValues.TryGetValue(rawValue, out var exactMatch))
            return FieldParseResult.CreateSuccess(exactMatch, rawValue);

        // Try trimmed match
        var trimmedValue = rawValue.Trim();
        if (LookupValues.TryGetValue(trimmedValue, out var trimmedMatch))
            return FieldParseResult.CreateSuccess(trimmedMatch, rawValue);

        // For non-required fields, return raw value if no match
        if (!IsRequired)
            return FieldParseResult.CreateSuccess(rawValue, rawValue);

        var validValues = string.Join(", ", LookupValues.Keys);
        return FieldParseResult.CreateFailed($"'{rawValue}' not found in lookup values: {validValues}");
    }
}

/// <summary>
/// Field data types supported by the protocol engine
/// </summary>
public enum FieldType
{
    /// <summary>
    /// Numeric value (integer or decimal)
    /// </summary>
    Numeric,

    /// <summary>
    /// Lookup/enumerated value
    /// </summary>
    Lookup,

    /// <summary>
    /// Free-form text
    /// </summary>
    Text
}

/// <summary>
/// Result of parsing a field value from a data frame
/// </summary>
public sealed record FieldParseResult
{
    /// <summary>
    /// Whether parsing was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed value (typed based on field type)
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Original raw value from data frame
    /// </summary>
    public string RawValue { get; init; } = string.Empty;

    /// <summary>
    /// Error message if parsing failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Create successful parse result
    /// </summary>
    public static FieldParseResult CreateSuccess(object? value, string rawValue) => new()
    {
        Success = true,
        Value = value,
        RawValue = rawValue
    };

    /// <summary>
    /// Create failed parse result
    /// </summary>
    public static FieldParseResult CreateFailed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Statistics from template validation process
/// Improves on Python version with detailed metrics
/// </summary>
public sealed record TemplateValidationStats
{
    /// <summary>
    /// Total number of data frames tested
    /// </summary>
    public int TotalFrames { get; init; }

    /// <summary>
    /// Number of frames successfully parsed
    /// </summary>
    public int SuccessfulParses { get; init; }

    /// <summary>
    /// Parse success rate (0-100)
    /// </summary>
    public double ParseSuccessRate => TotalFrames > 0 ? (double)SuccessfulParses / TotalFrames * 100 : 0;

    /// <summary>
    /// Frame length consistency score (0-100)
    /// </summary>
    public double FrameConsistency { get; init; }

    /// <summary>
    /// Format pattern match score (0-100)
    /// </summary>
    public double FormatMatch { get; init; }

    /// <summary>
    /// Data quality assessment (0-100)
    /// </summary>
    public double DataQuality { get; init; }

    /// <summary>
    /// Average frame length
    /// </summary>
    public double AverageFrameLength { get; init; }

    /// <summary>
    /// Frame length standard deviation
    /// </summary>
    public double FrameLengthStdDev { get; init; }

    /// <summary>
    /// Validation duration
    /// </summary>
    public TimeSpan ValidationDuration { get; init; }

    /// <summary>
    /// Calculate overall confidence score using weighted factors
    /// Same algorithm as Python version with enhanced metrics
    /// </summary>
    public double CalculateConfidence()
    {
        // Python algorithm: parse_success_rate * 0.6 + data_consistency * 0.2 + format_match * 0.2
        var dataConsistency = (FrameConsistency + DataQuality) / 2;
        return ParseSuccessRate * 0.6 + dataConsistency * 0.2 + FormatMatch * 0.2;
    }
}