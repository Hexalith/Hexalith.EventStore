using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Reserves exclusive actor ownership for the dormant logical replay operation protocol.</summary>
/// <remarks>It is intentionally unregistered until exact Dapr topology/component and endpoint authority qualify.</remarks>
internal sealed class DaprReplayOperationActor : Actor
{
    /// <summary>Derives stable tenant/operation scope from the actual canonical actor ID.</summary>
    internal DaprReplayOperationActor(ActorHost host) : base(host)
    {
        string[] parts = host.Id.GetId().Split(':');
        if (parts.Length != 3 || parts[1] != "replay-operation"
            || new AggregateIdentity(parts[0], parts[1], parts[2]).ActorId != host.Id.GetId()) { throw new ArgumentException("A replay operation actor requires canonical tenant:replay-operation:operation identity."); }
        Owner = new DaprReplayOperationOwner(StateManager, parts[0], parts[2]);
    }

    /// <summary>Gets this actor's exclusive application-state protocol.</summary>
    internal DaprReplayOperationOwner Owner { get; }

    /// <summary>Detaches the actor cache before releasing any retained private replay buffers.</summary>
    protected override Task OnDeactivateAsync() => Owner.DisposeAsync().AsTask();
}
