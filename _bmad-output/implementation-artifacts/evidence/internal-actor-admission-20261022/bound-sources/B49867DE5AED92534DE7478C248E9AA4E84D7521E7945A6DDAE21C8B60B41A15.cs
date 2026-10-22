using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Issues a resource-bound proof for one workload stream read.</summary>
public interface IEventStoreGatewayStreamReadProofSource
{
    /// <summary>Returns a signed proof, or null when the exact scope cannot be proven.</summary>
    ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken);
}
