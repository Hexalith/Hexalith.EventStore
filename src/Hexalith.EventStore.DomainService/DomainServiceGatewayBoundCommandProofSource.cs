using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.DomainService;

internal sealed class DomainServiceGatewayBoundCommandProofSource(
    string caller, string privateKey, IReadOnlySet<string> domains, TimeProvider timeProvider) : IEventStoreGatewayBoundCommandProofSource
{
    public ValueTask<string?> IssueAsync(BoundWorkloadCommandRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        return ValueTask.FromResult<string?>(domains.Contains(request.Command.Domain)
            ? BoundWorkloadCommandProof.Issue(request, caller, privateKey, timeProvider.GetUtcNow()) : null);
    }
}
