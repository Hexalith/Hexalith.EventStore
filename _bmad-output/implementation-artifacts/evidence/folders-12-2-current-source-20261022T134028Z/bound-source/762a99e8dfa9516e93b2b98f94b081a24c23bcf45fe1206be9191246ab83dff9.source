using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.DomainService;

/// <summary>Issues only the gateway command/stream grants of one configured domain host.</summary>
internal sealed class DomainServiceGatewayWorkloadAssertionSource(
    IWorkloadAssertionIssuer issuer,
    string domain,
    string audience) : IEventStoreGatewayWorkloadAssertionSource
{
    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return operation is EventStoreGatewayWorkloadOperations.CommandSubmit or EventStoreGatewayWorkloadOperations.StreamRead
            ? issuer.IssueAsync(new WorkloadAssertionRequest(audience, operation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
                }), cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }
}
