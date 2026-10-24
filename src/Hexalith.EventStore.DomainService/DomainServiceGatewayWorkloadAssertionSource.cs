using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.DomainService;

/// <summary>Issues only the gateway command/stream grants of one configured domain host.</summary>
internal sealed class DomainServiceGatewayWorkloadAssertionSource(
    IWorkloadAssertionIssuer issuer,
    IReadOnlySet<string> domains,
    string audience) : IEventStoreGatewayWorkloadAssertionSource
{
    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken = default)
    {
        return domains.Count == 1
            ? IssueAsync(operation, tenant, domains.Single(), cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(string operation, string tenant, string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return domains.Contains(domain) && (operation is EventStoreGatewayWorkloadOperations.CommandSubmit or EventStoreGatewayWorkloadOperations.StreamRead)
            ? issuer.IssueAsync(new WorkloadAssertionRequest(audience, operation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
                }), cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }
}
