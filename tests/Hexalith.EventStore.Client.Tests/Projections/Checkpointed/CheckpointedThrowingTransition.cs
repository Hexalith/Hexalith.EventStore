using Hexalith.EventStore.Client.Projections.Checkpointed;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>A transition that refuses replay while preserving the persisted gapped record.</summary>
internal sealed class CheckpointedThrowingTransition : ICheckpointedProjectionTransition<CheckpointedTestState>
{
    public CheckpointedTestState Apply(CheckpointedTestState? current, CheckpointedProjectionEvent @event)
        => throw new InvalidOperationException("Synthetic transition failure.");
}
