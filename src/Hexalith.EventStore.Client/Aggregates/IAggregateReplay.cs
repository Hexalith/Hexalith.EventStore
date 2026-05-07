using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Side-effect-free aggregate state replay capability exposed by domain processors that
/// own the canonical Apply path. Domain-service routers invoke <see cref="Replay"/> when
/// the EventStore server requests aggregate state reconstruction via <c>POST /replay-state</c>.
/// </summary>
public interface IAggregateReplay
{
    /// <summary>
    /// Returns whether this replay handler owns the requested aggregate type hint.
    /// </summary>
    /// <param name="aggregateType">The persisted aggregate type hint.</param>
    /// <returns>True when this replay handler can safely replay the aggregate type.</returns>
    bool CanReplayAggregateType(string aggregateType);

    /// <summary>
    /// Replays the supplied events through the owning state's Apply convention up to
    /// <see cref="AggregateReconstructionRequest.UpToSequence"/>. The implementation must
    /// not write aggregate state, projections, outbox messages, Dapr state, or any other
    /// runtime side effect.
    /// </summary>
    /// <param name="request">The reconstruction request.</param>
    /// <returns>The reconstruction result, including state JSON, status, and diagnostics.</returns>
    AggregateReconstructionResult Replay(AggregateReconstructionRequest request);
}
