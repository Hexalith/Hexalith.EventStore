using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Verifies lifecycle-issued, partition-bound deletion fence and erasure decisions.</summary>
public interface ITrustedEffectErasureAuthority
{
    /// <summary>Signs a partition request issued inside a serialized lifecycle deletion or purge turn.</summary>
    Task<string> IssueAsync(TrustedEffectAggregateErasure request, CancellationToken cancellationToken = default);

    /// <summary>Fails closed unless the request proves its partition, purpose, and decision lifetime.</summary>
    Task ValidateAsync(TrustedEffectAggregateErasure request, CancellationToken cancellationToken = default);
}
