// Industrial.IoT.Platform.Core - Scale Data Reading Interface
// Interface for scale data readings from ADAM-4571 devices following existing ADAM logger patterns

using Industrial.IoT.Platform.Core.Interfaces;

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Interface for scale data readings from ADAM-4571 devices
/// Extends base data reading with scale-specific properties
/// Following existing ADAM logger data model patterns for consistency
/// </summary>
public interface IScaleDataReading : IDataReading
{
    /// <summary>
    /// Serial port channel on the ADAM-4571 device (typically 1-8)
    /// </summary>
    int Channel { get; }

    /// <summary>
    /// Weight measurement value (in the scale's native unit)
    /// </summary>
    double WeightValue { get; }

    /// <summary>
    /// Raw weight value as received from the scale device
    /// </summary>
    string RawValue { get; }

    /// <summary>
    /// Scale unit of measurement (kg, lb, g, oz, etc.)
    /// </summary>
    string Unit { get; }

    /// <summary>
    /// Scale status flags (stable, unstable, overload, underload, etc.)
    /// </summary>
    string? Status { get; }

    /// <summary>
    /// Signal stability score (0-100) for RS232 connection quality
    /// </summary>
    double? StabilityScore { get; }

    /// <summary>
    /// Scale manufacturer (Mettler Toledo, Sartorius, etc.)
    /// </summary>
    string? Manufacturer { get; }

    /// <summary>
    /// Scale model identifier
    /// </summary>
    string? Model { get; }

    /// <summary>
    /// Serial number of the connected scale
    /// </summary>
    string? SerialNumber { get; }

    /// <summary>
    /// Protocol template used for communication
    /// </summary>
    string? ProtocolTemplate { get; }

    /// <summary>
    /// Additional metadata for extensibility
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Weight in standardized unit (always kg for consistency)
    /// </summary>
    decimal StandardizedWeightKg { get; }

    /// <summary>
    /// Indicates if this is a stable reading based on scale status
    /// </summary>
    bool IsStable { get; }

    /// <summary>
    /// Indicates if the reading represents an error condition
    /// </summary>
    bool IsError { get; }
}