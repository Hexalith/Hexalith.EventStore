namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Storage usage information for a single event stream.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name, enabling grouping by type for treemap views (Journey 8).</param>
/// <param name="EventCount">The number of events in the stream.</param>
/// <param name="SizeBytes">The storage size in bytes, or null if backend-dependent (NFR44).</param>
/// <param name="HasSnapshot">Whether a snapshot exists for this stream.</param>
/// <param name="SnapshotAge">The age of the most recent snapshot, or null if no snapshot exists.</param>
public record StreamStorageInfo(
    string TenantId,
    string Domain,
    string AggregateId,
    string AggregateType,
    long EventCount,
    long? SizeBytes,
    bool HasSnapshot,
    TimeSpan? SnapshotAge) {
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

    /// <summary>Gets the aggregate type name.</summary>
    public string AggregateType { get; } = !string.IsNullOrWhiteSpace(AggregateType)
        ? AggregateType
        : throw new ArgumentException("AggregateType cannot be null, empty, or whitespace.", nameof(AggregateType));
}
