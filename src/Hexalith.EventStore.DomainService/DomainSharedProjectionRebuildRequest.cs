using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Versioned internal request for one shared-projection rebuild session transition.</summary>
/// <param name="Version">The exact <see cref="DomainSharedProjectionRebuildProtocol.Version"/>.</param>
/// <param name="Action">The requested lifecycle transition.</param>
/// <param name="Identity">The stable session identity.</param>
/// <param name="AggregateOrdinal">The zero-based authoritative aggregate ordinal for accumulation.</param>
/// <param name="AggregateId">The aggregate identity for accumulation.</param>
/// <param name="IsErased">Whether the authoritative inventory marks the aggregate erased.</param>
/// <param name="Events">The complete aggregate event prefix, or empty when erased.</param>
/// <param name="ExpectedAggregateCount">The authoritative count sealed by finalization.</param>
/// <param name="ExpectedInventoryFingerprint">The last accepted inventory fingerprint sealed by finalization.</param>
public sealed record DomainSharedProjectionRebuildRequest(
    int Version,
    DomainSharedProjectionRebuildAction Action,
    DomainSharedProjectionRebuildIdentity Identity,
    long? AggregateOrdinal = null,
    string? AggregateId = null,
    bool IsErased = false,
    ProjectionEventDto[]? Events = null,
    long? ExpectedAggregateCount = null,
    string? ExpectedInventoryFingerprint = null);
