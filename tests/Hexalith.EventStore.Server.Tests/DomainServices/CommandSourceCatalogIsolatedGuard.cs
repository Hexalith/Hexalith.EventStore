using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Explicit test-only catalog isolation for the original signed complete-source HTTP protocol fixture.</summary>
internal sealed class CommandSourceCatalogIsolatedGuard : IDomainRouteCatalogGuard
{
    public Task<RouteCatalogDispatchLease?> AcquireAsync(EndpointFilterInvocationContext context)
        => Task.FromResult<RouteCatalogDispatchLease?>(null);
}
