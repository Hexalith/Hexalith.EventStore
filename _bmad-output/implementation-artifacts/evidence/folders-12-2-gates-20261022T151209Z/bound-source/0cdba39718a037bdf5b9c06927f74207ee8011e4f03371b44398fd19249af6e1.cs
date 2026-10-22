namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact all-or-none backend result; interface does not qualify physical destruction or restore behavior.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="BatchId">Exact immutable batch.</param>
/// <param name="ReservationReceiptId">Exact original owner reservation.</param>
/// <param name="State">Qualified backend observation.</param>
/// <param name="TargetReceipts">Complete ordered original vector only for Consumed.</param>
public sealed record DeletionManifestProviderResult(string TenantId, string BatchId, string ReservationReceiptId, DeletionManifestProviderState State, IReadOnlyList<DeletionTargetReceipt> TargetReceipts);
