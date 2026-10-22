using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Maps the Dapr actor routes that deliver typed-reminder convergence calls and callbacks.</summary>
public static class EventStoreReminderEndpointExtensions
{
    private const string ReminderCallbackRoute = "actors/{actorTypeName}/{actorId}/method/remind/{reminderName}";

    /// <summary>
    /// Maps the Dapr actor handlers, with the sidecar-channel policy, unless the host already mapped the actor
    /// reminder route. A host that maps the handlers itself must apply
    /// <c>.RequireEventStoreSidecarChannel()</c>. The app-channel token filter is installed by
    /// <c>AddEventStoreReminders</c> and guards the reminder actor routes either way.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>AddEventStoreReminders</c> was not called.</exception>
    public static IEndpointRouteBuilder MapEventStoreReminders(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.ServiceProvider.GetService<ReminderIntentIndex>() is null)
        {
            throw new InvalidOperationException("AddEventStoreReminders must be called before MapEventStoreReminders.");
        }

        if (!IsReminderCallbackRouteMapped(endpoints))
        {
            // Actor runtime calls are sidecar-originated. The framework marks its actor health route anonymous;
            // the sidecar-channel requirement removes that exception so only the three platform probes stay anonymous.
            _ = endpoints.MapActorsHandlers().RequireEventStoreSidecarChannel();
        }

        return endpoints;
    }

    private static bool IsReminderCallbackRouteMapped(IEndpointRouteBuilder endpoints)
    {
        foreach (EndpointDataSource dataSource in endpoints.DataSources)
        {
            foreach (Endpoint endpoint in dataSource.Endpoints)
            {
                if (endpoint is RouteEndpoint routeEndpoint
                    && string.Equals(
                        routeEndpoint.RoutePattern.RawText?.TrimStart('/'),
                        ReminderCallbackRoute,
                        StringComparison.OrdinalIgnoreCase)
                    && (routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } methods
                        || methods.HttpMethods.Contains(HttpMethods.Put, StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
