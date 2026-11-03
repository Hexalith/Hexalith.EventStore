using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Holds receiver authority through the actual asynchronous response execution.</summary>
internal sealed class DomainRouteCatalogResult(IResult result, RouteCatalogDispatchLease lease, bool trustedReceiver) : IResult
{
    /// <summary>Preserves ordinary extension-guard response execution without conferring a trusted receiver marker.</summary>
    internal DomainRouteCatalogResult(IResult result, RouteCatalogDispatchLease lease) : this(result, lease, false) { }

    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        await using var release = lease.ConfigureAwait(false);
        using var scope = trustedReceiver ? RouteCatalogRemoteCall.EnterReceiver(lease, httpContext.RequestAborted)
            : RouteCatalogRemoteCall.Enter(lease);
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
    }
}
