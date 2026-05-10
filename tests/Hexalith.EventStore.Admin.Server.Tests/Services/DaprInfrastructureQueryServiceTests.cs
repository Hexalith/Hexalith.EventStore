#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Diagnostics;
using System.Net;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprInfrastructureQueryServiceTests {
    private static DaprInfrastructureQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null,
        IHttpClientFactory? httpClientFactory = null,
        ILogger<DaprInfrastructureQueryService>? logger = null) {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions();
        httpClientFactory ??= Substitute.For<IHttpClientFactory>();
        logger ??= NullLogger<DaprInfrastructureQueryService>.Instance;
        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        return new DaprInfrastructureQueryService(
            daprClient,
            httpClientFactory,
            options,
            logger);
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
        // exposed via DaprCanonicalInventory.LocalSidecarMetadataAvailable=false). With no remote endpoint
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
    public async Task GetComponentsAsync_LocalFallbackComponents_AreUnverifiedUntilProbeOrRemoteEvidence() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []),
            new DaprComponentsMetadata("secretstore", "secretstores.local.file", "v1", []));

        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> result = await service.GetComponentsAsync();

        // Local sidecar metadata proves a component is configured, not that its backing
        // dependency is usable. Non-state-store local fallback rows stay unverified until remote
        // EventStore metadata or a dedicated probe supplies health evidence.
        IReadOnlyList<DaprComponentDetail> nonStateStore =
            result.Where(c => c.Category != DaprComponentCategory.StateStore).ToArray();
        nonStateStore.Count.ShouldBe(2);
        nonStateStore.ShouldAllBe(c => c.Source == DaprComponentSource.LocalAdminMetadataFallback);
        nonStateStore.ShouldAllBe(c => c.Status == HealthStatus.Unhealthy);
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
            "components": [],
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
    public async Task GetSidecarInfoAsync_ReturnsInvalidPayload_WhenRemoteSidecarMissingRequiredArrays() {
        // Arrange — remote responds 200 with parseable JSON but lacks required metadata arrays.
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
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.InvalidPayload);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_UsesRawEndpointForRequest_ButReportsSanitizedEndpoint() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", [])));

        const string rawEndpoint = "http://user:p%40ss@localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = rawEndpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        FakeHandler handler = new(HttpStatusCode.OK, """{"components": [], "subscriptions": []}""");
        using HttpClient httpClient = new(handler);
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprSidecarInfo? result = await service.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.RemoteEndpoint.ShouldBe("http://localhost:3501/");
        handler.LastRequestUri.ShouldBe("http://user:p%40ss@localhost:3501/v1.0/metadata");
        httpClientFactory.Received(1).CreateClient("DaprSidecar");
    }

    [Fact]
    public void Constructor_DoesNotLogRemoteEndpointCredentials() {
        RecordingLogger<DaprInfrastructureQueryService> logger = new();
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = "http://user:p%40ss@localhost:3501" };

        _ = CreateService(serverOptions: options, logger: logger);

        logger.Records.Any(r => r.Message.Contains("p%40ss", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        logger.Records.Any(r => r.Message.Contains("user:", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        logger.Records.Any(r => r.Message.Contains("http://localhost:3501", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
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
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(statusCode) {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("""{"components":[],"subscriptions":[]}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsNull_WhenSidecarUnavailable() {
        // Round 3: local metadata read is now memoized via TryReadLocalMetadataAsync, which
        // catches non-cancellation exceptions (the sidecar-down case) and surfaces them as
        // "metadata not available". GetSidecarInfoAsync therefore returns null rather than
        // letting the InvalidOperationException escape — graceful-degradation contract.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprSidecarInfo? info = await service.GetSidecarInfoAsync();

        info.ShouldBeNull();
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsUnreachable_WhenRemoteMetadataTimesOut() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        using HttpClient httpClient = new(new TimeoutHandler()) {
            Timeout = TimeSpan.FromMilliseconds(10),
        };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(
            daprClient,
            new AdminServerOptions { EventStoreDaprHttpEndpoint = endpoint },
            httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        inventory.RemoteEndpoint.ShouldBe(endpoint);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsLocalUnavailable_WhenLocalMetadataTimesOut() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(call => NeverCompletesMetadataAsync(call.Arg<CancellationToken>()));
        Stopwatch stopwatch = Stopwatch.StartNew();

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        stopwatch.Stop();
        inventory.LocalSidecarMetadataAvailable.ShouldBeFalse();
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));

        static async Task<DaprMetadata> NeverCompletesMetadataAsync(CancellationToken ct) {
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            return CreateMetadata();
        }
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
        // a single row. Probe-last ordering means the state-store row carries probe-derived
        // Status while preserving remote inventory Source; pub/sub also stays remote-sourced.
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
        inventory.LocalSidecarMetadataAvailable.ShouldBeTrue();
        inventory.PubSubSubscriptions.Count.ShouldBe(1);

        // State-store: probe-last ordering means Status comes from the local probe, while
        // Source still identifies the remote inventory fact.
        DaprComponentDetail stateStore = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.StateStore).ShouldHaveSingleItem();
        stateStore.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
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
            ],
            "subscriptions": []
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
            ],
            "subscriptions": []
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        DaprComponentDetail stateStore = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.StateStore).ShouldHaveSingleItem();
        stateStore.Status.ShouldBe(HealthStatus.Unhealthy);
        stateStore.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);

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
        inventory.LocalSidecarMetadataAvailable.ShouldBeTrue();
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
    public async Task GetCanonicalDaprInventoryAsync_PreservesRemoteVersion_WhenStateStoreProbeSucceeds() {
        // F25 — round 3 added a "remote wins on shared key" test using pubsub (which is not
        // probed). The state-store probe path was unproven: when probe succeeds, the resulting
        // row's Version field must come from the documented merge winner (remote takes the
        // pre-probe seat in the merged dictionary, then probe rewrites Status/LastCheck/Source
        // but preserves the remote-supplied Version).
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata(
                new DaprComponentsMetadata("statestore", "state.redis", "v1-local", [])));
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
                {"name": "statestore", "type": "state.redis", "version": "v2-remote"}
            ],
            "subscriptions": []
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        DaprComponentDetail stateStore = inventory.Components
            .ShouldHaveSingleItem();
        stateStore.ComponentName.ShouldBe("statestore");
        stateStore.Status.ShouldBe(HealthStatus.Healthy); // probe succeeded
        stateStore.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata); // probe preserved remote inventory source
        stateStore.Version.ShouldBe("v2-remote"); // remote-supplied version survives probe rewrite
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_SynthesizesProbeRow_WhenSameNameNonStateComponentExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata(
            new DaprComponentsMetadata("statestore", "bindings.http", "v1", [])));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.Components.ShouldContain(c => c.ComponentName == "statestore" && c.Category == DaprComponentCategory.Binding);
        DaprComponentDetail stateStore = inventory.Components
            .Where(c => c.ComponentName == "statestore" && c.Category == DaprComponentCategory.StateStore)
            .ShouldHaveSingleItem();
        stateStore.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);
        stateStore.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(RemoteMetadataStatus.NotConfigured)]
    [InlineData(RemoteMetadataStatus.Unreachable)]
    [InlineData(RemoteMetadataStatus.InvalidPayload)]
    public async Task GetCanonicalDaprInventoryAsync_PubSubSubscriptionsCountIsZero_ForEachNonAvailableStatus(
        RemoteMetadataStatus expectedStatus) {
        // F37 — the conflict-rule "subscription count != 0 only when status is Available" had
        // assertions only on the status field, never on the count. Pin the negative invariant:
        // when remote is Unreachable / InvalidPayload / NotConfigured, PubSubSubscriptions.Count
        // is always 0 — and consumers must treat that as "unavailable", not real zero. The UI
        // already does (driven by RemoteMetadataStatus), but the server contract was unpinned.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        AdminServerOptions options = new();
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        if (expectedStatus is RemoteMetadataStatus.Unreachable) {
            options = new AdminServerOptions { EventStoreDaprHttpEndpoint = "http://localhost:3501" };
            using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.InternalServerError, "boom"));
            _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

            DaprInfrastructureQueryService unreachableService = CreateService(daprClient, options, httpClientFactory);
            DaprCanonicalInventory inv = await unreachableService.GetCanonicalDaprInventoryAsync();

            inv.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
            inv.PubSubSubscriptions.Count.ShouldBe(0);
            return;
        }

        if (expectedStatus is RemoteMetadataStatus.InvalidPayload) {
            options = new AdminServerOptions { EventStoreDaprHttpEndpoint = "http://localhost:3501" };
            using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, "{ malformed"));
            _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

            DaprInfrastructureQueryService invalidService = CreateService(daprClient, options, httpClientFactory);
            DaprCanonicalInventory inv = await invalidService.GetCanonicalDaprInventoryAsync();

            inv.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.InvalidPayload);
            inv.PubSubSubscriptions.Count.ShouldBe(0);
            return;
        }

        // NotConfigured — no endpoint set
        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        inventory.PubSubSubscriptions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_PreservesProbeUnhealthy_WhenStateStoreProbeFails() {
        // F2 (CRITICAL probe-timeout fix) regression coverage. We assert the *ProbeFails*
        // branch separately: a thrown exception from GetStateAsync must convert the row to
        // Status = Unhealthy + Source = LocalAdminProbe and survive the merge.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis connection refused"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        DaprComponentDetail stateStore = inventory.Components.ShouldHaveSingleItem();
        stateStore.ComponentName.ShouldBe("statestore");
        stateStore.Status.ShouldBe(HealthStatus.Unhealthy);
        stateStore.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);
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
            ],
            "subscriptions": []
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        // Act
        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        // Assert
        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        inventory.LocalSidecarMetadataAvailable.ShouldBeFalse();
        DaprComponentDetail pubSub = inventory.Components
            .Where(c => c.Category == DaprComponentCategory.PubSub).ShouldHaveSingleItem();
        pubSub.Source.ShouldBe(DaprComponentSource.RemoteEventStoreMetadata);
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsRemoteStatus_WhenLocalSidecarUnavailableButRemoteAvailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("local sidecar transient error"));

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "components": [
                {"name": "pubsub", "type": "pubsub.redis", "version": "v1"}
            ],
            "subscriptions": [
                {"pubsubName": "pubsub", "topic": "events"}
            ]
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprSidecarInfo? info = await service.GetSidecarInfoAsync();

        _ = info.ShouldNotBeNull();
        info.AppId.ShouldBe("unknown");
        info.RuntimeVersion.ShouldBe("unknown");
        info.ComponentCount.ShouldBe(0);
        info.SubscriptionCount.ShouldBe(1);
        info.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    // Round 5 P12: pin transport-level failure classification so a future change to
    // ReadRemoteMetadataAsync's catch graph does not silently retag transport failures
    // (HTTP 429, 503, 5xx) as Available with empty inventory. Each fixture fires the
    // canonical inventory path and asserts both RemoteMetadataStatus == Unreachable and
    // PubSubSubscriptions.Count == 0 so the consumer-facing "subscription data unavailable"
    // contract holds for every transport-error variant.
    [Theory]
    [InlineData((int)HttpStatusCode.TooManyRequests)]
    [InlineData((int)HttpStatusCode.ServiceUnavailable)]
    [InlineData((int)HttpStatusCode.BadGateway)]
    [InlineData((int)HttpStatusCode.GatewayTimeout)]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsUnreachable_OnTransportFailureStatusCodes(int statusCode) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler((HttpStatusCode)statusCode, """{"detail":"upstream throttled"}"""));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        inventory.PubSubSubscriptions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsAvailable_WithEmptyOptionalSections_WhenJsonHasZeroLengthBody() {
        // 200 OK + empty body must still be classified as InvalidPayload — a successful HTTP
        // status with no parseable JSON cannot become "Available with empty inventory" because
        // that would silently lose components/subscriptions evidence. The handler returns an
        // empty body which JsonDocument.ParseAsync rejects with JsonException → InvalidPayload.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, string.Empty));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.InvalidPayload);
        inventory.PubSubSubscriptions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetCanonicalDaprInventoryAsync_ReturnsInvalidPayload_OnMixedCaseRequiredProperties() {
        // System.Text.Json TryGetProperty is case-sensitive by default; a sidecar returning
        // `Components` (capitalised) instead of `components` must be classified as InvalidPayload
        // rather than Available with an empty list. Pin the contract: "successful HTTP, parseable
        // JSON, but missing required property" -> InvalidPayload, never Available with silent
        // empty data.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());

        const string endpoint = "http://localhost:3501";
        AdminServerOptions options = new() { EventStoreDaprHttpEndpoint = endpoint };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        using HttpClient httpClient = new(new FakeHandler(HttpStatusCode.OK, """
        {
            "Components": [],
            "Subscriptions": []
        }
        """));
        _ = httpClientFactory.CreateClient("DaprSidecar").Returns(httpClient);

        DaprInfrastructureQueryService service = CreateService(daprClient, options, httpClientFactory);

        DaprCanonicalInventory inventory = await service.GetCanonicalDaprInventoryAsync();

        inventory.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.InvalidPayload);
    }
}
