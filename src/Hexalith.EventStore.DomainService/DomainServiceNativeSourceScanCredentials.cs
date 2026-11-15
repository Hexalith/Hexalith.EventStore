using System.Security.Cryptography;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.DomainService;

/// <summary>Uses only the actual host issuer and its separately provisioned scan proof key.</summary>
internal sealed class DomainServiceNativeSourceScanCredentials(IWorkloadAssertionIssuer issuer,
    IConfiguration configuration, string domain, string audience, TimeProvider clock)
    : IEventStoreGatewayWorkloadAssertionSource, IEventStoreGatewayStreamReadProofSource
{
    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken = default)
        => IssueAsync(operation, tenant, domain, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, string requestedDomain, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return operation == EventStoreGatewayWorkloadOperations.NativeSourceScan && requestedDomain == domain
            && TenantCanonicalizer.TryCanonicalize(tenant, out string canonical) && canonical == tenant
            ? issuer.IssueAsync(new WorkloadAssertionRequest(audience, operation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
                }), cancellationToken) : ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Domain != domain
            || configuration["Authentication:WorkloadIssuer:Workload"] is not { Length: > 0 } caller
            || configuration["Authentication:NativeSourceScan:PrivateKey"] is not { Length: > 0 } key)
            return ValueTask.FromResult<string?>(null);
        try
        {
            return ValueTask.FromResult<string?>(BoundNativeSourceScanProof.Issue(request, caller, key, clock.GetUtcNow()));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            return ValueTask.FromResult<string?>(null);
        }
    }
}
