using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Maps the DAPR pub/sub subscription endpoint a service uses to consume a domain's published events. The route
/// requires the Dapr application-channel token (<see cref="EventStoreDomainServicePolicies.SidecarChannel"/>); it is
/// never anonymous.
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

        EventStoreSubscriptionSkipPolicy allSkips = EventStoreSubscriptionSkipPolicy.UnknownEventType
            | EventStoreSubscriptionSkipPolicy.NoHandlers | EventStoreSubscriptionSkipPolicy.AggregateMismatch;
        if (options.MaximumDeliveryAttempts is < 1 or > 1000
            || options.MaximumDeliveryAge is { } age && (age <= TimeSpan.Zero || age > TimeSpan.FromDays(365))
            || (options.AcknowledgedSkips & ~allSkips) != 0)
        {
            throw new InvalidOperationException("The subscription delivery policy is invalid.");
        }

        RouteHandlerBuilder endpoint = endpoints.MapPost(options.SubscriptionRoute, (
            HttpContext context,
            EventStoreDomainEventEnvelope envelope,
            EventStoreDomainEventProcessor processor,
            CancellationToken cancellationToken) => HandleDeliveryAsync(context, envelope, processor, options, cancellationToken))
            .RequireEventStoreSidecarChannel();

        string path = options.SubscriptionRoute;
        string pubsub = options.PubSubName;
        string topic = options.TopicName;
        bool declarative = endpoints.ServiceProvider.GetService<IConfiguration>()?.GetValue<bool>("Dapr:DeclarativeSubscriptions") == true;
        endpoint.Finally(builder => builder.Metadata.Add(new DomainRouteCatalogSidecarMetadata(builder.RequestDelegate, path, pubsub, topic, declarative, options.AcknowledgedSkips)));

        if (!declarative)
        {
            _ = endpoint.WithTopic(options.PubSubName, options.TopicName);
        }

        return endpoints;
    }

    internal static async Task<IResult> HandleDeliveryAsync(HttpContext context, EventStoreDomainEventEnvelope envelope,
        EventStoreDomainEventProcessor processor, EventStoreDomainEventsOptions options, CancellationToken cancellationToken)
    {
        IServiceProvider services = context.RequestServices;
        bool development = services.GetRequiredService<IHostEnvironment>().IsDevelopment();
        IEventStoreSubscriberPoisonSink? sink = services.GetService<IEventStoreSubscriberPoisonSink>();
        if (!development && !processor.HasDurableAdmission)
        {
            return MapProcessingResult(EventStoreDomainEventProcessingResult.RetryableInProgress);
        }

        string? id = Header(context, "cloudevent.id");
        string? source = Header(context, "cloudevent.source");
        bool replay = development && id is not null && sink is DevelopmentSubscriberPoisonSink developmentSink
            && Header(context, EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName) == developmentSink.OperationsAppId
            && Header(context, DevelopmentSubscriberPoisonSink.ReplayHeader) == id;
        bool metadataPresent = context.Request.Headers.Keys.Any(key => key.StartsWith("cloudevent.", StringComparison.OrdinalIgnoreCase));
        EventStoreSubscriberDelivery? delivery = null;
        if (metadataPresent || !development || sink is not null)
        {
            string? component = Header(context, "cloudevent.pubsubname");
            bool legacyReplay = replay && !context.Request.Headers.ContainsKey("cloudevent.pubsubname");
            if (id is null || source is null || Header(context, "cloudevent.topic") != options.TopicName
                || (component != options.PubSubName && !legacyReplay)
                || Header(context, "cloudevent.type") != envelope.EventTypeName || Header(context, "cloudevent.specversion") != "1.0")
            {
                return MapProcessingResult(EventStoreDomainEventProcessingResult.RetryableContentConflict);
            }

            delivery = new EventStoreSubscriberDelivery(options.PubSubName, options.TopicName, options.SubscriptionRoute,
                source, id, envelope);
            if (!delivery.IsValid())
            {
                return MapProcessingResult(EventStoreDomainEventProcessingResult.RetryableContentConflict);
            }
        }

        try
        {
            if (!replay && sink is not null && delivery is not null)
            {
                EventStoreSubscriberPoisonOutcome observation = await sink.ObserveAsync(delivery, null, cancellationToken)
                    .ConfigureAwait(false);
                if (observation == EventStoreSubscriberPoisonOutcome.Retained)
                {
                    return Results.Ok();
                }

                if (observation != EventStoreSubscriberPoisonOutcome.Retryable)
                {
                    return MapProcessingResult(EventStoreDomainEventProcessingResult.RetryableContentConflict);
                }
            }

            TimeProvider clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
            bool expired = !replay && options.MaximumDeliveryAge is { } maximumAge
                && clock.GetUtcNow() - envelope.Timestamp >= maximumAge;
            EventStoreDomainEventProcessingResult result;
            try
            {
                result = await processor.ProcessAsync(envelope, !expired, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (sink is null)
                {
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }

                return await RecordFailureAsync(sink, delivery, options,
                    expired ? EventStoreSubscriberFailureReason.DeliveryAgeExceeded : EventStoreSubscriberFailureReason.ProcessingFailure,
                    retryable: !expired, replay, cancellationToken).ConfigureAwait(false);
            }

            if (result is EventStoreDomainEventProcessingResult.Processed or EventStoreDomainEventProcessingResult.Duplicate)
            {
                if (replay)
                {
                    context.Response.Headers[DevelopmentSubscriberPoisonSink.OutcomeHeader] = "handled";
                }

                return Results.Ok();
            }

            if (!expired && !replay && await AllowsSkipAsync(context, options, result, cancellationToken).ConfigureAwait(false))
            {
                return Results.Ok();
            }

            (EventStoreSubscriberFailureReason reason, bool retryable) = expired
                ? (EventStoreSubscriberFailureReason.DeliveryAgeExceeded, false) : result switch
            {
                EventStoreDomainEventProcessingResult.SkippedUnknownEventType => (EventStoreSubscriberFailureReason.UnknownEventType, false),
                EventStoreDomainEventProcessingResult.SkippedNoHandlers => (EventStoreSubscriberFailureReason.NoHandlers, false),
                EventStoreDomainEventProcessingResult.SkippedAggregateMismatch => (EventStoreSubscriberFailureReason.AggregateMismatch, false),
                EventStoreDomainEventProcessingResult.FailedInvalidPayload => (EventStoreSubscriberFailureReason.InvalidPayload, false),
                EventStoreDomainEventProcessingResult.DeliveryAgeExceeded => (EventStoreSubscriberFailureReason.DeliveryAgeExceeded, false),
                EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch => (EventStoreSubscriberFailureReason.CapabilityUnavailable, true),
                EventStoreDomainEventProcessingResult.RetryableContentConflict => (EventStoreSubscriberFailureReason.ContentConflict, true),
                _ => (EventStoreSubscriberFailureReason.MarkerPending, true),
            };
            return await RecordFailureAsync(sink, delivery, options, reason, retryable, replay, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> RecordFailureAsync(IEventStoreSubscriberPoisonSink? sink, EventStoreSubscriberDelivery? delivery,
        EventStoreDomainEventsOptions options, EventStoreSubscriberFailureReason reason, bool retryable, bool replay, CancellationToken cancellationToken)
    {
        if (!replay && sink is not null && delivery is not null && await sink.ObserveAsync(delivery,
                new EventStoreSubscriberFailure(reason, retryable, options.MaximumDeliveryAttempts), cancellationToken)
                .ConfigureAwait(false) == EventStoreSubscriberPoisonOutcome.Retained)
        {
            return Results.Ok();
        }

        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<bool> AllowsSkipAsync(HttpContext context, EventStoreDomainEventsOptions options,
        EventStoreDomainEventProcessingResult result, CancellationToken cancellationToken)
    {
        EventStoreSubscriptionSkipPolicy skip = result switch
        {
            EventStoreDomainEventProcessingResult.SkippedUnknownEventType => EventStoreSubscriptionSkipPolicy.UnknownEventType,
            EventStoreDomainEventProcessingResult.SkippedNoHandlers => EventStoreSubscriptionSkipPolicy.NoHandlers,
            EventStoreDomainEventProcessingResult.SkippedAggregateMismatch => EventStoreSubscriptionSkipPolicy.AggregateMismatch,
            _ => EventStoreSubscriptionSkipPolicy.None,
        };
        if (skip == EventStoreSubscriptionSkipPolicy.None || (options.AcknowledgedSkips & skip) == 0
            || context.GetEndpoint() is not RouteEndpoint route
            || DomainRouteCatalogSidecarMetadata.ObserveEndpoint(route) is not { } observed
            || context.RequestServices.GetService<IRouteCatalogAuthority>() is not { } authority)
        {
            return false;
        }

        RouteCatalogDispatchLease lease = await authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using (lease.ConfigureAwait(false))
        {
            return lease.Snapshot.LocalAppId is { } localAppId && lease.Snapshot.AuxiliaryOperations.Any(operation =>
                operation.AppId == localAppId && operation.Method == options.SubscriptionRoute.TrimStart('/')
                && operation.Operation == DomainRouteCatalogSidecarMetadata.Operation && operation.Sidecar == observed);
        }
    }

    private static string? Header(HttpContext context, string name)
        => context.Request.Headers.TryGetValue(name, out var values) && values.Count == 1 ? values[0] : null;

    internal static IResult MapProcessingResult(EventStoreDomainEventProcessingResult result)
        => result switch
        {
            EventStoreDomainEventProcessingResult.Processed or EventStoreDomainEventProcessingResult.Duplicate => Results.Ok(),
            EventStoreDomainEventProcessingResult.RetryableInProgress => Results.StatusCode(StatusCodes.Status500InternalServerError),
            _ => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
        };
}
