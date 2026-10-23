namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Physical immutable bytes stored exclusively in the body namespace.</summary>
internal sealed record ContentStoredBody(ContentReceipt Receipt, byte[] Bytes, bool Erased = false);
