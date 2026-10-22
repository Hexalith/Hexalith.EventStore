namespace Hexalith.EventStore.Contracts.Security;

/// <summary>EventStore-owned technical guard body. Source/outbox effects and this state are committed together; existing aggregate actor keys are never accessed.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="InstallationId">Exact InstallationId.</param>
/// <param name="Revision">Exact Revision.</param>
/// <param name="EpochId">Exact EpochId.</param>
/// <param name="LegacyRevocationReceipt">Exact LegacyRevocationReceipt.</param>
/// <param name="WriterEnforcementReceipt">Exact WriterEnforcementReceipt.</param>
/// <param name="Repair">Exact Repair.</param>
/// <param name="RepairHistory">Exact RepairHistory.</param>
/// <param name="Deletions">Exact Deletions.</param>
/// <param name="Holds">Exact Holds.</param>
/// <param name="Authorizations">Exact Authorizations.</param>
/// <param name="Receipts">Exact Receipts.</param>
/// <param name="CompromisedKeyVersions">Exact CompromisedKeyVersions.</param>
public sealed record TenantGovernanceGuardState(string TenantId, string InstallationId, long Revision, string EpochId, string LegacyRevocationReceipt, string WriterEnforcementReceipt, GovernanceRepairState? Repair, IReadOnlyList<GovernanceRepairState> RepairHistory, IReadOnlyList<GovernanceDeletionState> Deletions, IReadOnlyList<GovernanceHoldState> Holds, IReadOnlyList<GovernanceAuthorization> Authorizations, IReadOnlyList<GovernanceProtocolReceipt> Receipts, IReadOnlyList<string> CompromisedKeyVersions)
{
    /// <summary>Complete original independently authenticated key blocks, retained for exact mirror recovery.</summary>
    public IReadOnlyList<DeletionCapabilityRevocationReceipt> Revocations { get; init; } = [];
}
