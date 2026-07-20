using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Contains server-owned canonical intent and fixed operation policy metadata.</summary>
/// <param name="AdapterId">The registered trusted adapter identifier.</param>
/// <param name="OperationId">The registered operation identifier.</param>
/// <param name="DescriptorVersion">The canonical descriptor schema version.</param>
/// <param name="CanonicalIntent">The deterministic length-prefixed, type-tagged canonical intent bytes.</param>
/// <param name="RetentionTier">The fixed server-registered replay retention tier.</param>
public sealed record TrustedIdempotencyDescriptor(
    string AdapterId,
    string OperationId,
    int DescriptorVersion,
    byte[] CanonicalIntent,
    IdempotencyReplayRetentionTier RetentionTier);
