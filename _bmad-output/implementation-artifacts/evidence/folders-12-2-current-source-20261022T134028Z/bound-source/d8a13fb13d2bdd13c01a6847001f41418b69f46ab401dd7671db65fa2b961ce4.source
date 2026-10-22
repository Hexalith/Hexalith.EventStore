using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Contains protected admission identity material safe for persistence and routing.</summary>
/// <param name="ActorId">The tenant/key admission actor identifier.</param>
/// <param name="TenantPartition">The managed tenant partition.</param>
/// <param name="DigestKeyVersion">The digest-key version.</param>
/// <param name="KeyDigest">The base64url tenant-key digest.</param>
/// <param name="VerificationTag">The domain-separated collision verification tag.</param>
/// <param name="IntentDigest">The keyed canonical intent digest.</param>
/// <param name="RetentionTier">The validated fixed retention tier.</param>
public sealed record IdempotencyProtectedIdentity(
    string ActorId,
    string TenantPartition,
    string DigestKeyVersion,
    string KeyDigest,
    string VerificationTag,
    string IntentDigest,
    IdempotencyReplayRetentionTier RetentionTier);
