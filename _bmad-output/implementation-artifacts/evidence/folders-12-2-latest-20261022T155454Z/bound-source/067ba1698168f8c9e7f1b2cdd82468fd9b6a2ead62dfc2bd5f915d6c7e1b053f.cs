using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Authorizes bounded removal of one tombstone after tenant lifecycle eligibility.</summary>
[DataContract]
public sealed record IdempotencyAdmissionPurgeRequest(
    [property: DataMember] string TenantPartition,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest);
