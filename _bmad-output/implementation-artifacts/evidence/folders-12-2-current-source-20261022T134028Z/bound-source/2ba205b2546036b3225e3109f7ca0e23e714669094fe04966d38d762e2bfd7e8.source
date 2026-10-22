namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Opaque exact owner outcome with durable receipt, key block-set revision and original ordered target vector.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="BatchId">Exact immutable batch.</param>
/// <param name="Status">Owner state or fail-closed result.</param>
/// <param name="OwnerRevision">Durable tenant owner revision.</param>
/// <param name="KeyBlockSetRevision">Durable current key block-set revision.</param>
/// <param name="ReceiptId">Exact durable state receipt if available.</param>
/// <param name="BlockReason">Winning cancellation reason.</param>
/// <param name="BlockedKeyVersion">Exact compromised version.</param>
/// <param name="RevocationRevision">Exact authenticated key revocation revision.</param>
/// <param name="TargetReceipts">Original ordered irreversible vector.</param>
public sealed record DeletionConsumptionOutcome(string TenantId, string BatchId, DeletionConsumptionStatus Status, long OwnerRevision, long KeyBlockSetRevision, string? ReceiptId, DeletionConsumptionBlockReason? BlockReason, string? BlockedKeyVersion, long? RevocationRevision, IReadOnlyList<DeletionTargetReceipt> TargetReceipts);
