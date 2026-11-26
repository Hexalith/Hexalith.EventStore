using Dapr;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tests for the generic DAPR domain-event subscription endpoint.
/// </summary>
public sealed class EventStoreDomainEventsEndpointExtensionsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapEventStoreDomainEvents_MapsConfiguredRouteAndTopicMetadata(bool declarative) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["Dapr:DeclarativeSubscriptions"] = declarative.ToString();
        _ = builder.Services.AddLogging();
        _ = builder.Services.AddSingleton(Substitute.For<DaprClient>());
        _ = builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        _ = builder.Services.Configure<EventStoreDomainEventsOptions>(options => {
            options.SubscriptionRoute = "/widgets/events";
            options.PubSubName = "event-pubsub";
            options.TopicName = "widgets.events";
        });
        WebApplication app = builder.Build();

        _ = app.MapEventStoreDomainEvents();
        IEndpointRouteBuilder endpoints = app;

        RouteEndpoint endpoint = endpoints.DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => string.Equals(endpoint.RoutePattern.RawText, "/widgets/events", StringComparison.Ordinal));

        IHttpMethodMetadata methodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>().ShouldNotBeNull();
        methodMetadata.HttpMethods.ShouldContain(HttpMethods.Post);

        var owner = endpoint.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>().ShouldNotBeNull().Observe(endpoint);
        owner.PubSubName.ShouldBe("event-pubsub");
        owner.TopicName.ShouldBe("widgets.events");
        owner.CodeDigest.Length.ShouldBe(64);
        var copied = new RouteEndpoint(static _ => Task.CompletedTask, endpoint.RoutePattern, endpoint.Order, endpoint.Metadata, endpoint.DisplayName);
        Should.Throw<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException>(() =>
            copied.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>()!.Observe(copied));

        if (declarative)
        {
            endpoint.Metadata.GetMetadata<ITopicMetadata>().ShouldBeNull();
            return;
        }

        ITopicMetadata topicMetadata = endpoint.Metadata.GetMetadata<ITopicMetadata>().ShouldNotBeNull();
        topicMetadata.PubsubName.ShouldBe("event-pubsub");
        topicMetadata.Name.ShouldBe("widgets.events");
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("component")]
    [InlineData("topic")]
    [InlineData("extra")]
    [InlineData("raw")]
    [InlineData("dead-letter")]
    [InlineData("declarative-extra")]
    public async Task SameActualDelegateCannotHideChangedSubscriptionMetadata(string mutation)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Dapr:DeclarativeSubscriptions"] = (mutation == "declarative-extra").ToString();
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly, options => {
            options.SubscriptionRoute = "/widgets/events"; options.PubSubName = "event-pubsub"; options.TopicName = "widgets.events";
        });
        await using var app = builder.Build();
        app.MapEventStoreDomainEvents();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single();
        var witness = endpoint.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>().ShouldNotBeNull();
        witness.Observe(endpoint).TopicName.ShouldBe("widgets.events");
        var metadata = endpoint.Metadata.Where(item => item is not ITopicMetadata).ToList();
        if (mutation != "removed") metadata.Add(mutation switch {
            "component" => new TopicAttribute("foreign-component", "widgets.events") { EnableRawPayload = false },
            "topic" => new TopicAttribute("event-pubsub", "foreign.events") { EnableRawPayload = false },
            "raw" => new TopicAttribute("event-pubsub", "widgets.events") { EnableRawPayload = true },
            "dead-letter" => new TopicAttribute("event-pubsub", "widgets.events") { EnableRawPayload = false, DeadLetterTopic = "foreign.dead-letter" },
            _ => new TopicAttribute("event-pubsub", "widgets.events") { EnableRawPayload = false },
        });
        if (mutation == "extra") metadata.Add(new TopicAttribute("event-pubsub", "widgets.events") { EnableRawPayload = false });
        var changed = new RouteEndpoint(endpoint.RequestDelegate, endpoint.RoutePattern, endpoint.Order, new EndpointMetadataCollection(metadata), endpoint.DisplayName);
        Should.Throw<RouteCatalogUnavailableException>(() => witness.Observe(changed));
    }

    [Theory]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retained, false, StatusCodes.Status200OK)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Conflict, false, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retryable, false, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retained, true, StatusCodes.Status200OK)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retryable, true, StatusCodes.Status503ServiceUnavailable)]
    public async Task TerminalOrExpiredDeliveryIsAcknowledgedOnlyAfterExactPoisonEvidence(
        EventStoreSubscriberPoisonOutcome outcome, bool expired, int expectedStatusCode)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        IEventStoreSubscriberPoisonSink sink = Substitute.For<IEventStoreSubscriberPoisonSink>();
        _ = sink.ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(), null, Arg.Any<CancellationToken>())
            .Returns(EventStoreSubscriberPoisonOutcome.Retryable);
        _ = sink.ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(), Arg.Is<EventStoreSubscriberFailure>(failure => failure != null), Arg.Any<CancellationToken>())
            .Returns(outcome);
        builder.Services.AddSingleton(sink);
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        await using WebApplication app = builder.Build();
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        options.MaximumDeliveryAge = expired ? TimeSpan.FromDays(1) : null;
        EventStoreSubscriberFailureReason reason = expired
            ? EventStoreSubscriberFailureReason.DeliveryAgeExceeded : EventStoreSubscriberFailureReason.UnknownEventType;
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var envelope = new EventStoreDomainEventEnvelope(messageId, "widget-1", "tenant-a", "Missing.Widget", 1,
            DateTimeOffset.UnixEpoch, "corr", "json", "{}"u8.ToArray()) { Domain = "widgets" };
        var context = DeliveryContext(app.Services, options, envelope);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(context, envelope,
            app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(expectedStatusCode);
        _ = await sink.Received(1).ObserveAsync(
            Arg.Is<EventStoreSubscriberDelivery>(delivery => delivery.Id == messageId && delivery.SubscriptionRoute == options.SubscriptionRoute),
            Arg.Is<EventStoreSubscriberFailure>(failure => !failure.Retryable && failure.Reason == reason),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("foreign-component")]
    public async Task UnboundDeliveryRemainsRetryableWithoutObservingPoison(string? component)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        IEventStoreSubscriberPoisonSink sink = Substitute.For<IEventStoreSubscriberPoisonSink>();
        builder.Services.AddSingleton(sink);
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        await using WebApplication app = builder.Build();
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var envelope = new EventStoreDomainEventEnvelope(messageId, "widget-1", "tenant-a", "Missing.Widget", 1,
            DateTimeOffset.UnixEpoch, "corr", "json", "{}"u8.ToArray()) { Domain = "widgets" };
        var context = DeliveryContext(app.Services, options, envelope);
        if (component is null)
        {
            _ = context.Request.Headers.Remove("cloudevent.pubsubname");
        }
        else
        {
            context.Request.Headers["cloudevent.pubsubname"] = component;
        }

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(context, envelope,
            app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        sink.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EventStoreSubscriptionSkipPolicy.UnknownEventType, StatusCodes.Status200OK)]
    [InlineData(EventStoreSubscriptionSkipPolicy.NoHandlers, StatusCodes.Status503ServiceUnavailable)]
    public async Task SkipAcknowledgementRequiresTheExactCommittedSubscriptionPolicy(
        EventStoreSubscriptionSkipPolicy catalogPolicy, int statusCode)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        builder.Services.AddSingleton(authority);
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly, options =>
        {
            options.TopicName = "widgets.events";
            options.SubscriptionRoute = "/widgets/events";
            options.AcknowledgedSkips = EventStoreSubscriptionSkipPolicy.UnknownEventType;
        });
        await using WebApplication app = builder.Build();
        _ = app.MapEventStoreDomainEvents();
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single();
        var observed = DomainRouteCatalogSidecarMetadata.ObserveEndpoint(endpoint).ShouldNotBeNull();
        observed.AcknowledgedSkips.ShouldBe(EventStoreSubscriptionSkipPolicy.UnknownEventType);
        var auxiliary = new RouteCatalogAuxiliaryOperation("widgets", "widgets/events", DomainRouteCatalogSidecarMetadata.Operation, "widgets")
        {
            Sidecar = observed with { AcknowledgedSkips = catalogPolicy },
        };
        var query = new RouteContractDeclaration("widget-query", "query", "widgets", "get-widget", "widgets", "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
            new("Sample.WidgetQuery", "exact", ["get-widget"], "domain-service", "projection", []));
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Seal(new(2, 1, "Development", false, [query],
            [new(query.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(query), "widgets", "query", "v1",
                query.CredentialKind, query.Operation, false, "tenant-a")], [], "", ""));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog),
            1, [auxiliary], localAppId: "widgets");
        int releases = 0;
        _ = authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () =>
        {
            releases++;
            return Task.CompletedTask;
        }));
        var envelope = new EventStoreDomainEventEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(), "widget-1", "tenant-a",
            "Missing.Widget", 1, DateTimeOffset.UnixEpoch, "corr", "json", "{}"u8.ToArray()) { Domain = "widgets" };
        EventStoreDomainEventsOptions options = app.Services.GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value;
        var context = DeliveryContext(app.Services, options, envelope);
        context.SetEndpoint(endpoint);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(context, envelope,
            app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(statusCode);
        releases.ShouldBe(1);
    }

    private static DefaultHttpContext DeliveryContext(IServiceProvider services, EventStoreDomainEventsOptions options,
        EventStoreDomainEventEnvelope envelope)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers["cloudevent.id"] = envelope.MessageId;
        context.Request.Headers["cloudevent.source"] = $"hexalith-eventstore/{envelope.TenantId}/{envelope.Domain}";
        context.Request.Headers["cloudevent.topic"] = options.TopicName;
        context.Request.Headers["cloudevent.pubsubname"] = options.PubSubName;
        context.Request.Headers["cloudevent.type"] = envelope.EventTypeName;
        context.Request.Headers["cloudevent.specversion"] = "1.0";
        return context;
    }

    [Theory]
    [InlineData(EventStoreDomainEventProcessingResult.Processed, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.Duplicate, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedUnknownEventType, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedNoHandlers, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedAggregateMismatch, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreDomainEventProcessingResult.FailedInvalidPayload, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(EventStoreDomainEventProcessingResult.RetryableInProgress, StatusCodes.Status500InternalServerError)]
    [InlineData(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch, StatusCodes.Status503ServiceUnavailable)]
    public void MapProcessingResult_MapsProcessorOutcomesIntentionally(
        EventStoreDomainEventProcessingResult processingResult,
        int expectedStatusCode) {
        IResult result = EventStoreDomainEventsEndpointExtensions.MapProcessingResult(processingResult);

        IStatusCodeHttpResult statusResult = result.ShouldBeAssignableTo<IStatusCodeHttpResult>();
        statusResult.StatusCode.ShouldBe(expectedStatusCode);
    }
}
