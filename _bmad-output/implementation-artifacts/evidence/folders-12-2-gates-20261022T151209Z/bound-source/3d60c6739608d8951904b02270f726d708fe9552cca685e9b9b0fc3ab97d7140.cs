using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Versioned internal request for one shared-projection rebuild session transition.</summary>
/// <param name="Version">The exact <see cref="DomainSharedProjectionRebuildProtocol.Version"/>.</param>
/// <param name="Action">The requested lifecycle transition.</param>
/// <param name="Identity">The stable session identity.</param>
/// <param name="AggregateOrdinal">The zero-based authoritative aggregate ordinal for accumulation.</param>
/// <param name="AggregateId">The aggregate identity for accumulation.</param>
/// <param name="IsErased">Whether the authoritative inventory marks the aggregate erased.</param>
/// <param name="Events">The complete aggregate event prefix for Accumulate, one bounded page for AccumulatePart, or empty when erased.</param>
/// <param name="ExpectedAggregateCount">The authoritative count sealed by finalization.</param>
/// <param name="ExpectedInventoryFingerprint">The last accepted inventory fingerprint sealed by finalization.</param>
/// <param name="CaptureInventoryFingerprint">The authoritative inventory fingerprint sealed at Begin.</param>
/// <param name="SourceHighWatermarks">The authoritative source positions sealed at Begin.</param>
/// <param name="ErasedOwnerIds">Owners proved erased by the same authoritative Begin inventory.</param>
/// <param name="PartOrdinal">The zero-based bounded owner transport part ordinal.</param>
/// <param name="PartCount">The number of accepted owner transport parts sealed by completion.</param>
/// <param name="ExpectedHistoryFingerprint">The exact captured raw owner history identity.</param>
public sealed record DomainSharedProjectionRebuildRequest(
    int Version,
    DomainSharedProjectionRebuildAction Action,
    DomainSharedProjectionRebuildIdentity Identity,
    long? AggregateOrdinal = null,
    string? AggregateId = null,
    bool IsErased = false,
    ProjectionEventDto[]? Events = null,
    long? ExpectedAggregateCount = null,
    string? ExpectedInventoryFingerprint = null,
    string? CaptureInventoryFingerprint = null,
    IReadOnlyDictionary<string, long>? SourceHighWatermarks = null,
    IReadOnlyList<string>? ErasedOwnerIds = null,
    int? PartOrdinal = null,
    int? PartCount = null,
    string? ExpectedHistoryFingerprint = null);
