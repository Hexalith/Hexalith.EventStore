namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Current authorization resource for durable terminal abandonment.</summary>
/// <param name="ReceiptId">Exact never-assigned upload.</param>
public sealed record ContentAbandonRequest(string ReceiptId);
