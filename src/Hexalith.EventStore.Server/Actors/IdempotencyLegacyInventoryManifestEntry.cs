using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Binds one protected inventory alias to its immutable entry fingerprint.</summary>
[DataContract]
internal sealed record IdempotencyLegacyInventoryManifestEntry(
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] string EntryDigest);
