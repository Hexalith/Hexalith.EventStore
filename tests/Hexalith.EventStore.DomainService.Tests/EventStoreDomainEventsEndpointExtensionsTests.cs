using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

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
    [Fact]
    public void MapEventStoreDomainEvents_MapsConfiguredRouteAndTopicMetadata() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
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

        ITopicMetadata topicMetadata = endpoint.Metadata.GetMetadata<ITopicMetadata>().ShouldNotBeNull();
        topicMetadata.PubsubName.ShouldBe("event-pubsub");
        topicMetadata.Name.ShouldBe("widgets.events");
    }

    [Theory]
    [InlineData(EventStoreDomainEventProcessingResult.Processed, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.Duplicate, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedUnknownEventType, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedNoHandlers, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.SkippedAggregateMismatch, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.FailedInvalidPayload, StatusCodes.Status200OK)]
    [InlineData(EventStoreDomainEventProcessingResult.RetryableInProgress, StatusCodes.Status500InternalServerError)]
    public void MapProcessingResult_MapsProcessorOutcomesIntentionally(
        EventStoreDomainEventProcessingResult processingResult,
        int expectedStatusCode) {
        IResult result = EventStoreDomainEventsEndpointExtensions.MapProcessingResult(processingResult);

        IStatusCodeHttpResult statusResult = result.ShouldBeAssignableTo<IStatusCodeHttpResult>();
        statusResult.StatusCode.ShouldBe(expectedStatusCode);
    }
}
