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
public sealed record IdempotencyAdmissionRecord(
    int SchemaVersion,
    IdempotencyAdmissionState State,
    string TenantPartition,
    string DigestKeyVersion,
    string KeyDigest,
    string VerificationTag,
    string? IntentDigest,
    IdempotencyReplayRetentionTier RetentionTier,
    DateTimeOffset FirstConsumedAt,
    DateTimeOffset LastObservedAt,
    DateTimeOffset? ReplayExpiresAt,
    long FencingToken,
    CommandProcessingResult? ReplayResult)
{
    /// <summary>Gets the only record schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
