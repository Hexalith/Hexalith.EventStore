namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Identifies an owner's canonical descriptor schema; contains schema metadata only, never request intent.</summary>
/// <param name="SchemaId">Stable owner-assigned schema identifier.</param>
/// <param name="Version">Explicit positive schema version.</param>
/// <param name="Fields">Complete owner-declared field schema, sorted by name for encoding.</param>
public sealed record IdempotencyDescriptorSchema(string SchemaId, int Version, IReadOnlyList<IdempotencyDescriptorField> Fields);
