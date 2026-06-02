using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Routes a <see cref="ProjectionRequest"/> to the registered <see cref="IDomainProjectionHandler"/> whose
/// <see cref="IDomainProjectionHandler.Domain"/> matches. Backs the SDK's <c>/project</c> endpoint, mirroring
/// how <see cref="DomainServiceRequestRouter"/> backs <c>/process</c> and <see cref="DomainQueryDispatcher"/>
/// backs <c>/query</c>.
/// </summary>
public static class DomainProjectionDispatcher {
    /// <summary>
    /// Projects a request by dispatching it to the matching domain projection handler.
    /// </summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The projection request to dispatch.</param>
    /// <returns>
    /// The handler's <see cref="ProjectionResponse"/>, or <c>null</c> when no handler is registered for the
    /// request's domain (the endpoint maps a <c>null</c> result to <c>404 Not Found</c>).
    /// </returns>
    public static ProjectionResponse? Project(IServiceProvider serviceProvider, ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);

        IDomainProjectionHandler? handler = serviceProvider
            .GetServices<IDomainProjectionHandler>()
            .FirstOrDefault(h => string.Equals(h.Domain, request.Domain, StringComparison.OrdinalIgnoreCase));

        return handler?.Project(request);
    }
}
