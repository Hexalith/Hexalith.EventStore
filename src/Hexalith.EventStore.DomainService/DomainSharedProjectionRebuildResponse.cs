using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Bounded durable outcome for one shared-projection rebuild session transition.</summary>
/// <param name="Version">The shared rebuild protocol version.</param>
/// <param name="Phase">The last proven durable session phase.</param>
/// <param name="Status">The closed transition outcome.</param>
/// <param name="AcceptedAggregateCount">The number of authoritative inventory entries accepted.</param>
/// <param name="InventoryFingerprint">The rolling fingerprint of accepted inventory entries.</param>
/// <param name="BatchFingerprint">The immutable finalized batch fingerprint once staging has proven it.</param>
/// <param name="ReasonCode">An optional bounded support-safe reason code.</param>
public sealed record DomainSharedProjectionRebuildResponse(
    int Version,
    DomainSharedProjectionRebuildPhase Phase,
    ProjectionDispatchStatus Status,
    long AcceptedAggregateCount,
    string InventoryFingerprint,
    string? BatchFingerprint,
    string? ReasonCode);
