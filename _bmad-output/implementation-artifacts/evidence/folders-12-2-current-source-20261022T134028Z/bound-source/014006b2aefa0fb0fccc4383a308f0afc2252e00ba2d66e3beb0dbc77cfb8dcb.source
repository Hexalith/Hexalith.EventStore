using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Proves a monotonic tenant decision that retains source, target, and receipt together.</summary>
public interface ITrustedEffectJointRetentionPolicy
{
    /// <summary>
    /// Rejects when either stream is held, offboarding, or independently erasable.
    /// A successful decision must remain valid through target commit and replay.
    /// </summary>
    Task ValidateAsync(EffectIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently erases the source or target stream, receipts, collision records, and
    /// trusted-effect idempotency records of each authorized partition, under one authorized
    /// offboarding decision. The tenant lifecycle calls this once per registered partition, each
    /// time with a single freshly signed capability, so an implementation must treat
    /// <paramref name="authorizedPartitions"/> as a subset of the tenant, not the whole tenant.
    /// It must return only after durable erasure of those partitions is proven; failure leaves
    /// the lifecycle open for retry.
    /// </summary>
    /// <param name="tenant">The tenant being offboarded.</param>
    /// <param name="authorizedPartitions">The lifecycle-signed partitions to erase in this call.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task EraseTenantAsync(
        string tenant,
        TrustedEffectAggregateErasure[] authorizedPartitions,
        CancellationToken cancellationToken = default);
}
