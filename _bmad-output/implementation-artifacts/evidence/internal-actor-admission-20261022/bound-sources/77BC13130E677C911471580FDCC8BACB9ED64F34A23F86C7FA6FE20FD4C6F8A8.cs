namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Detached pure protocol result; a no-effect stale/obsolete/denied result preserves the logical guard while its exact outcome is persisted.</summary>
/// <param name="State">Exact State.</param>
/// <param name="Receipt">Exact Receipt.</param>
/// <param name="MutatesGuard">Exact MutatesGuard.</param>
/// <param name="AllowsTargetWrites">Exact AllowsTargetWrites.</param>
public sealed record GovernanceGuardReduction(TenantGovernanceGuardState State, GovernanceProtocolReceipt Receipt, bool MutatesGuard, bool AllowsTargetWrites);
