using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    [InlineData(EventStoreDomainEventProcessingResult.Processed, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.Duplicate, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedUnknownEventType, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedNoHandlers, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedAggregateMismatch, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.FailedInvalidPayload, StatusCodes.Status200OK)]
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
