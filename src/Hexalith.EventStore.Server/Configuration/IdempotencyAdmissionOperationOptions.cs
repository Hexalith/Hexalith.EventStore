using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Defines one server-registered canonical operation policy.</summary>
public sealed record IdempotencyAdmissionOperationOptions
{
    /// <summary>Gets the accepted descriptor schema version.</summary>
    public int DescriptorVersion { get; init; } = 1;

    /// <summary>Gets the fixed replay-result retention tier.</summary>
    public IdempotencyReplayRetentionTier RetentionTier { get; init; } = IdempotencyReplayRetentionTier.Mutation;
}
