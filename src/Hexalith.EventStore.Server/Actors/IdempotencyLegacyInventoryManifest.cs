using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists the versioned immutable closure proof for one tenant legacy inventory.</summary>
[DataContract]
internal sealed record IdempotencyLegacyInventoryManifest(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string InventoryId,
    [property: DataMember] int InventoryVersion,
    [property: DataMember] bool Closed,
    [property: DataMember] IdempotencyLegacyInventoryManifestEntry[] Entries,
    [property: DataMember] string[] DigestKeyVersions,
    [property: DataMember] string? ManifestDigest = null)
{
    /// <summary>Gets the only manifest schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = IdempotencyLegacyInventoryClosure.CurrentSchemaVersion;
}
