using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Events;

internal sealed class DaprGlobalPositionAllocator(IActorProxyFactory actorProxyFactory) : IGlobalPositionAllocator {
    private static readonly ActorId GlobalActorId = new("global");

    public Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        cancellationToken.ThrowIfCancellationRequested();

        IGlobalPositionActor proxy = actorProxyFactory.CreateActorProxy<IGlobalPositionActor>(
            GlobalActorId,
            GlobalPositionActor.ActorTypeName);

        return proxy.AllocateAsync(count);
    }
}
