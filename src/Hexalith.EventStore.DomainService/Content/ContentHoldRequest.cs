namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Current authorization resource for a content-specific hold transition.</summary>
/// <param name="ReceiptId">Exact receipt.</param>
/// <param name="LegalHold">Whether to preserve physical bytes.</param>
public sealed record ContentHoldRequest(string ReceiptId, bool LegalHold);
