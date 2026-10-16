using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Coordinates projection route admission and atomic completion/checkpoint transitions.</summary>
internal interface IProjectionDeliveryIdempotencyCoordinator {
    /// <summary>Fences one explicit before-effect canonical fallback while preserving the original admitted work.</summary>
    Task<ProjectionDeliveryReservation?> TrySwitchToCanonicalReplayAsync(AggregateIdentity identity,
        string projectionName, ProjectionDeliveryReservation reservation, CancellationToken cancellationToken);
    /// <summary>Reclaims only the same pending finalization and binds the recoverer's own lifecycle token.</summary>
    Task<ProjectionDeliveryReservation?> TryReclaimIdleBindingAsync(AggregateIdentity identity,
        string projectionName, ProjectionDeliveryReservation reservation, string lifecycleOperationToken,
        CancellationToken cancellationToken = default, long? resumeFencingToken = null);
    /// <summary>Persists immutable pending references while retaining C and its active reservation.</summary>
    Task<bool> PrepareIdleBindingAsync(AggregateIdentity identity, string projectionName,
        ProjectionDeliveryReservation reservation, string lifecycleOperationToken, ProjectionOwnerProof ownerProof,
        CancellationToken cancellationToken = default);
    /// <summary>Reads a validated completed route prefix for an exact next-event delivery.</summary>
    Task<ProjectionDeliveryAcceptedPrefix?> ReadAcceptedPrefixAsync(AggregateIdentity identity,
        string projectionName, CancellationToken cancellationToken = default);

    /// <summary>Preserves the frozen v1 identity when a mapped route already has v1 receipts.</summary>
    Task<bool> UseLegacyMappedIdentityAsync(AggregateIdentity identity, string projectionName,
        CancellationToken cancellationToken = default);

    /// <summary>Attempts to acquire or classify a durable delivery reservation.</summary>
    Task<ProjectionDeliveryAdmissionResult> TryAdmitAsync(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
        bool reclaimSafe,
        CancellationToken cancellationToken = default,
        long? resumeFencingToken = null);

    /// <summary>Attempts admission using canonical digests computed from bounded source pages.</summary>
    Task<ProjectionDeliveryAdmissionResult> TryAdmitHistoryAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryFingerprintHistory history,
        bool reclaimSafe,
        CancellationToken cancellationToken = default,
        long? resumeFencingToken = null,
        ProjectionDeliveryGuardReservation? guardBinding = null);

    /// <summary>Conditionally completes a matching fenced reservation.</summary>
    Task<ProjectionDeliveryCompletion> CompleteAsync(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default);

    /// <summary>Completes a reservation using canonical digests computed from bounded source pages.</summary>
    Task<ProjectionDeliveryCompletion> CompleteHistoryAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryFingerprintHistory history,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default,
        ProjectionDeliveryGuardEvidence? guardEvidence = null);

    /// <summary>Conditionally releases an untouched or deterministically failed reservation.</summary>
    Task<bool> TryReleaseAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default);
}
