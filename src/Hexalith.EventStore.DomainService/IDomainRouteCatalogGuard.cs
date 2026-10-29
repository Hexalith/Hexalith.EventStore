using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Receiver validation seam; only isolated endpoint unit fixtures substitute this trusted platform guard.</summary>
internal interface IDomainRouteCatalogGuard
{
    /// <summary>Acquires and validates exact shared authority before any endpoint work.</summary>
    Task<RouteCatalogDispatchLease?> AcquireAsync(EndpointFilterInvocationContext context);
}
