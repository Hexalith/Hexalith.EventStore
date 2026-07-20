using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Builds server-trusted canonical mutation intent after authorization and validation.</summary>
public interface IIdempotencyIntentAdapter
{
    /// <summary>Gets the exact command type handled by this adapter.</summary>
    string CommandType { get; }

    /// <summary>Gets the stable server-owned adapter identifier.</summary>
    string AdapterId { get; }

    /// <summary>Gets the stable server-owned operation identifier.</summary>
    string OperationId { get; }

    /// <summary>Gets the supported canonical descriptor version.</summary>
    int DescriptorVersion { get; }

    /// <summary>Gets the fixed replay retention tier for the operation.</summary>
    IdempotencyReplayRetentionTier RetentionTier { get; }

    /// <summary>Creates schema-normalized semantic intent for an authorized and validated command.</summary>
    /// <param name="command">The authorized and structurally validated command.</param>
    /// <returns>The trusted semantic intent fields.</returns>
    IdempotencyCanonicalIntent CreateIntent(SubmitCommand command);
}
