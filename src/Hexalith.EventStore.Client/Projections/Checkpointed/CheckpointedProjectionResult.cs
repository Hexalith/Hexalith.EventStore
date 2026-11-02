namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The durable result of a checkpointed projection delivery or recovery.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <param name="Outcome">The committed or proven-unchanged outcome.</param>
/// <param name="Record">The record observed or committed with that outcome.</param>
public sealed record CheckpointedProjectionResult<TState>(
    CheckpointedProjectionOutcome Outcome,
    CheckpointedProjectionRecord<TState> Record)
    where TState : class;
