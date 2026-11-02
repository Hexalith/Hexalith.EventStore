namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The domain-owned pure transition a checkpointed projection applies to each contiguous event.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// EventStore owns identity, compare-and-swap, retry, replay, fencing, and acknowledgement; the domain owns
/// only this function. It must be deterministic, must not mutate <c>current</c>, and must accept both live
/// deliveries (<see cref="CheckpointedProjectionEvent.DecodedEvent"/>) and authoritative history
/// (<see cref="CheckpointedProjectionEvent.Payload"/>). Events that do not change the projection still advance the
/// checkpoint, so the transition returns an unchanged copy for them. A transition may return <see langword="null"/>
/// when the stream has no materialized state yet (for example events that precede the aggregate's creation); the
/// checkpoint still advances and the record keeps no state.
/// </remarks>
public interface ICheckpointedProjectionTransition<TState>
    where TState : class {
    /// <summary>
    /// Produces the next projection state from the current state and one contiguous event.
    /// </summary>
    /// <param name="current">The current state, or <see langword="null"/> while nothing is materialized.</param>
    /// <param name="event">The event at the next contiguous sequence.</param>
    /// <returns>The next state, or <see langword="null"/> when the stream still has no materialized state.</returns>
    TState? Apply(TState? current, CheckpointedProjectionEvent @event);
}
