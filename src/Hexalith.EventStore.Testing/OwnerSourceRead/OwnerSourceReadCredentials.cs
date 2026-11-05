using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>Uses the actual Tenants issuer and owner-bound key only for the selected original SDK request.</summary>
internal sealed class OwnerSourceReadCredentials(IWorkloadAssertionIssuer issuer, string key, TimeProvider clock)
    : IEventStoreGatewayWorkloadAssertionSource, IEventStoreGatewayStreamReadProofSource
{
    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken)
        => ValueTask.FromResult<string?>(null);

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, string domain, CancellationToken cancellationToken)
        => operation == EventStoreGatewayWorkloadOperations.StreamRead && tenant == OwnerSourceReadSelection.Scope
            && domain == OwnerSourceReadSelection.Administrator
            ? issuer.IssueAsync(new("eventstore", operation, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
            }), cancellationToken)
            : ValueTask.FromResult<string?>(null);

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(request.Tenant == OwnerSourceReadSelection.Scope
            && request.Domain == OwnerSourceReadSelection.Administrator && request.AggregateId == OwnerSourceReadSelection.Administrator
            ? BoundStreamReadProof.Issue(request, "tenants", key, clock.GetUtcNow()) : null);
    }
}
