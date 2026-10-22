
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events, sends to domain service, updates ProjectionActor.
/// </summary>
public interface IProjectionUpdateOrchestrator {
    /// <summary>
    /// Triggers a projection update for the specified aggregate. Fire-and-forget safe.
    /// </summary>
    /// <remarks>
    /// For domains with <c>RefreshIntervalMs &gt; 0</c>, this method registers the identity for
    /// later polling and returns without invoking the domain service. For domains with
    /// <c>RefreshIntervalMs == 0</c> it performs immediate full-replay delivery.
    /// The polling background service must NOT call this method to drive its own ticks — it
    /// would short-circuit on the registration path. Use <see cref="IProjectionPollerDeliveryGateway"/>
    /// for poller-driven delivery.
    /// </remarks>
    /// <param name="identity">The aggregate identity (tenant, domain, aggregateId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}

/// <summary>
/// Seam used by <see cref="ProjectionPollerService"/> to drive projection delivery without the
/// <c>RefreshIntervalMs &gt; 0</c> registration short-circuit applied by
/// <see cref="IProjectionUpdateOrchestrator.UpdateProjectionAsync"/>.
/// </summary>
/// <remarks>
/// This is a separate contract from <see cref="IProjectionUpdateOrchestrator"/> on purpose: immediate-mode
/// callers (event publication, REST controllers, MediatR handlers) inject the orchestrator and would
/// previously have observed <c>DeliverProjectionAsync</c> on it, making accidental polling-mode bypass
/// trivially reachable. Reaching this gateway now requires a deliberate DI request, which keeps
/// <see cref="IProjectionUpdateOrchestrator.UpdateProjectionAsync"/> as the only sanctioned entry point
/// for non-poller code paths.
/// </remarks>
public interface IProjectionPollerDeliveryGateway {
    /// <summary>
    /// Delivers projection work for a tracked polling identity, bypassing only the immediate-mode interval guard.
    /// </summary>
    /// <param name="identity">The aggregate identity (tenant, domain, aggregateId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeliverProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
