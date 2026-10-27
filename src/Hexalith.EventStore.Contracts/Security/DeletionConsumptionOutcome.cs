using System.Runtime.Serialization;

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
[DataContract]
public sealed record DeletionConsumptionOutcome([property: DataMember] string TenantId, [property: DataMember] string BatchId,
    [property: DataMember] DeletionConsumptionStatus Status, [property: DataMember] long OwnerRevision, [property: DataMember] long KeyBlockSetRevision,
    [property: DataMember] string? ReceiptId, [property: DataMember] DeletionConsumptionBlockReason? BlockReason, [property: DataMember] string? BlockedKeyVersion,
    [property: DataMember] long? RevocationRevision, [property: DataMember] IReadOnlyList<DeletionTargetReceipt> TargetReceipts);
