namespace Hexalith.EventStore.DomainService;

/// <summary>Private durable contribution chunk bound to one source-history fingerprint.</summary>
/// <param name="Ordinal">The accepted owner inventory ordinal.</param>
/// <param name="ChunkOrdinal">The chunk ordinal for that owner.</param>
/// <param name="AggregateId">The authoritative owner identifier.</param>
/// <param name="HistoryFingerprint">The captured history fingerprint.</param>
/// <param name="State">The immutable contribution bytes.</param>
internal sealed record DomainSharedProjectionStoredPartition(
    long Ordinal,
    int ChunkOrdinal,
    string AggregateId,
    string HistoryFingerprint,
    byte[] State);
