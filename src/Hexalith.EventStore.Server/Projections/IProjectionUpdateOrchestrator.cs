
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events, sends to domain service, updates ProjectionActor.
/// </summary>
public interface IProjectionUpdateOrchestrator {
    /// <summary>
    /// Triggers a projection update for the specified aggregate. Fire-and-forget safe.
    /// </summary>
    /// <param name="identity">The aggregate identity (tenant, domain, aggregateId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
