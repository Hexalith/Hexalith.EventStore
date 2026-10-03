namespace Hexalith.EventStore.Server.Identity;

/// <summary>Private durable stable attribution, independent of login aliases and Party IDs.</summary>
/// <param name="ActorId">The stable opaque actor ULID.</param>
/// <param name="Revision">The monotonic capability revision.</param>
/// <param name="Active">Whether the actor currently has active attribution capability.</param>
/// <param name="ProvenanceId">The opaque verified operator provenance reference.</param>
public sealed record ActorRegistryEntry(string ActorId, long Revision, bool Active, string ProvenanceId);
