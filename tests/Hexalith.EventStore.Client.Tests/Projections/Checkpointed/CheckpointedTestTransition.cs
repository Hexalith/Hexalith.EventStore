using System.Text;

using Hexalith.EventStore.Client.Projections.Checkpointed;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>A pure transition that refuses any non-contiguous application.</summary>
internal sealed class CheckpointedTestTransition : ICheckpointedProjectionTransition<CheckpointedTestState> {
    public int Applications { get; private set; }

    public CheckpointedTestState Apply(CheckpointedTestState? current, CheckpointedProjectionEvent @event) {
        ArgumentNullException.ThrowIfNull(@event);
        long previous = current?.Sequences.Count > 0 ? current.Sequences[^1] : 0;
        if (@event.Sequence != previous + 1) {
            throw new InvalidOperationException($"Non-contiguous application of {@event.Sequence} after {previous}.");
        }

        string value = @event.DecodedEvent as string
            ?? Encoding.UTF8.GetString(@event.Payload ?? throw new InvalidOperationException("No payload."));
        Applications++;
        return new CheckpointedTestState(
            [.. current?.Sequences ?? [], @event.Sequence],
            [.. current?.Values ?? [], value]);
    }
}
