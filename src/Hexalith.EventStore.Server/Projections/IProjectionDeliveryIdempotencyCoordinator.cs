using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Coordinates projection route admission and atomic completion/checkpoint transitions.</summary>
internal interface IProjectionDeliveryIdempotencyCoordinator {
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
        long? resumeFencingToken = null);

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
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally releases an untouched or deterministically failed reservation.</summary>
    Task<bool> TryReleaseAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default);
}
