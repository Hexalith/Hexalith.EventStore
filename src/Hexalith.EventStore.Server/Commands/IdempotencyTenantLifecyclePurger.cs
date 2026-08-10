using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Delegates bounded final purge to the tenant lifecycle actor's serialized turn.</summary>
public sealed class IdempotencyTenantLifecyclePurger(IActorProxyFactory actorProxyFactory)
{
    /// <summary>Gets the maximum number of serialized purge turns issued by one call.</summary>
    public const int MaximumIterationsPerCall = 32;

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
        cancellationToken.ThrowIfCancellationRequested();
        IdempotencyTenantLifecycleRecord state = await lifecycle.GetAsync()
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        int iterationCount = Math.Min(maximumCount, MaximumIterationsPerCall);
        for (int iteration = 0; iteration < iterationCount; iteration++)
        {
            if (state.State == IdempotencyTenantLifecycleState.Purged)
            {
                break;
            }

            int previousReferenceCount = state.References.Length;
            state = await lifecycle
                .PurgeAsync(IdempotencyTenantLifecycleActor.MaximumReferencesPerPurgeTurn)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (state.State == IdempotencyTenantLifecycleState.Purged
                || state.References.Length >= previousReferenceCount)
            {
                break;
            }
        }

        return state;
    }
}
