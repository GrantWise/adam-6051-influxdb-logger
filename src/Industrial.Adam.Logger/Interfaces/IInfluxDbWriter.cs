// Industrial.Adam.Logger - InfluxDB Writer Interface
// Interface for writing ADAM data to InfluxDB

using Industrial.Adam.Logger.Models;

namespace Industrial.Adam.Logger.Interfaces;

/// <summary>
/// Interface for writing ADAM data to InfluxDB
/// </summary>
public interface IInfluxDbWriter : IDisposable
{
    /// <summary>
    /// Write a single data reading to InfluxDB
    /// </summary>
    /// <param name="reading">The data reading to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteAsync(AdamDataReading reading, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write multiple data readings to InfluxDB
    /// </summary>
    /// <param name="readings">The data readings to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteBatchAsync(IEnumerable<AdamDataReading> readings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flush any pending writes to InfluxDB
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the flush operation</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if InfluxDB is accessible
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if InfluxDB is accessible, false otherwise</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}