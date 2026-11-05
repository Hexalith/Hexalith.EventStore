using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.DomainService;

/// <summary>Issues only the configured host's exact metadata-read credentials; it exposes no command client.</summary>
internal sealed class DomainServiceNativeSourceCredentials(
    IWorkloadAssertionIssuer issuer, IConfiguration configuration, string domain, string audience, TimeProvider clock)
    : IEventStoreGatewayWorkloadAssertionSource, IEventStoreGatewayStreamReadProofSource
{
    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken = default)
        => IssueAsync(operation, tenant, domain, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(string operation, string tenant, string requestedDomain, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return operation == EventStoreGatewayWorkloadOperations.StreamRead && requestedDomain == domain
            && TenantCanonicalizer.TryCanonicalize(tenant, out string canonical) && canonical == tenant
            ? issuer.IssueAsync(new WorkloadAssertionRequest(audience, EventStoreGatewayWorkloadOperations.StreamRead,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = requestedDomain,
                }), cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Domain != domain || request.FromSequence != 0 || request.ToSequence != 0 || request.PageSize != 1
            || !request.RequireNativeProof || request.Checkpoint is not null || request.ContinuationToken is not null
            || request.ProjectionName is not null
            || !TenantCanonicalizer.TryCanonicalize(request.Tenant, out string canonical) || canonical != request.Tenant
            || configuration["Authentication:WorkloadIssuer:Workload"] is not { Length: > 0 } caller
            || configuration["Authentication:BoundStreamRead:PrivateKey"] is not { Length: > 0 } key)
            return ValueTask.FromResult<string?>(null);
        try
        {
            return ValueTask.FromResult<string?>(BoundStreamReadProof.Issue(request, caller, key, clock.GetUtcNow()));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            return ValueTask.FromResult<string?>(null);
        }
    }
}
