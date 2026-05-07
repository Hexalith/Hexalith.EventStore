using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Canonical aggregate state reconstruction service used by every Admin replay surface.
/// Routes the request to the owning domain's <c>POST /replay-state</c> endpoint via Dapr
/// service invocation so reconstruction always runs the same Apply convention as the
/// command path. The Admin UI / Admin API never reconstruct state independently.
/// </summary>
public interface IAggregateStateReconstructor
{
    /// <summary>
    /// Replays the supplied events through the owning domain's Apply convention up to
    /// <paramref name="upToSequence"/>. The implementation must be side-effect free: no
    /// aggregate state, projections, outbox messages, Dapr state, or other runtime state
    /// may be written.
    /// </summary>
    /// <param name="identity">Aggregate identity (tenant, domain, aggregate id).</param>
    /// <param name="aggregateType">Optional aggregate type hint (taken from the persisted envelope when available).</param>
    /// <param name="events">Persisted events available for replay. The reconstructor sorts by sequence number.</param>
    /// <param name="upToSequence">Inclusive replay target.</param>
    /// <param name="includeTimeline">When true the result will include per-event state snapshots (for Blame, Step Through).</param>
    /// <param name="requestId">Optional correlation id propagated to logs/traces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reconstruction result, including state JSON, status, and diagnostics.</returns>
    Task<AggregateReconstructionResult> ReconstructAsync(
        AggregateIdentity identity,
        string aggregateType,
        IReadOnlyList<EventEnvelope> events,
        long upToSequence,
        bool includeTimeline = false,
        string? requestId = null,
        CancellationToken cancellationToken = default);
}
