using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Uses the tenant lifecycle inventory to erase every source and target actor partition.</summary>
public sealed class ActorTrustedEffectJointRetentionPolicy(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions) : ITrustedEffectJointRetentionPolicy
{
    /// <inheritdoc/>
    public async Task ValidateAsync(EffectIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        IIdempotencyTenantLifecycleActor lifecycle = actorProxyFactory
            .CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                new ActorId(identity.Tenant), IdempotencyTenantLifecycleActor.ActorTypeName);
        IdempotencyTenantLifecycleRecord record = await lifecycle.GetAsync().ConfigureAwait(false);
        if (!string.Equals(record.Tenant, identity.Tenant, StringComparison.Ordinal)
            || record.State != IdempotencyTenantLifecycleState.Active)
        {
            throw new InvalidOperationException("Joint trusted effect retention is unavailable.");
        }
    }

    /// <inheritdoc/>
    public async Task EraseTenantAsync(
        string tenant,
        TrustedEffectAggregateErasure[] authorizedPartitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(authorizedPartitions);
        cancellationToken.ThrowIfCancellationRequested();
        if (authorizedPartitions.Length == 0
            || authorizedPartitions.Any(request => request is null
                || !string.Equals(request.Tenant, tenant, StringComparison.Ordinal))
            || authorizedPartitions.Select(static request => string.Concat(
                    request.Tenant, ":", request.Domain, ":", request.Aggregate))
                .Distinct(StringComparer.Ordinal).Count() != authorizedPartitions.Length)
        {
            throw new InvalidOperationException("Joint trusted effect erasure has no valid authorized partitions.");
        }

        foreach (TrustedEffectAggregateErasure request in authorizedPartitions
            .OrderBy(static request => string.Concat(request.Tenant, ":", request.Domain, ":", request.Aggregate),
                StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity(
                request.Tenant, request.Domain, request.Aggregate);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(partition.ActorId), actorOptions.Value.AggregateActorTypeName);
            await actor.EraseTrustedEffectEvidenceAsync(request).ConfigureAwait(false);
        }
    }
}
