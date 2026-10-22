using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedSnapshotAggregate<TMarker>(DetachedStateCapture<DetachedSnapshotState>? capture) : EventStoreAggregate<DetachedSnapshotState>
{
    protected override DetachedStateCapture<DetachedSnapshotState>? SnapshotCapture {
        get { DetachedSnapshotProbe.Current!.OnDeclaration?.Invoke(); return capture; }
    }

    public static DomainResult Handle(DetachedSnapshotCommand command, DetachedSnapshotState? state)
    {
        DetachedSnapshotProbe probe = DetachedSnapshotProbe.Current!;
        probe.HandledState = state;
        if (state is not null) { state.Value += 100; }
        probe.OnHandle?.Invoke();
        if (probe.FailHandle) { throw new InvalidOperationException("Injected Handle failure after mutation."); }
        return DomainResult.NoOp();
    }
}
