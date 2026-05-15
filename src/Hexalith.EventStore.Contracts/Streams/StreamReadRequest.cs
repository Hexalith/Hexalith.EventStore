namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public downstream request for reading a tenant-scoped EventStore stream page.
/// </summary>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The domain identifier.</param>
/// <param name="AggregateId">Optional aggregate identifier. Omit only for domain-wide rebuild reads.</param>
/// <param name="FromSequence">The exclusive lower sequence bound. Defaults to 0.</param>
/// <param name="ToSequence">Optional inclusive upper sequence bound.</param>
/// <param name="Checkpoint">Optional projection rebuild checkpoint used as the read cursor.</param>
/// <param name="ContinuationToken">Optional opaque continuation token from a previous page.</param>
/// <param name="PageSize">Maximum number of events to return.</param>
/// <param name="ProjectionName">Optional projection/rebuild scope for domain-wide reads.</param>
public sealed record StreamReadRequest(
    string Tenant,
    string Domain,
    string? AggregateId = null,
    long FromSequence = 0,
    long? ToSequence = null,
    ProjectionRebuildCheckpoint? Checkpoint = null,
    ReplayContinuationToken? ContinuationToken = null,
    int PageSize = 100,
    string? ProjectionName = null);
