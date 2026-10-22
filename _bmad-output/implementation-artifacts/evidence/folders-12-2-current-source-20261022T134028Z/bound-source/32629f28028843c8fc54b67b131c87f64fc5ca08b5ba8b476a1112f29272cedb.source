using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists versioned protected admission state without a raw idempotency key.</summary>
/// <param name="SchemaVersion">The record schema version.</param>
/// <param name="State">The durable admission state.</param>
/// <param name="TenantPartition">The managed tenant partition.</param>
/// <param name="DigestKeyVersion">The digest-key version.</param>
/// <param name="KeyDigest">The tenant/key partition digest.</param>
/// <param name="VerificationTag">The collision verification tag.</param>
/// <param name="IntentDigest">The live keyed intent digest, removed after expiry.</param>
/// <param name="RetentionTier">The fixed replay-result retention tier.</param>
/// <param name="FirstConsumedAt">When the key was first durably consumed.</param>
/// <param name="LastObservedAt">The monotonic persisted clock high-water mark.</param>
/// <param name="ReplayExpiresAt">The inclusive replay expiry boundary.</param>
/// <param name="FencingToken">The current fence.</param>
/// <param name="ReplayResult">The live replay result, removed after expiry.</param>
/// <param name="ExecutionMessageId">The stable live execution identity, removed on tombstone compaction.</param>
/// <param name="ExecutionCorrelationId">The stable live aggregate-checkpoint identity, removed on tombstone compaction.</param>
[DataContract]
public sealed record IdempotencyAdmissionRecord(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] IdempotencyAdmissionState State,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] string VerificationTag,
    [property: DataMember] string? IntentDigest,
    [property: DataMember] IdempotencyReplayRetentionTier RetentionTier,
    [property: DataMember] DateTimeOffset FirstConsumedAt,
    [property: DataMember] DateTimeOffset LastObservedAt,
    [property: DataMember] DateTimeOffset? ReplayExpiresAt,
    [property: DataMember] long FencingToken,
    [property: DataMember] CommandProcessingResult? ReplayResult,
    [property: DataMember] string? ExecutionMessageId = null,
    [property: DataMember] string? ExecutionCorrelationId = null)
{
    /// <summary>Gets the only record schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 2;
}
