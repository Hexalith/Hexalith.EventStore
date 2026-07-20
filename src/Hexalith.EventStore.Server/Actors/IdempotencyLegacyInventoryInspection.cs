using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns protected legacy inventory classification and optional exact source evidence.</summary>
[DataContract]
public sealed record IdempotencyLegacyInventoryInspection(
    [property: DataMember] IdempotencyLegacyInventoryDecision Decision,
    [property: DataMember] IdempotencyLegacyInventoryEntry? Entry = null);
