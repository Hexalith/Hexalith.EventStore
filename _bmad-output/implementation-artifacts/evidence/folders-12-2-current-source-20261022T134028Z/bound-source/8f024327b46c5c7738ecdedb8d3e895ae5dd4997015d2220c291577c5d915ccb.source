using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Reads the retained floor from the source aggregate's authoritative metadata.</summary>
public sealed class ActorTrustedEffectSourceFloorProvider(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions) : ITrustedEffectSourceFloorProvider
{
    /// <inheritdoc/>
    public Task<long?> GetRetainedFloorAsync(EffectIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        var source = new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(source.ActorId), actorOptions.Value.AggregateActorTypeName);
        return actor.GetRetainedFloorAsync();
    }
}
