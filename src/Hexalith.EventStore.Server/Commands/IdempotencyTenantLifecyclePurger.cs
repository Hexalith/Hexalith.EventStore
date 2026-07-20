using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Executes bounded final purge outside actor turns after lifecycle eligibility.</summary>
public sealed class IdempotencyTenantLifecyclePurger(IActorProxyFactory actorProxyFactory)
{
    /// <summary>Purges at most <paramref name="maximumCount"/> protected references.</summary>
    public async Task<IdempotencyTenantLifecycleRecord> PurgeAsync(
        string tenant,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        IIdempotencyTenantLifecycleActor lifecycle = actorProxyFactory
            .CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                new ActorId(tenant),
                IdempotencyTenantLifecycleActor.ActorTypeName);
        IdempotencyTenantLifecycleRecord state = await lifecycle.GetAsync().ConfigureAwait(false);
        if (state.State != IdempotencyTenantLifecycleState.PurgeEligible)
        {
            throw new InvalidOperationException("Tenant idempotency state is not purge eligible.");
        }

        IIdempotencyAdmissionDirectoryActor directory = actorProxyFactory
            .CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                new ActorId(tenant),
                IdempotencyAdmissionDirectoryActor.ActorTypeName);
        foreach (IdempotencyTenantLifecycleReference reference in state.References.Take(maximumCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IIdempotencyAdmissionActor admission = actorProxyFactory
                .CreateActorProxy<IIdempotencyAdmissionActor>(
                    new ActorId(reference.ActorId),
                    IdempotencyAdmissionActor.ActorTypeName);
            bool purged = await admission.PurgeTombstoneAsync(
                new IdempotencyAdmissionPurgeRequest(
                    tenant,
                    reference.DigestKeyVersion,
                    reference.KeyDigest)).ConfigureAwait(false);
            if (!purged)
            {
                continue;
            }

            await directory.PurgeAliasAsync(
                new IdempotencyAdmissionDirectoryAlias(
                    reference.DigestKeyVersion,
                    reference.ActorId,
                    reference.KeyDigest)).ConfigureAwait(false);
            state = await lifecycle.AcknowledgePurgeAsync(reference.ActorId).ConfigureAwait(false);
        }

        return state;
    }
}
