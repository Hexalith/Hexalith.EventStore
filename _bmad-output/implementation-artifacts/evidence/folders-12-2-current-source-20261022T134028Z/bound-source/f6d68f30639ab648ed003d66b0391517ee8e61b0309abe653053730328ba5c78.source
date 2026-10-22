namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Conditional successor activation with independently proved atomic old-epoch/fence/bridge revocation and preserved guard history.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="OperationId">Immutable activation identity.</param><param name="RepairId">Original fully drained boundary.</param>
/// <param name="ExpectedRevision">Conditional owner revision.</param><param name="Successor">Exact next installed all-writer epoch.</param>
/// <param name="BridgeRevocationReceipt">Atomic old epoch/repair/bridge revocation proof.</param><param name="PreservedGuardEvidenceReceipt">Independent full ordinal/hold/seal/batch/outcome preservation proof.</param>
public sealed record DirectoryEpochActivation(string TenantId, string OperationId, string RepairId, long ExpectedRevision, DirectoryEpochInstallation Successor,
    string BridgeRevocationReceipt, string PreservedGuardEvidenceReceipt);
