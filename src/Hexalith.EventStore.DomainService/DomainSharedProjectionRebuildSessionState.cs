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
    byte[]? CompletionState);
