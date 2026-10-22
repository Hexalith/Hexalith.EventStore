namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Opaque irreversible original per-target evidence; never contains key material.</summary>
/// <param name="Target">Complete exact target.</param>
/// <param name="OriginalBatchId">Original consuming batch.</param>
/// <param name="ReceiptId">Durable provider irreversible receipt.</param>
public sealed record DeletionTargetReceipt(ProtectionTarget Target, string OriginalBatchId, string ReceiptId);
