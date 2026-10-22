namespace Hexalith.EventStore.DomainService;

/// <summary>One immutable chunk of an owner contribution to a partitioned shared rebuild.</summary>
/// <param name="Ordinal">The accepted inventory ordinal.</param>
/// <param name="AggregateId">The owner aggregate identifier.</param>
/// <param name="State">The handler-owned canonical contribution bytes.</param>
/// <param name="ChunkOrdinal">The chunk ordinal within this owner.</param>
public sealed record DomainSharedProjectionRebuildPartition(
    long Ordinal,
    string AggregateId,
    ReadOnlyMemory<byte> State,
    int ChunkOrdinal = 0);
