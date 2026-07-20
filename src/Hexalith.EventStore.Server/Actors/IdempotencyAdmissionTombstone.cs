using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists the approved metadata-only consumed-key evidence after replay expiry.</summary>
/// <param name="SchemaVersion">The tombstone schema version.</param>
/// <param name="State">The terminal expired state.</param>
/// <param name="TenantPartition">The managed tenant partition.</param>
/// <param name="KeyDigest">The protected tenant/key partition digest.</param>
/// <param name="VerificationTag">The protected collision-verification tag.</param>
/// <param name="DigestKeyVersion">The referenced digest-key version.</param>
/// <param name="RetentionTier">The fixed operation retention class.</param>
/// <param name="FirstConsumedAt">When the key was first durably consumed.</param>
/// <param name="ReplayExpiredAt">The inclusive replay-expiry boundary.</param>
/// <param name="LastObservedAt">The monotonic persisted clock high-water mark.</param>
[DataContract]
public sealed record IdempotencyAdmissionTombstone(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] IdempotencyAdmissionState State,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string KeyDigest,
    [property: DataMember] string VerificationTag,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] IdempotencyReplayRetentionTier RetentionTier,
    [property: DataMember] DateTimeOffset FirstConsumedAt,
    [property: DataMember] DateTimeOffset ReplayExpiredAt,
    [property: DataMember] DateTimeOffset LastObservedAt)
{
    /// <summary>Gets the only tombstone schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
