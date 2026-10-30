namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>An immutable historical policy and key-reference entry. Never participates in routing or admission.</summary>
/// <param name="Entry">The original entry, including its original generation and content digest.</param>
public sealed record RetainedIdempotencyCatalogEntry(IdempotencyCatalogEntry Entry);
