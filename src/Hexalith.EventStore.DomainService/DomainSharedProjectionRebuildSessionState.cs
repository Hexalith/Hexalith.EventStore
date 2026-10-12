namespace Hexalith.EventStore.DomainService;

/// <summary>Durable private state for one shared-projection rebuild session.</summary>
internal sealed record DomainSharedProjectionRebuildSessionState(
    int Version,
    DomainSharedProjectionRebuildIdentity Identity,
    DomainSharedProjectionRebuildPhase Phase,
    byte[] CandidateState,
    long AcceptedAggregateCount,
    string InventoryFingerprint,
    string? LastAggregateId,
    DomainSharedProjectionRebuildReceipt[] Receipts,
    long? ExpectedAggregateCount,
    string? ExpectedInventoryFingerprint,
    string? BatchFingerprint,
    byte[]? CompletionState,
    string? CaptureInventoryFingerprint = null,
    bool Partitioned = false,
    bool ScratchIndexesCleaned = false,
    long ScratchCleanedOwnerCount = 0,
    int PendingScratchChunkCount = -1,
    long TransportOwnerOrdinal = -1,
    string? TransportAggregateId = null,
    int TransportNextPart = 0,
    int TransportReservedPartCount = 0,
    int TransportReservedFragmentCount = 0);
