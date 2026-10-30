namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Private typed shared owner seam. Ordinary writer/Workflow/public paths receive no unrestricted instance.</summary>
public interface IGovernanceScopeGuard
{
    /// <summary>Reads the receiving transaction's exact original outcome, including refusals absent from committed guard state; no mutation or redispatch.</summary>
    Task<GovernanceProtocolReceipt?> LookupOriginalAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets, CancellationToken cancellationToken = default)
        => Task.FromResult<GovernanceProtocolReceipt?>(null);
    /// <summary>Reads the exact independently qualified installed content-free guard; it grants no effect authority.</summary>
    Task<TenantGovernanceGuardState?> ReadAsync(string tenantId, CancellationToken cancellationToken = default);
    /// <summary>Applies exact conditional technical transition with joint protected source/outbox/phase mutations.</summary>
    Task<GovernanceProtocolReceipt?> ExecuteAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets, CancellationToken cancellationToken = default);
    /// <summary>Reads exact irreversible original no-issue and current healthy successor basis; unknown or absence never proves it.</summary>
    Task<GovernanceSigningRecovery?> ReadNoIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default);
    /// <summary>Reads an exact independently retained refused replacement-issue attempt. Absence/unknown is never evidence of non-issue.</summary>
    Task<GovernanceRejectedIssueOriginal?> ReadRejectedIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
        => Task.FromResult<GovernanceRejectedIssueOriginal?>(null);
}
