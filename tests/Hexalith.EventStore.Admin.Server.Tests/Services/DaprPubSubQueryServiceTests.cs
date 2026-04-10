#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprPubSubQueryServiceTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly AdminServerOptions _options = new() { EventStoreDaprHttpEndpoint = "http://localhost:3500" };
    private readonly DaprInfrastructureQueryService _sut;

    public DaprPubSubQueryServiceTests()
    {
        _sut = new DaprInfrastructureQueryService(
            _daprClient,
            _httpClientFactory,
            Options.Create(_options),
            NullLogger<DaprInfrastructureQueryService>.Instance);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ReturnsOverview_WithPubSubComponents()
    {
        // Arrange — remote sidecar returns components AND subscriptions in the same payload
        SetupRemoteSidecar("""
        {
            "components": [
                {"name": "pubsub-events", "type": "pubsub.redis", "version": "v1", "capabilities": []},
                {"name": "statestore", "type": "state.redis", "version": "v1", "capabilities": ["ETAG"]}
            ],
            "subscriptions": [
                {"pubsubName": "pubsub-events", "topic": "*.*.events", "type": "DECLARATIVE", "deadLetterTopic": "", "rules": {"rules": [{"match": "", "path": "/events/handle"}]}}
            ]
        }
        """);

        // Act
        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        // Assert
        result.PubSubComponents.Count.ShouldBe(1);
        result.PubSubComponents[0].ComponentName.ShouldBe("pubsub-events");
        result.PubSubComponents[0].ComponentType.ShouldBe("pubsub.redis");
        result.Subscriptions.Count.ShouldBe(1);
        result.Subscriptions[0].Topic.ShouldBe("*.*.events");
        result.Subscriptions[0].Route.ShouldBe("/events/handle");
        result.Subscriptions[0].DeadLetterTopic.ShouldBeNull(); // empty string → null
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ReturnsEmptyComponents_WhenEndpointNotConfigured()
    {
        // Arrange — no remote endpoint configured; components are sourced from remote only.
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = null };
        DaprInfrastructureQueryService sut = new(
            _daprClient, _httpClientFactory, Options.Create(options),
            NullLogger<DaprInfrastructureQueryService>.Instance);

        // Act
        DaprPubSubOverview result = await sut.GetPubSubOverviewAsync();

        // Assert
        result.PubSubComponents.ShouldBeEmpty();
        result.Subscriptions.ShouldBeEmpty();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ReturnsEmptyEverything_WhenRemoteSidecarFails()
    {
        // Arrange — remote sidecar throws; both components and subscriptions are empty.
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, ""));
        _httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        // Act
        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        // Assert
        result.PubSubComponents.ShouldBeEmpty();
        result.Subscriptions.ShouldBeEmpty();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_FiltersOnlyPubSubComponents()
    {
        // Arrange — remote payload has state store + pubsub + binding components.
        SetupRemoteSidecar("""
        {
            "components": [
                {"name": "statestore", "type": "state.redis", "version": "v1", "capabilities": []},
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1", "capabilities": []},
                {"name": "mybinding", "type": "bindings.http", "version": "v1", "capabilities": []}
            ]
        }
        """);

        // Act
        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        // Assert — only pubsub.redis is included
        result.PubSubComponents.Count.ShouldBe(1);
        result.PubSubComponents[0].ComponentName.ShouldBe("pubsub");
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_HandlesSubscriptionsKeyAbsent()
    {
        // Arrange — remote returns metadata without subscriptions key
        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        SetupRemoteSidecar("""{"actors": []}""");

        // Act
        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        // Assert
        result.Subscriptions.ShouldBeEmpty();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ParsesMultipleSubscriptions()
    {
        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        SetupRemoteSidecar("""
        {
            "subscriptions": [
                {"pubsubName": "pubsub", "topic": "*.*.events", "type": "DECLARATIVE", "deadLetterTopic": "dl.topic", "rules": {"rules": [{"path": "/events/handle"}]}},
                {"pubsubName": "pubsub", "topic": "projection.changed", "type": "PROGRAMMATIC", "deadLetterTopic": "", "rules": {"rules": [{"path": "/projections/notify"}]}}
            ]
        }
        """);

        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        result.Subscriptions.Count.ShouldBe(2);
        result.Subscriptions[0].DeadLetterTopic.ShouldBe("dl.topic");
        result.Subscriptions[1].DeadLetterTopic.ShouldBeNull();
        result.Subscriptions[1].Route.ShouldBe("/projections/notify");
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_HandlesGracefully_WhenRemoteReturnsMalformedJson()
    {
        // Arrange
        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        SetupRemoteSidecar("not valid json {{{");

        // Act
        DaprPubSubOverview result = await _sut.GetPubSubOverviewAsync();

        // Assert — graceful degradation, not an exception
        result.Subscriptions.ShouldBeEmpty();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_WhenEndpointNotConfigured_ReturnsNotConfiguredStatus()
    {
        // Arrange
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = null };
        DaprInfrastructureQueryService sut = new(
            _daprClient, _httpClientFactory, Options.Create(options),
            NullLogger<DaprInfrastructureQueryService>.Instance);

        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        // Act
        DaprPubSubOverview result = await sut.GetPubSubOverviewAsync();

        // Assert
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        result.RemoteEndpoint.ShouldBeNull();
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_WhenRemoteCallThrows_ReturnsUnreachableStatus()
    {
        // Arrange — endpoint configured, HTTP call returns error
        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        DaprInfrastructureQueryService sut = new(
            _daprClient, _httpClientFactory, Options.Create(options),
            NullLogger<DaprInfrastructureQueryService>.Instance);

        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, "error"));
        _httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        // Act
        DaprPubSubOverview result = await sut.GetPubSubOverviewAsync();

        // Assert
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        result.RemoteEndpoint.ShouldBe(endpoint);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_WhenRemoteCallSucceeds_ReturnsAvailableStatus()
    {
        // Arrange — endpoint configured, HTTP call returns subscription metadata
        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        DaprInfrastructureQueryService sut = new(
            _daprClient, _httpClientFactory, Options.Create(options),
            NullLogger<DaprInfrastructureQueryService>.Instance);

        DaprMetadata metadata = CreateMetadata([]);
        _daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        string remoteJson = """{"subscriptions":[{"pubsubName":"pubsub","topic":"*.*.events","type":"DECLARATIVE","deadLetterTopic":"","rules":{"rules":[{"path":"/events/handle"}]}}]}""";
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, remoteJson));
        _httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        // Act
        DaprPubSubOverview result = await sut.GetPubSubOverviewAsync();

        // Assert
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.RemoteEndpoint.ShouldBe(endpoint);
        result.Subscriptions.Count.ShouldBe(1);
    }

    // ===== Helpers =====

    private void SetupRemoteSidecar(string jsonResponse)
    {
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, jsonResponse));
        _httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);
    }

    private static DaprMetadata CreateMetadata(List<DaprComponentsMetadata> components) => new(
        id: "test-app",
        actors: [],
        extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.14.0" },
        components: components);

    private sealed class FakeHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
