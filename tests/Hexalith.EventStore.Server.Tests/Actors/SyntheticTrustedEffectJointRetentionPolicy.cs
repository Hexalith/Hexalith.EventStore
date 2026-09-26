using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Test-only tenant eraser over committed source and target actor state.</summary>
internal sealed class SyntheticTrustedEffectJointRetentionPolicy(
    string tenant,
    FaultInjectingActorStateManager source,
    FaultInjectingActorStateManager target) : ITrustedEffectJointRetentionPolicy
{
    /// <inheritdoc/>
    public Task ValidateAsync(EffectIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(identity.Tenant, tenant, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Synthetic tenant retention decision is unavailable.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task EraseTenantAsync(string requestedTenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(requestedTenant, tenant, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Synthetic tenant erasure is unauthorized.");
        }

        await EraseCommittedAsync(source, cancellationToken).ConfigureAwait(false);
        await EraseCommittedAsync(target, cancellationToken).ConfigureAwait(false);
        if (source.CommittedState.Count != 0 || target.CommittedState.Count != 0)
        {
            throw new InvalidOperationException("Joint erasure did not remove both actor partitions.");
        }
    }

    private static async Task EraseCommittedAsync(
        FaultInjectingActorStateManager state,
        CancellationToken cancellationToken)
    {
        await state.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
        foreach (string key in state.CommittedState.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray())
        {
            await state.RemoveStateAsync(key, cancellationToken).ConfigureAwait(false);
        }

        await state.SaveStateAsync(cancellationToken).ConfigureAwait(false);
        await state.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
    }
}
