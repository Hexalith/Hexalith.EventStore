namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures a projection's processing status relative to the trace events.
/// </summary>
/// <param name="ProjectionName">The projection's registered name.</param>
/// <param name="Status">The projection status: "processed", "pending", "faulted", or "unknown".</param>
/// <param name="LastProcessedSequence">The projection's last processed sequence number, null if unknown.</param>
public record TraceMapProjection(
    string ProjectionName,
    string Status,
    long? LastProcessedSequence)
{
    /// <summary>Gets the projection's registered name.</summary>
    public string ProjectionName { get; } = ProjectionName ?? string.Empty;

    /// <summary>Gets the projection status.</summary>
    public string Status { get; } = Status ?? string.Empty;

    /// <summary>
    /// Returns a string representation.
    /// </summary>
    public override string ToString()
        => $"TraceMapProjection {{ ProjectionName = {ProjectionName}, Status = {Status}, LastProcessedSequence = {LastProcessedSequence?.ToString() ?? "(unknown)"} }}";
}
