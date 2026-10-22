namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Closed exact technical protocol command; unrelated optional shapes are rejected. It carries no approval credentials or content.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="OperationId">Exact OperationId.</param>
/// <param name="Operation">Exact Operation.</param>
/// <param name="ExpectedGuardRevision">Exact ExpectedGuardRevision.</param>
/// <param name="EpochId">Exact EpochId.</param>
/// <param name="DeletionRequestId">Exact DeletionRequestId.</param>
/// <param name="Scope">Exact Scope.</param>
/// <param name="Write">Exact Write.</param>
/// <param name="Repair">Exact Repair.</param>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="Hold">Exact Hold.</param>
/// <param name="Batch">Exact Batch.</param>
/// <param name="AuthorizationId">Exact AuthorizationId.</param>
/// <param name="ReferenceId">Exact ReferenceId.</param>
public sealed record GovernanceGuardTransition(string TenantId, string OperationId, GovernanceGuardOperation Operation, long ExpectedGuardRevision, string EpochId, string DeletionRequestId, GovernanceScopeV1? Scope, GovernanceWriteFacts? Write, DirectoryRepairBoundary? Repair, GovernanceOrdinalCommand? Ordinal, GovernanceHoldCommand? Hold, GovernanceBatchCommand? Batch, string AuthorizationId, string ReferenceId)
{
    /// <summary>Gets the complete original owner/operation identity only for bridge append or acknowledged drain. It grants no terminal-result or mutation authority.</summary>
    public DirectoryRepairOriginalIdentity? BridgeOriginal { get; init; }
    /// <summary>Complete original same-owner key block result; present only for the exact compromise mirror.</summary>
    public DeletionCapabilityRevocationReceipt? RevocationReceipt { get; init; }
}
