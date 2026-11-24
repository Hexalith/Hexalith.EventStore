using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Explicit unit-test substitute; full HTTP enforcement and activation are exercised by the owned catalog host drill.</summary>
internal sealed class DomainRouteCatalogIsolatedGuard(bool deny = false) : IDomainRouteCatalogGuard
{
    public Task<RouteCatalogDispatchLease?> AcquireAsync(EndpointFilterInvocationContext context)
        => deny
            ? Task.FromException<RouteCatalogDispatchLease?>(new RouteCatalogUnavailableException("Fixture admission denied."))
            : Task.FromResult<RouteCatalogDispatchLease?>(null);
}
