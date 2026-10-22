using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Produces colliding optional evidence for the actor's support-safe classification.</summary>
public sealed class SnapshotCollisionTestProcessor : DomainProcessorBase<SnapshotCollisionTestState>
{
    /// <inheritdoc/>
    protected override FoldedSnapshotCaptureProfile<SnapshotCollisionTestState> FoldedSnapshotCapture { get; } =
        new("collision-profile", "collision-parity",
            new DetachedStateCapture<SnapshotCollisionTestState>(1024,
                static (state, _) => new SnapshotCollisionTestState
                {
                    CurrentSequence = state.CurrentSequence,
                    Events = [.. state.Events],
                }), SnapshotCaptureTestLimits.Value);

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, SnapshotCollisionTestState? currentState)
        => Task.FromResult(DomainResult.Success([new SnapshotCaptureTestEvent { Amount = 99 }]));
}
