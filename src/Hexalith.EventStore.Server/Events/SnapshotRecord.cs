namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Immutable record representing a snapshot of aggregate state at a specific event sequence.
/// Stored via IActorStateManager at key <c>{tenant}:{domain}:{aggId}:snapshot</c>.
/// </summary>
/// <param name="SequenceNumber">The event sequence number this snapshot was taken at.</param>
/// <param name="State">The serialized aggregate state (domain-specific, opaque to EventStore).</param>
/// <param name="CreatedAt">When the snapshot was created.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
public record SnapshotRecord(
    long SequenceNumber,
    object State,
    DateTimeOffset CreatedAt,
    string Domain,
    string AggregateId,
    string TenantId);
