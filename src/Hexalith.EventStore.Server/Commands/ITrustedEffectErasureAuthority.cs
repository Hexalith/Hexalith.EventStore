using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Verifies a lifecycle-issued, partition-bound offboarding decision before actor erasure.</summary>
public interface ITrustedEffectErasureAuthority
{
    /// <summary>Signs a partition request issued inside the lifecycle purge turn.</summary>
    Task<string> IssueAsync(TrustedEffectAggregateErasure request, CancellationToken cancellationToken = default);

    /// <summary>Fails closed unless the request proves the current authorized tenant purge decision.</summary>
    Task ValidateAsync(TrustedEffectAggregateErasure request, CancellationToken cancellationToken = default);
}
