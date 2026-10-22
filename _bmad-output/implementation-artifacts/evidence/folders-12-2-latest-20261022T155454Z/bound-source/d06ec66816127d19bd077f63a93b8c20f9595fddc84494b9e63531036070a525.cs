namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Durable complete registered-batch revocation result; reservation cannot be cancelled.</summary>
/// <param name="Envelope">Exact authenticated input.</param>
/// <param name="OwnerRevision">Durable tenant revision.</param>
/// <param name="KeyBlockSetRevision">Durable key block-set revision.</param>
/// <param name="ReceiptId">Original durable registrar receipt.</param>
/// <param name="AffectedBatchIds">Complete registered unconsumed matching batch identities.</param>
public sealed record DeletionCapabilityRevocationReceipt(DeletionCapabilityRevocationEnvelope Envelope, long OwnerRevision, long KeyBlockSetRevision, string ReceiptId, IReadOnlyList<string> AffectedBatchIds);
