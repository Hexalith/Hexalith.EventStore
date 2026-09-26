using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Rejects effects without a retained source event and a joint tenant retention decision.</summary>
public sealed class TrustedEffectRetentionGate(
    IActorProxyFactory actorProxyFactory,
    ITrustedEffectSourceFloorProvider sourceFloors,
    ITrustedEffectJointRetentionPolicy jointRetention,
    IOptions<EventStoreActorOptions> actorOptions) : ITrustedEffectRetentionGate
{
    /// <inheritdoc/>
    public async Task ValidateAsync(EffectIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        _ = EffectIdentityCodec.Encode(identity);

        // Lifecycle is serialized per tenant. Once deletion starts, neither a new effect nor a
        // receipt replay may disclose an outcome, regardless of the gateway status record.
        IIdempotencyTenantLifecycleActor lifecycle = actorProxyFactory
            .CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                new ActorId(identity.Tenant), IdempotencyTenantLifecycleActor.ActorTypeName);
        IdempotencyTenantLifecycleRecord record = await lifecycle.GetAsync().ConfigureAwait(false);
        if (!string.Equals(record.Tenant, identity.Tenant, StringComparison.Ordinal)
            || record.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Trusted effect tenant retention is unavailable.");
        }

        await jointRetention.ValidateAsync(identity, cancellationToken).ConfigureAwait(false);
        long? floor = await sourceFloors.GetRetainedFloorAsync(identity, cancellationToken).ConfigureAwait(false);
        if (floor is null || floor < 1 || identity.SourceEnvelopeSequence < floor)
        {
            throw new InvalidOperationException("Trusted effect source is below the retained floor.");
        }

        var source = new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(source.ActorId), actorOptions.Value.AggregateActorTypeName);
        EventEnvelope[] events = await actor.ReadEventsRangeAsync(
            identity.SourceEnvelopeSequence - 1,
            identity.SourceEnvelopeSequence,
            1).ConfigureAwait(false);
        if (events.Length != 1
            || events[0].SequenceNumber != identity.SourceEnvelopeSequence
            || !string.Equals(events[0].TenantId, identity.Tenant, StringComparison.Ordinal)
            || !string.Equals(events[0].Domain, identity.SourceDomain, StringComparison.Ordinal)
            || !string.Equals(events[0].AggregateId, identity.SourceAggregate, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trusted effect source evidence is unavailable.");
        }
    }
}
