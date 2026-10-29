using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Holds receiver authority through the actual asynchronous response execution.</summary>
internal sealed class DomainRouteCatalogResult(IResult result, RouteCatalogDispatchLease lease) : IResult
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        await using var release = lease.ConfigureAwait(false);
        using var scope = RouteCatalogRemoteCall.Enter(lease);
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
    }
}
