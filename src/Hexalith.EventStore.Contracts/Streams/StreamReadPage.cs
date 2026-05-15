namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public page of stream events returned for downstream replay/rebuild use.
/// </summary>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The domain identifier.</param>
/// <param name="AggregateId">Optional aggregate identifier.</param>
/// <param name="Events">The stream events ordered by sequence number.</param>
/// <param name="Metadata">The read metadata and continuation details.</param>
public sealed record StreamReadPage(
    string Tenant,
    string Domain,
    string? AggregateId,
    IReadOnlyList<StreamReadEvent> Events,
    StreamReadMetadata Metadata);
