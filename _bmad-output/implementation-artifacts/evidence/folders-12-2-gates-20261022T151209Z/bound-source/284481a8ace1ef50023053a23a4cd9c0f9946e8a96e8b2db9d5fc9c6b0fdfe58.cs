using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independent exact source, current principal/decision, finite cohort, custody and nonrollback authority. No fixture or default grants production authority.</summary>
public interface IGovernanceGuardAuthority
{
    /// <summary>Authenticates the installed current healthy signing key and immutable terminal no-issue source; omission disables signing continuation.</summary>
    Task<GovernanceSigningRecovery?> ReadSigningRecoveryAsync(GovernanceProtocolReceipt noIssue, TenantGovernanceGuardState state,
        CancellationToken cancellationToken = default) => Task.FromResult<GovernanceSigningRecovery?>(null);
    /// <summary>Authenticates current private exact original-result lookup independently of any fresh effect/decision permission; omission disables lookup.</summary>
    Task<GovernanceGuardEvidence?> ReadLookupAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest,
        CancellationToken cancellationToken = default) => Task.FromResult<GovernanceGuardEvidence?>(null);
    /// <summary>Authenticates the closed transition, immutable first-event/permit facts and exact protected source/outbox/phase mutations. AppendResourceId must name the concrete resource owned by this exact append and mutation vector; violation evidence must independently correlate its original committed operation, resource, facts and target mutation digest, rather than just authenticate a receipt in isolation. Missing provenance denies the operation. Supplies applicable current policy and original protection receipts.</summary>
    Task<GovernanceGuardEvidence?> ReadAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest,
        TenantGovernanceGuardState state, CancellationToken cancellationToken = default);
}
