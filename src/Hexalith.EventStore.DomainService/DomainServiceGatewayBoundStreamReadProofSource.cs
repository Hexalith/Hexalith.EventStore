using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.DomainService;

/// <summary>Signs only exact owner stream scopes in the independently configured domain allowlist.</summary>
/// <param name="caller">The actual configured workload owner.</param>
/// <param name="privateKey">The independently provisioned owner signing key.</param>
/// <param name="domains">The exact allowed owner domains.</param>
/// <param name="clock">The proof lifetime clock.</param>
internal sealed class DomainServiceGatewayBoundStreamReadProofSource(
    string caller, string privateKey, IReadOnlySet<string> domains, TimeProvider clock) : IEventStoreGatewayStreamReadProofSource
{
    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(domains.Contains(request.Domain)
            ? BoundStreamReadProof.Issue(request, caller, privateKey, clock.GetUtcNow()) : null);
    }
}
