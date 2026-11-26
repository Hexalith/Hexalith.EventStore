using System.Text.Json;

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
using Microsoft.Extensions.Logging;
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
        CommittedRouteCatalogSnapshot snapshot = SkipCatalog(observed, catalogPolicy);
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

    [Theory]
    [InlineData(false, StatusCodes.Status500InternalServerError, 0)]
    [InlineData(true, StatusCodes.Status200OK, 1)]
    public async Task ProductionDeliveryRequiresDurableContentBoundAdmission(bool durable, int expectedStatusCode, int handlerCalls)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        var logs = new RecordingLoggerProvider();
        builder.Logging.AddProvider(logs);
        var handler = new CountingWidgetHandler();
        builder.Services.AddSingleton<IEventStoreDomainEventHandler<WidgetCreated>>(handler);
        if (durable)
        {
            builder.Services.AddSingleton<IEventStoreDomainEventMarkerStore>(new DurableMarkerStore());
        }

        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        await using WebApplication app = builder.Build();
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(DeliveryContext(app.Services, options, envelope),
            envelope, app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(expectedStatusCode);
        handler.Count.ShouldBe(handlerCalls);
        logs.EventIds.Contains(200401).ShouldBe(!durable);
    }

    [Fact]
    public async Task ExpiredKnownEventIsCapturedWithoutRunningItsHandler()
    {
        await using WebApplication app = BuildDevelopment(
            EventStoreSubscriberPoisonOutcome.Retryable, EventStoreSubscriberPoisonOutcome.Retained, out IEventStoreSubscriberPoisonSink sink,
            out CountingWidgetHandler handler);
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        options.MaximumDeliveryAge = TimeSpan.FromMinutes(5);
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow.AddHours(-1));

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(DeliveryContext(app.Services, options, envelope),
            envelope, app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        handler.Count.ShouldBe(0);
        _ = await sink.Received(1).ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(),
            Arg.Is<EventStoreSubscriberFailure>(failure => failure.Reason == EventStoreSubscriberFailureReason.DeliveryAgeExceeded && !failure.Retryable),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpiredDeliveryOwnedByAnotherAttemptRecordsPendingRatherThanAge()
    {
        await using WebApplication app = BuildDevelopment(
            EventStoreSubscriberPoisonOutcome.Retryable, EventStoreSubscriberPoisonOutcome.Retryable, out IEventStoreSubscriberPoisonSink sink,
            out CountingWidgetHandler handler, services => services.AddSingleton<IEventStoreDomainEventMarkerStore>(new InProgressMarkerStore()));
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        options.MaximumDeliveryAge = TimeSpan.FromMinutes(5);
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow.AddHours(-1));

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(DeliveryContext(app.Services, options, envelope),
            envelope, app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        handler.Count.ShouldBe(0);
        _ = await sink.Received(1).ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(),
            Arg.Is<EventStoreSubscriberFailure>(failure => failure.Reason == EventStoreSubscriberFailureReason.MarkerPending && failure.Retryable),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retained, StatusCodes.Status200OK)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Conflict, StatusCodes.Status503ServiceUnavailable)]
    public async Task PreProcessingRetainedCheckDecidesBeforeAnyHandlerRuns(EventStoreSubscriberPoisonOutcome outcome, int expectedStatusCode)
    {
        await using WebApplication app = BuildDevelopment(
            outcome, EventStoreSubscriberPoisonOutcome.Retained, out IEventStoreSubscriberPoisonSink sink, out CountingWidgetHandler handler);
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(DeliveryContext(app.Services, options, envelope),
            envelope, app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(expectedStatusCode);
        handler.Count.ShouldBe(0);
        _ = await sink.Received(1).ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(), Arg.Any<EventStoreSubscriberFailure?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryCarriesTheSubscriptionRouteWithOneLeadingSlash()
    {
        await using WebApplication app = BuildDevelopment(
            EventStoreSubscriberPoisonOutcome.Retained, EventStoreSubscriberPoisonOutcome.Retained, out IEventStoreSubscriberPoisonSink sink, out _);
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        options.SubscriptionRoute = "widgets/events";
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow);

        _ = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(DeliveryContext(app.Services, options, envelope),
            envelope, app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        _ = await sink.Received(1).ObserveAsync(Arg.Is<EventStoreSubscriberDelivery>(delivery => delivery.SubscriptionRoute == "/widgets/events"),
            null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidReplaySkipsTheSinkAndReportsHandled()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        var handler = new CountingWidgetHandler();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Services.AddSingleton<IEventStoreSubscriberPoisonSink>(
            new DevelopmentSubscriberPoisonSink(dapr, Substitute.For<IHttpClientFactory>(), "eventstore-operations"));
        builder.Services.AddSingleton<IEventStoreDomainEventHandler<WidgetCreated>>(handler);
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        await using WebApplication app = builder.Build();
        var options = EventStoreDomainEventsOptions.ForDomain("widgets");
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow);
        DefaultHttpContext context = ReplayContext(app.Services, options, envelope);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(context, envelope,
            app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.Headers[DevelopmentSubscriberPoisonSink.OutcomeHeader].ToString().ShouldBe("handled");
        handler.Count.ShouldBe(1);
        dapr.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplayedSkipStaysRetryableEvenWhenTheSkipPolicyAllowsIt()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Services.AddSingleton(authority);
        builder.Services.AddSingleton<IEventStoreSubscriberPoisonSink>(
            new DevelopmentSubscriberPoisonSink(dapr, Substitute.For<IHttpClientFactory>(), "eventstore-operations"));
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
        CommittedRouteCatalogSnapshot snapshot = SkipCatalog(
            DomainRouteCatalogSidecarMetadata.ObserveEndpoint(endpoint).ShouldNotBeNull(), EventStoreSubscriptionSkipPolicy.UnknownEventType);
        int releases = 0;
        _ = authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () =>
        {
            releases++;
            return Task.CompletedTask;
        }));
        var envelope = new EventStoreDomainEventEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(), "widget-1", "tenant-a",
            "Missing.Widget", 1, DateTimeOffset.UtcNow, "corr", "json", "{}"u8.ToArray()) { Domain = "widgets" };
        EventStoreDomainEventsOptions options = app.Services.GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value;
        DefaultHttpContext context = ReplayContext(app.Services, options, envelope);
        context.SetEndpoint(endpoint);

        IResult result = await EventStoreDomainEventsEndpointExtensions.HandleDeliveryAsync(context, envelope,
            app.Services.GetRequiredService<EventStoreDomainEventProcessor>(), options, CancellationToken.None);

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.ContainsKey(DevelopmentSubscriberPoisonSink.OutcomeHeader).ShouldBeFalse();
        releases.ShouldBe(0);
        dapr.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("attempts", "MaximumDeliveryAttempts")]
    [InlineData("age", "MaximumDeliveryAge")]
    [InlineData("skips", "AcknowledgedSkips")]
    public async Task InvalidDeliveryPolicyNamesTheOffendingSetting(string setting, string expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly, options =>
        {
            switch (setting)
            {
                case "attempts":
                    options.MaximumDeliveryAttempts = 0;
                    break;
                case "age":
                    options.MaximumDeliveryAge = TimeSpan.Zero;
                    break;
                default:
                    options.AcknowledgedSkips = (EventStoreSubscriptionSkipPolicy)64;
                    break;
            }
        });
        await using WebApplication app = builder.Build();

        Should.Throw<InvalidOperationException>(() => app.MapEventStoreDomainEvents()).Message.ShouldContain(expected);
    }

    [Fact]
    public async Task DevelopmentSinkPostsTheObservationShapeAndParsesTheOutcome()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        _ = dapr.CreateInvokeMethodRequest(HttpMethod.Post, "eventstore-operations", "internal/dead-letters/subscriber-delivery")
            .Returns(_ => new HttpRequestMessage(HttpMethod.Post, "http://localhost/internal/dead-letters/subscriber-delivery"));
        var http = new CapturingSinkHandler();
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        _ = clients.CreateClient(nameof(DevelopmentSubscriberPoisonSink)).Returns(_ => new HttpClient(http, disposeHandler: false));
        var sink = new DevelopmentSubscriberPoisonSink(dapr, clients, "eventstore-operations");
        EventStoreDomainEventEnvelope envelope = KnownEnvelope(DateTimeOffset.UtcNow);
        var delivery = new EventStoreSubscriberDelivery("pubsub", "widgets.events", "/widgets/events",
            "hexalith-eventstore/tenant-a/widgets", envelope.MessageId, envelope);
        var failure = new EventStoreSubscriberFailure(EventStoreSubscriberFailureReason.ProcessingFailure, true, 10);

        foreach (EventStoreSubscriberPoisonOutcome outcome in Enum.GetValues<EventStoreSubscriberPoisonOutcome>())
        {
            http.Outcome = outcome;
            (await sink.ObserveAsync(delivery, failure)).ShouldBe(outcome);
        }

        using (JsonDocument body = JsonDocument.Parse(http.Body.ShouldNotBeNull()))
        {
            EventStoreSubscriberDelivery sent = body.RootElement.GetProperty("delivery")
                .Deserialize<EventStoreSubscriberDelivery>(JsonSerializerOptions.Web).ShouldNotBeNull();
            sent.Id.ShouldBe(delivery.Id);
            sent.SubscriptionRoute.ShouldBe("/widgets/events");
            sent.Envelope.Payload.ShouldBe(envelope.Payload);
            sent.IsValid().ShouldBeTrue();
            body.RootElement.GetProperty("failure").Deserialize<EventStoreSubscriberFailure>(JsonSerializerOptions.Web).ShouldBe(failure);
        }

        http.Outcome = EventStoreSubscriberPoisonOutcome.Retryable;
        _ = await sink.ObserveAsync(delivery, null);
        using (JsonDocument check = JsonDocument.Parse(http.Body.ShouldNotBeNull()))
        {
            check.RootElement.GetProperty("failure").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        http.Status = System.Net.HttpStatusCode.ServiceUnavailable;
        _ = await Should.ThrowAsync<HttpRequestException>(() => sink.ObserveAsync(delivery, failure));
    }

    private static WebApplication BuildDevelopment(
        EventStoreSubscriberPoisonOutcome precheck, EventStoreSubscriberPoisonOutcome failureOutcome,
        out IEventStoreSubscriberPoisonSink sink, out CountingWidgetHandler handler, Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        sink = Substitute.For<IEventStoreSubscriberPoisonSink>();
        _ = sink.ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(), null, Arg.Any<CancellationToken>()).Returns(precheck);
        _ = sink.ObserveAsync(Arg.Any<EventStoreSubscriberDelivery>(), Arg.Is<EventStoreSubscriberFailure>(failure => failure != null),
            Arg.Any<CancellationToken>()).Returns(failureOutcome);
        handler = new CountingWidgetHandler();
        builder.Services.AddSingleton(sink);
        builder.Services.AddSingleton<IEventStoreDomainEventHandler<WidgetCreated>>(handler);
        configure?.Invoke(builder.Services);
        builder.Services.AddEventStoreDomainEvents(typeof(WidgetCreated).Assembly);
        return builder.Build();
    }

    private static EventStoreDomainEventEnvelope KnownEnvelope(DateTimeOffset timestamp)
        => new(UniqueIdHelper.GenerateSortableUniqueStringId(), "widget-1", "tenant-a", typeof(WidgetCreated).FullName!, 1,
            timestamp, "corr", "json", "{}"u8.ToArray()) { Domain = "widgets" };

    private static DefaultHttpContext ReplayContext(IServiceProvider services, EventStoreDomainEventsOptions options,
        EventStoreDomainEventEnvelope envelope)
    {
        DefaultHttpContext context = DeliveryContext(services, options, envelope);
        context.Request.Headers[Hexalith.EventStore.ServiceDefaults.Authentication.EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName]
            = "eventstore-operations";
        context.Request.Headers[DevelopmentSubscriberPoisonSink.ReplayHeader] = envelope.MessageId;
        return context;
    }

    private static CommittedRouteCatalogSnapshot SkipCatalog(RouteCatalogSidecarTransport observed, EventStoreSubscriptionSkipPolicy catalogPolicy)
    {
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
        return new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog),
            1, [auxiliary], localAppId: "widgets");
    }

    private sealed class CountingWidgetHandler : IEventStoreDomainEventHandler<WidgetCreated>
    {
        public int Count { get; private set; }

        public Task HandleAsync(WidgetCreated @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class DurableMarkerStore : IEventStoreDomainEventMarkerStore
    {
        private readonly IEventStoreDomainEventMarkerStore _inner = new InMemoryEventStoreDomainEventMarkerStore();

        public bool SupportsContentBoundAdmission => true;

        public bool IsDurable => true;

        public Task<EventStoreDomainEventMarkerAdmission> TryAcquireContentAsync(string messageId, string fingerprint,
            CancellationToken cancellationToken = default) => _inner.TryAcquireContentAsync(messageId, fingerprint, cancellationToken);

        public Task<bool> MarkFencedDispatchedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
            => _inner.MarkFencedDispatchedAsync(lease, cancellationToken);

        public Task MarkFencedCompletedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
            => _inner.MarkFencedCompletedAsync(lease, cancellationToken);

        public Task ReleaseFenceAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
            => _inner.ReleaseFenceAsync(lease, cancellationToken);

        public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(string messageId, CancellationToken cancellationToken = default)
            => _inner.TryAcquireAsync(messageId, cancellationToken);

        public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default)
            => _inner.MarkCompletedAsync(messageId, cancellationToken);

        public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default)
            => _inner.ReleaseAsync(messageId, cancellationToken);
    }

    private sealed class InProgressMarkerStore : IEventStoreDomainEventMarkerStore
    {
        public bool SupportsContentBoundAdmission => true;

        public Task<EventStoreDomainEventMarkerAdmission> TryAcquireContentAsync(string messageId, string fingerprint,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new EventStoreDomainEventMarkerAdmission(EventStoreDomainEventMarkerAcquisitionResult.InProgress));

        public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(string messageId, CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreDomainEventMarkerAcquisitionResult.InProgress);

        public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingSinkHandler : HttpMessageHandler
    {
        public EventStoreSubscriberPoisonOutcome Outcome { get; set; }

        public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;

        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = System.Net.Http.Json.JsonContent.Create(Outcome) };
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<int> _eventIds = new();

        public IReadOnlyCollection<int> EventIds => [.. _eventIds];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_eventIds);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(System.Collections.Concurrent.ConcurrentQueue<int> eventIds) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => eventIds.Enqueue(eventId.Id);
        }
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
