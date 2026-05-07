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

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprInfrastructureQueryServiceTests {
    private static DaprInfrastructureQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null,
        IHttpClientFactory? httpClientFactory = null) {
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
    public async Task GetComponentsAsync_ReturnsComponents_WhenMetadataAvailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", ["ETAG", "TRANSACTIONAL"]),
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        _ = daprClient.GetStateAsync<string>(
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
    public async Task GetComponentsAsync_SurfacesSynthesizedStateStore_WhenBothMetadataSourcesUnavailable() {
        // Canonical inventory swallows local-sidecar failures (the failure becomes evidence
        // exposed via DaprCanonicalInventory.LocalProbeAvailable=false). With no remote endpoint
        // configured we still surface the configured state store as an Unhealthy synth row so
        // operators see the dependency rather than an empty list (AC1 truth contract: "absent
        // local evidence cannot be misread as healthy").
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));
        _ = daprClient.GetStateAsync<string>(
            Arg.Any<string>(), "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unreachable"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        DaprComponentDetail synth = result.ShouldHaveSingleItem();
        synth.ComponentName.ShouldBe("statestore");
        synth.Category.ShouldBe(DaprComponentCategory.StateStore);
        synth.Status.ShouldBe(HealthStatus.Unhealthy);
        synth.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);
    }

    [Fact]
    public async Task GetComponentsAsync_MarksStateStoreUnhealthy_WhenProbeFails() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unreachable"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task GetComponentsAsync_NonStateStoreComponents_AreAlwaysHealthy() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []),
            new DaprComponentsMetadata("secretstore", "secretstores.local.file", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        // Non-state-store components are surfaced as Healthy. The configured state-store synth
        // row is also produced (probe-default success), but this test only pins the contract
        // for non-state-store rows.
        IReadOnlyList<DaprComponentDetail> nonStateStore =
            result.Where(c => c.Category != DaprComponentCategory.StateStore).ToArray();
        nonStateStore.Count.ShouldBe(2);
        nonStateStore.ShouldAllBe(c => c.Status == HealthStatus.Healthy);
    }

    [Fact]
    public async Task GetComponentsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetComponentsAsync(cts.Token));
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsSidecarInfo_WhenMetadataAvailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []),
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("test-app");
        result.RuntimeVersion.ShouldBe("1.14.0");
        result.ComponentCount.ShouldBe(2);
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        result.RemoteEndpoint.ShouldBeNull();
    }

    [Fact]
    public async Task GetSidecarInfoAsync_PopulatesSubscriptionAndHttpEndpointCounts_FromRemoteSidecar() {
        // Arrange — local sidecar metadata + remote eventstore sidecar exposing 2 subs and 3 http endpoints
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
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
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert
        _ = result.ShouldNotBeNull();
        result.SubscriptionCount.ShouldBe(2);
        result.HttpEndpointCount.ShouldBe(3);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.RemoteEndpoint.ShouldBe(endpoint);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsZeroCounts_WhenRemoteSidecarMissingArrays() {
        // Arrange — remote responds 200 with no subscriptions/httpEndpoints arrays
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """{"actors": []}"""));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert
        _ = result.ShouldNotBeNull();
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsUnreachable_WhenRemoteSidecarFails() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, "boom"));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        // Assert — local metadata still served, but remote unreachable so counts stay 0
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("test-app");
        result.SubscriptionCount.ShouldBe(0);
        result.HttpEndpointCount.ShouldBe(0);
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        result.RemoteEndpoint.ShouldBe(endpoint);
    }

    private sealed class FakeHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode) {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        });
    }

    [Fact]
    public async Task GetSidecarInfoAsync_Throws_WhenSidecarUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetSidecarInfoAsync());
    }

    [Fact]
    public async Task GetSidecarInfoAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetSidecarInfoAsync(cts.Token));
    }

    [Fact]
    public async Task GetSidecarInfoAsync_RuntimeVersionFromExtended() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "my-app",
            actors: [],
            extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.15.2" },
            components: []);

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.RuntimeVersion.ShouldBe("1.15.2");
    }

    [Fact]
    public async Task GetSidecarInfoAsync_RuntimeVersionUnknown_WhenNotInExtended() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "my-app",
            actors: [],
            extended: new Dictionary<string, string>(),
            components: []);

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.RuntimeVersion.ShouldBe("unknown");
    }

    [Fact]
    public async Task GetSidecarInfoAsync_EmptyId_ReturnsUnknownAppId() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "",
            actors: [],
            extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.14.0" },
            components: []);

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("unknown");
    }

    [Fact]
    public async Task GetComponentsAsync_SkipsComponents_WithNullOrEmptyNameOrType() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []),
            new DaprComponentsMetadata("", "pubsub.redis", "v1", []),
            new DaprComponentsMetadata("binding", "", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        result.Count.ShouldBe(1);
        result[0].ComponentName.ShouldBe("statestore");
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_MergesRemoteAndLocal_WithSourceAttribution() {
        // Arrange — local sidecar sees state-store; remote eventstore sidecar adds pub/sub +
        // confirms the state-store. Identity merges by { name, type } so each component gets
        // a single row. Probe-last ordering means the state-store row carries
        // Source=LocalAdminProbe (probe wins on the probed component); pub/sub stays
        // Source=RemoteEventStoreMetadata (no probe runs against it).
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "id": "eventstore",
            "components": [
                {"name": "statestore", "type": "state.redis", "version": "v1", "capabilities": ["ETAG"]},
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1", "capabilities": []}
            ],
            "subscriptions": [
                {"pubsubName": "pubsub", "topic": "events"}
            ]
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        inventory.LocalProbeAvailable.ShouldBeTrue();
        inventory.PubSubSubscriptions.Count.ShouldBe(1);

        // State-store: probe-last ordering means the probed source wins on the shared key.
        DaprComponentDetail stateStore = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.StateStore).ShouldHaveSingleItem();
        stateStore.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);
        stateStore.Status.ShouldBe(HealthStatus.Healthy);

        // Pub/sub: not probed, so remote attribution stays put.
        DaprComponentDetail pubSub = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.PubSub).ShouldHaveSingleItem();
        pubSub.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
        pubSub.ComponentName.ShouldBe("pubsub");
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_RemoteWinsOverLocalFallback_OnSharedNonProbedKey() {
        // Real "remote-wins-on-shared-key" proof for components that are NOT subject to a
        // local probe (e.g. pub/sub). Local fallback sees pub/sub with v1; remote sees pub/sub
        // with v2. Identity is { name, type } so collisions need a SAME type to actually merge.
        // This covers the merge contract's non-probed branch — orthogonal to the state-store
        // probe-wins case above.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1-local", ["LOCAL_CAP"])));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "id": "eventstore",
            "components": [
                {"name": "pubsub", "type": "pubsub.redis", "version": "v2-remote", "capabilities": ["REMOTE_CAP"]}
            ]
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        DaprComponentDetail pubSub = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.PubSub).ShouldHaveSingleItem();
        pubSub.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
        pubSub.Version.ShouldBe("v2-remote");
        pubSub.Capabilities.ShouldContain("REMOTE_CAP");
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_PreservesProbeUnhealthy_WhenRemoteReportsStateStoreLoaded() {
        // Operator Truth Contract conflict rule: "Local probe fails, remote says loaded ->
        // one row, loaded inventory + unhealthy probe evidence." Probe-last ordering keeps
        // the probe Status (Unhealthy) on the shared state-store key even when the remote
        // EventStore sidecar also reports the component (which would otherwise overwrite
        // Status to Healthy under the previous merge order).
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis connection refused"));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "id": "eventstore",
            "components": [
                {"name": "statestore", "type": "state.redis", "version": "v1"},
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1"}
            ]
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        DaprComponentDetail stateStore = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.StateStore).ShouldHaveSingleItem();
        stateStore.Status.ShouldBe(HealthStatus.Unhealthy);
        stateStore.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);

        // Pub/sub still surfaced from remote — probe failure does not erase remote pub/sub.
        DaprComponentDetail pubSub = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.PubSub).ShouldHaveSingleItem();
        pubSub.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ZeroSubscriptions_WithAvailable_DistinguishesValidEmptyFromFailed() {
        // Operator Truth Contract: a remote payload that genuinely contains zero pub/sub
        // subscriptions reports Available + zero (not Unavailable). This pins the boundary
        // between "real zero with successful evidence" vs "unavailable evidence".
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "id": "eventstore",
            "components": [
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1"}
            ],
            "subscriptions": []
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        inventory.PubSubSubscriptions.ShouldBeEmpty();
        inventory.Components.ShouldContain(c => c.Category == DaprComponentCategory.PubSub);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_PreservesLocalEvidence_WhenRemoteUnreachable() {
        // Arrange — remote sidecar unreachable; local Admin sidecar still reports state store.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, "boom"));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert — local fallback evidence kept; remote labelled Unreachable; pub/sub absent.
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        inventory.LocalProbeAvailable.ShouldBeTrue();
        inventory.PubSubSubscriptions.ShouldBeEmpty();
        DaprComponentDetail stateStore = inventory.Components.ShouldHaveSingleItem();
        stateStore.ComponentName.ShouldBe("statestore");
        // Local fallback OR local probe (state-store probe succeeded) — never RemoteEventStoreMetadata.
        stateStore.Source.ShouldBeOneOf(
            DaprComponentSource.LocalAdminProbe,
            DaprComponentSource.LocalAdminMetadataFallback);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsInvalidPayload_OnMalformedRemoteJson() {
        // Arrange
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, "this is not json"));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.InvalidPayload);
        inventory.PubSubSubscriptions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsNotConfigured_WhenEndpointUnset() {
        // Arrange — no endpoint configured
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        inventory.RemoteEndpoint.ShouldBeNull();
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_DoesNotErasePubSub_WhenLocalSidecarFailsButRemoteAvailable() {
        // Story scenario: Redis (state store) is down on Admin.Server, but the remote EventStore
        // sidecar is still reachable. /dapr and /dapr/pubsub must still show pub/sub from remote.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("local sidecar transient error"));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "id": "eventstore",
            "components": [
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1"}
            ]
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        inventory.LocalProbeAvailable.ShouldBeFalse();
        DaprComponentDetail pubSub = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.PubSub).ShouldHaveSingleItem();
        pubSub.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
    }
}
