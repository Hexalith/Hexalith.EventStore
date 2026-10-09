using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Retains the consumption actor entry deadline across independent transition journal calls.</summary>
internal sealed class DeadlineDeletionConsumptionAuthority(IAnchoredStateTransitionAuthority authority, AuthoritativeStreamReadDeadline deadline) : IAnchoredStateTransitionAuthority
{
    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(_ => authority.AdmitTransitionAsync(transition, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(_ => authority.RecoverTransitionAsync(transition, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(_ => authority.RecordTransitionAsync(transition, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => deadline.ReadAsync(_ => authority.VerifyTransitionAsync(transition, cancellationToken));
}
