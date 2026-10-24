using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Issues a proof binding one workload command, represented principal and every metadata byte.</summary>
public interface IEventStoreGatewayBoundCommandProofSource
{
    /// <summary>Returns a signed proof, or null when the exact command scope is not authorized.</summary>
    ValueTask<string?> IssueAsync(BoundWorkloadCommandRequest request, CancellationToken cancellationToken);
}
