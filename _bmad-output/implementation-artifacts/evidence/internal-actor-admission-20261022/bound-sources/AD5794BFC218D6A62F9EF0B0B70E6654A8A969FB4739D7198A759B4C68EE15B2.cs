using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Closes one exact versioned inventory after its complete manifest is known.</summary>
[DataContract]
public sealed record IdempotencyLegacyInventoryClosure(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string InventoryId,
    [property: DataMember] int InventoryVersion,
    [property: DataMember] string[] DigestKeyVersions,
    [property: DataMember] int EntryCount,
    [property: DataMember] string ManifestDigest)
{
    /// <summary>Gets the current closed-inventory contract schema.</summary>
    public const int CurrentSchemaVersion = 1;
}
