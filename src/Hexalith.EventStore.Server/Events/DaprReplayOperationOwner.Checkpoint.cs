using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Supplies a narrow local ownership check before checkpoint composition uses actual completed state.</summary>
internal sealed partial class DaprReplayOperationOwner
{
    /// <summary>Refuses a different fold or a proof manager whose cache clearing could erase projection staging.</summary>
    internal void RequireCheckpointOwner(IActorStateManager projectionManager, RegisteredLogicalReplayBinding reconstruction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(projectionManager, _stateManager) || !ReferenceEquals(reconstruction, _reconstruction))
        {
            throw new InvalidOperationException("CheckpointHold: exact fold and separate actual proof owner required.");
        }
    }
}
