namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Fresh independently anchored exact blocked-batch comparison; no effect or replacement authority is granted by this read.</summary>
/// <param name="TenantId">Exact private tenant.</param><param name="BatchId">Exact immutable batch.</param>
/// <param name="CompromiseBlockReceiptId">Immutable original per-batch block receipt.</param><param name="KeyBlockSetRevision">Current global owner block-set revision.</param>
/// <param name="ReplacementKeyVersion">Exact prospective replacement key.</param><param name="ReplacementKeyBlocked">Whether that exact version is permanently blocked.</param>
/// <param name="OwnerRevision">Current independently anchored owner revision.</param>
public sealed record DeletionActivationComparison(string TenantId, string BatchId, string CompromiseBlockReceiptId, long KeyBlockSetRevision,
    string ReplacementKeyVersion, bool ReplacementKeyBlocked, long OwnerRevision)
{
    /// <summary>Exact independently retained blocked-key event at this current comparison; absence grants no reconciliation.</summary>
    public DeletionCapabilityRevocationReceipt? ReplacementKeyRevocation { get; init; }
}
