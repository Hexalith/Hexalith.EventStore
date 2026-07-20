using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Contains protected metadata for one tenant/key admission.</summary>
/// <param name="SchemaVersion">The admission record schema version.</param>
/// <param name="TenantPartition">The managed tenant partition.</param>
/// <param name="DigestKeyVersion">The digest-key version.</param>
/// <param name="KeyDigest">The tenant/key partition digest.</param>
/// <param name="VerificationTag">The collision verification tag.</param>
/// <param name="IntentDigest">The keyed canonical intent digest.</param>
/// <param name="RetentionTier">The validated fixed replay tier.</param>
[DataContract]
public sealed record IdempotencyAdmissionRequest(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] string VerificationTag,
    [property: DataMember] string IntentDigest,
    [property: DataMember] IdempotencyReplayRetentionTier RetentionTier);
