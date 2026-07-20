using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies one protected admission actor and tenant-directory alias without raw key material.</summary>
[DataContract]
public sealed record IdempotencyTenantLifecycleReference(
    [property: DataMember] string ActorId,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest);
