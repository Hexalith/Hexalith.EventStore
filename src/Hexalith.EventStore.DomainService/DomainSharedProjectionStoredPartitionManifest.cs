namespace Hexalith.EventStore.DomainService;

/// <summary>Durable owner manifest written only after every contribution chunk is immutable.</summary>
/// <param name="Ordinal">The accepted owner inventory ordinal.</param>
/// <param name="AggregateId">The authoritative owner identifier.</param>
/// <param name="HistoryFingerprint">The captured history fingerprint.</param>
/// <param name="IsErased">Whether the accepted owner has no materialized contribution.</param>
/// <param name="ChunkHashes">The ordered content hashes of the owner's chunks.</param>
/// <param name="LastSequenceNumber">The captured final owner sequence.</param>
/// <param name="CapturedEventDigests">SHA-256 identities of each captured owner event envelope and payload.</param>
/// <param name="TransportPartCount">The private bounded input parts to reclaim after a terminal session.</param>
internal sealed record DomainSharedProjectionStoredPartitionManifest(
    long Ordinal,
    string AggregateId,
    string HistoryFingerprint,
    bool IsErased,
    string[] ChunkHashes,
    long LastSequenceNumber = 0,
    byte[]? CapturedEventDigests = null,
    int TransportPartCount = 0,
    int StreamedChunkCount = 0,
    string[]? ChunkHashPageDigests = null)
{
    internal int MaterializedChunkCount => StreamedChunkCount > 0 ? StreamedChunkCount : ChunkHashes.Length;
}
