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
    /// Idempotently erases every source stream, target stream, receipt, collision record, and
    /// related tenant key only after one authorized offboarding decision. The implementation
    /// must return only after durable erasure is proven; failure leaves the lifecycle open.
    /// </summary>
    Task EraseTenantAsync(
        string tenant,
        TrustedEffectAggregateErasure[] authorizedPartitions,
        CancellationToken cancellationToken = default);
}
