namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Body-free current binding and its verified immutable receipt.</summary>
/// <param name="Change">Accepted resource metadata and exact version identity.</param>
/// <param name="Receipt">Server-verified body metadata.</param>
public sealed record ContentBinding(ContentChange Change, ContentReceipt Receipt);
