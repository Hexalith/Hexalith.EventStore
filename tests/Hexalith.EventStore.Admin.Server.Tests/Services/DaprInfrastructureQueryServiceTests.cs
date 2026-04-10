#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprInfrastructureQueryServiceTests
{
    private static DaprInfrastructureQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions();
        httpClientFactory ??= Substitute.For<IHttpClientFactory>();
        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        return new DaprInfrastructureQueryService(
            daprClient,
            httpClientFactory,
            options,
            NullLogger<DaprInfrastructureQueryService>.Instance);
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
        id: "test-app",
        actors: [],
        extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.14.0" },
        components: components);

    [Fact]
    public async Task GetComponentsAsync_ReturnsComponents_WhenMetadataAvailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", ["ETAG", "TRANSACTIONAL"]),
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(2);
        result[0].ComponentName.ShouldBe("statestore");
        result[0].Category.ShouldBe(DaprComponentCategory.StateStore);
        result[0].Status.ShouldBe(HealthStatus.Healthy);
        result[0].Capabilities.Count.ShouldBe(2);
        result[1].ComponentName.ShouldBe("pubsub");
        result[1].Category.ShouldBe(DaprComponentCategory.PubSub);
    }

    [Fact]
    public async Task GetComponentsAsync_Throws_WhenSidecarUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetComponentsAsync());
    }

    [Fact]
    public async Task GetComponentsAsync_MarksStateStoreUnhealthy_WhenProbeFails()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unreachable"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task GetComponentsAsync_NonStateStoreComponents_AreAlwaysHealthy()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []),
            new DaprComponentsMetadata("secretstore", "secretstores.local.file", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(c => c.Status == HealthStatus.Healthy);
    }

    [Fact]
    public async Task GetComponentsAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetComponentsAsync(cts.Token));
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsSidecarInfo_WhenMetadataAvailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []),
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        result.ShouldNotBeNull();
        result.AppId.ShouldBe("test-app");
        result.RuntimeVersion.ShouldBe("1.14.0");
        result.ComponentCount.ShouldBe(2);
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        result.RemoteEndpoint.ShouldBeNull();
    }

    [Fact]
    public async Task GetSidecarInfoAsync_PopulatesSubscriptionAndHttpEndpointCounts_FromRemoteSidecar()
    {
        // Arrange — local sidecar metadata + remote eventstore sidecar exposing 2 subs and 3 http endpoints
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "subscriptions": [
                {"pubsubName": "pubsub", "topic": "events"},
                {"pubsubName": "pubsub", "topic": "projection.changed"}
            ],
            "httpEndpoints": [
                {"name": "ep1"},
                {"name": "ep2"},
                {"name": "ep3"}
            ]
        }
        """));
        httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert
        result.ShouldNotBeNull();
        result.SubscriptionCount.ShouldBe(2);
        result.HttpEndpointCount.ShouldBe(3);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.RemoteEndpoint.ShouldBe(endpoint);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsZeroCounts_WhenRemoteSidecarMissingArrays()
    {
        // Arrange — remote responds 200 with no subscriptions/httpEndpoints arrays
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """{"actors": []}"""));
        httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert
        result.ShouldNotBeNull();
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsUnreachable_WhenRemoteSidecarFails()
    {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, "boom"));
        httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert — local metadata still served, but remote unreachable so counts stay 0
        result.ShouldNotBeNull();
        result.AppId.ShouldBe("test-app");
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        result.RemoteEndpoint.ShouldBe(endpoint);
    }

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

    [Fact]
    public async Task GetSidecarInfoAsync_Throws_WhenSidecarUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetSidecarInfoAsync());
    }

    [Fact]
    public async Task GetSidecarInfoAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetSidecarInfoAsync(cts.Token));
    }

    [Fact]
    public async Task GetSidecarInfoAsync_RuntimeVersionFromExtended()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "my-app",
            actors: [],
            extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.15.2" },
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        result.ShouldNotBeNull();
        result.RuntimeVersion.ShouldBe("1.15.2");
    }

    [Fact]
    public async Task GetSidecarInfoAsync_RuntimeVersionUnknown_WhenNotInExtended()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "my-app",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        result.ShouldNotBeNull();
        result.RuntimeVersion.ShouldBe("unknown");
    }

    [Fact]
    public async Task GetSidecarInfoAsync_EmptyId_ReturnsUnknownAppId()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "",
            actors: [],
            extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.14.0" },
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        result.ShouldNotBeNull();
        result.AppId.ShouldBe("unknown");
    }

    [Fact]
    public async Task GetComponentsAsync_SkipsComponents_WithNullOrEmptyNameOrType()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []),
            new DaprComponentsMetadata("", "pubsub.redis", "v1", []),
            new DaprComponentsMetadata("binding", "", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(1);
        result[0].ComponentName.ShouldBe("statestore");
    }
}
