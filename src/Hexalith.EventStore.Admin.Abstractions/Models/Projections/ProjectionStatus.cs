namespace Hexalith.EventStore.Admin.Abstractions.Models.Projections;

/// <summary>
/// Status information about a projection.
/// </summary>
/// <param name="Name">The projection name.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Status">The current projection status.</param>
/// <param name="Lag">The number of events behind the current position.</param>
/// <param name="Throughput">The current processing throughput in events per second.</param>
/// <param name="ErrorCount">The total number of errors encountered.</param>
/// <param name="LastProcessedPosition">The last successfully processed event position.</param>
/// <param name="LastProcessedUtc">When the last event was successfully processed.</param>
public record ProjectionStatus(
    string Name,
    string TenantId,
    ProjectionStatusType Status,
    long Lag,
    double Throughput,
    int ErrorCount,
    long LastProcessedPosition,
    DateTimeOffset LastProcessedUtc)
{
    /// <summary>Gets the projection name.</summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name)
        ? Name
        : throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(Name));

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the current processing throughput in events per second.</summary>
    public double Throughput { get; } = !double.IsNaN(Throughput) && !double.IsInfinity(Throughput)
        ? Throughput
        : throw new ArgumentOutOfRangeException(nameof(Throughput), Throughput, "Value must be a finite number.");
}
