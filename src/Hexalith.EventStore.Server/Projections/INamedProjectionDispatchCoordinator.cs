using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Coordinates pre-admitted named projection persistence for normal delivery only.</summary>
internal interface INamedProjectionDispatchCoordinator {
    /// <summary>Attempts named v2 delivery when an exact verified catalog binding exists.</summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="registration">The resolved exact domain-service registration.</param>
    /// <param name="events">The persisted event history.</param>
    /// <param name="projectionEvents">The protected-data-readable wire event history.</param>
    /// <param name="cancellationToken">Propagates delivery cancellation.</param>
    /// <returns><c>true</c> when named delivery owned the request; otherwise the caller may use v1.</returns>
    Task<bool> TryDispatchAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        EventEnvelope[] events,
        ProjectionEventDto[] projectionEvents,
        CancellationToken cancellationToken);
}
