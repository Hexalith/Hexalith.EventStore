using Hexalith.EventStore.Controllers;

using Microsoft.AspNetCore.Builder;

namespace Hexalith.EventStore.Extensions;

/// <summary>Applies the gateway's existing projection notification topic binding to external package hosts.</summary>
public static class ProjectionNotificationEndpointExtensions
{
    /// <summary>Binds the actual notification controller to the configured pub/sub component.</summary>
    /// <param name="endpoints">The mapped gateway controllers.</param>
    /// <param name="services">The actual gateway service provider.</param>
    /// <param name="declarativeSubscriptions">Whether Dapr uses owner-provided declarative subscriptions.</param>
    /// <returns>The same controller endpoint builder.</returns>
    public static ControllerActionEndpointConventionBuilder WithEventStoreProjectionNotifications(
        this ControllerActionEndpointConventionBuilder endpoints, IServiceProvider services, bool declarativeSubscriptions)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(services);
        endpoints.Add(endpoint => ProjectionNotificationTopicAttribute.Bind(endpoint, services, declarativeSubscriptions));
        return endpoints;
    }
}
