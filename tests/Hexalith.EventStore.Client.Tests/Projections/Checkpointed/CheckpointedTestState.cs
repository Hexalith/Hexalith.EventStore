namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>A projection state that records every applied sequence so contiguity is observable.</summary>
/// <param name="Sequences">The applied aggregate sequences in order.</param>
/// <param name="Values">The applied event values in order.</param>
public sealed record CheckpointedTestState(IReadOnlyList<long> Sequences, IReadOnlyList<string> Values);
