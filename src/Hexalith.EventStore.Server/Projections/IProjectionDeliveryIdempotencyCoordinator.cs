using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Coordinates projection route admission and atomic completion/checkpoint transitions.</summary>
internal interface IProjectionDeliveryIdempotencyCoordinator {
    /// <summary>Attempts to acquire or classify a durable delivery reservation.</summary>
    Task<ProjectionDeliveryAdmissionResult> TryAdmitAsync(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
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

    /// <summary>Conditionally releases an untouched or deterministically failed reservation.</summary>
    Task<bool> TryReleaseAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReservation reservation,
        CancellationToken cancellationToken = default);
}
