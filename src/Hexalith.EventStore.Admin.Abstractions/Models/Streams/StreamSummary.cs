namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Summary information about an event stream.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="LastEventSequence">The sequence number of the most recent event.</param>
/// <param name="LastActivityUtc">The timestamp of the most recent activity.</param>
/// <param name="EventCount">The total number of events in the stream.</param>
/// <param name="HasSnapshot">Whether a snapshot exists for this stream.</param>
/// <param name="StreamStatus">The current status of the stream.</param>
public record StreamSummary(
    string TenantId,
    string Domain,
    string AggregateId,
    long LastEventSequence,
    DateTimeOffset LastActivityUtc,
    long EventCount,
    bool HasSnapshot,
    StreamStatus StreamStatus)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = !string.IsNullOrWhiteSpace(AggregateId)
        ? AggregateId
        : throw new ArgumentException("AggregateId cannot be null, empty, or whitespace.", nameof(AggregateId));
}
