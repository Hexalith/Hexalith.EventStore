using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedSnapshotProcessor(DetachedStateCapture<DetachedSnapshotState>? capture) : DomainProcessorBase<DetachedSnapshotState>
{
    protected override DetachedStateCapture<DetachedSnapshotState>? SnapshotCapture {
        get { DetachedSnapshotProbe.Current!.OnDeclaration?.Invoke(); return capture; }
    }

    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, DetachedSnapshotState? currentState)
    {
        DetachedSnapshotProbe probe = DetachedSnapshotProbe.Current!;
        probe.HandledState = currentState;
        if (currentState is not null) { currentState.Value += 100; }
        probe.OnHandle?.Invoke();
        if (probe.FailHandle) { throw new InvalidOperationException("Injected Handle failure after mutation."); }
        return Task.FromResult(DomainResult.NoOp());
    }
}
