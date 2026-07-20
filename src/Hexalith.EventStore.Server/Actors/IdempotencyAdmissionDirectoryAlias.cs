using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies one protected digest-version alias for a logical tenant/key.</summary>
/// <param name="DigestKeyVersion">The digest-key version.</param>
/// <param name="ActorId">The corresponding admission actor identifier.</param>
/// <param name="KeyDigest">The protected partition digest used for directory state addressing.</param>
[DataContract]
public sealed record IdempotencyAdmissionDirectoryAlias(
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string ActorId,
    [property: DataMember] string KeyDigest);
