using Hexalith.EventStore.Client.Subscriptions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Maps the DAPR pub/sub subscription endpoint a service uses to consume a domain's published events.
/// </summary>
/// <remarks>
/// This is the platform generalization of the per-domain subscription endpoints domain modules previously
/// hand-wrote (e.g. <c>MapTenantEventSubscription</c>). Pair with
/// <c>AddEventStoreDomainEvents</c> (registration) and ensure <c>app.UseCloudEvents()</c> and
/// <c>app.MapSubscribeHandler()</c> are configured.
/// </remarks>
public static class EventStoreDomainEventsEndpointExtensions {
    /// <summary>
    /// Maps the configured DAPR pub/sub subscription endpoint (route, pub/sub component, and topic come
    /// from <see cref="EventStoreDomainEventsOptions"/>) that delivers consumed domain events to the
    /// registered <see cref="EventStoreDomainEventProcessor"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapEventStoreDomainEvents(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        EventStoreDomainEventsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value;

        _ = endpoints.MapPost(options.SubscriptionRoute, async (
            EventStoreDomainEventEnvelope envelope,
            EventStoreDomainEventProcessor processor,
            CancellationToken cancellationToken) => {
                EventStoreDomainEventProcessingResult result = await processor
                    .ProcessAsync(envelope, cancellationToken)
                    .ConfigureAwait(false);
                return result switch {
                    EventStoreDomainEventProcessingResult.Processed => Results.Ok(),
                    EventStoreDomainEventProcessingResult.Duplicate => Results.Ok(),
                    EventStoreDomainEventProcessingResult.SkippedUnknownEventType => Results.Ok(),
                    EventStoreDomainEventProcessingResult.SkippedNoHandlers => Results.Ok(),
                    EventStoreDomainEventProcessingResult.FailedInvalidPayload => Results.Problem(
                        title: "Domain event processing failed.",
                        detail: "The domain event payload could not be deserialized.",
                        statusCode: StatusCodes.Status500InternalServerError),
                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
                };
            }).WithTopic(options.PubSubName, options.TopicName);

        return endpoints;
    }
}
