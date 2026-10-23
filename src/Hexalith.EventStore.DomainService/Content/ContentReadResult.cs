namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Verified exact body bytes; never send this value to events, projections or diagnostics.</summary>
/// <param name="Receipt">Immutable verified metadata.</param>
/// <param name="Bytes">Exact body bytes.</param>
public sealed record ContentReadResult(ContentReceipt Receipt, byte[] Bytes);
