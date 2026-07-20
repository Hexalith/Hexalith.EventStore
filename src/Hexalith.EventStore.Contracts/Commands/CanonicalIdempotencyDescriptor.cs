namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Carries a domain adapter's canonical mutation intent into EventStore admission.
/// </summary>
/// <param name="AdapterId">The server-registered trusted adapter identifier.</param>
/// <param name="OperationId">The server-registered operation identifier.</param>
/// <param name="DescriptorVersion">The canonical descriptor schema version.</param>
/// <param name="CanonicalIntent">The adapter-produced length-prefixed, type-tagged canonical intent bytes.</param>
/// <param name="RetentionTier">The fixed server-registered replay-result retention tier.</param>
public sealed record CanonicalIdempotencyDescriptor(
    string AdapterId,
    string OperationId,
    int DescriptorVersion,
    byte[] CanonicalIntent,
    IdempotencyReplayRetentionTier RetentionTier);
