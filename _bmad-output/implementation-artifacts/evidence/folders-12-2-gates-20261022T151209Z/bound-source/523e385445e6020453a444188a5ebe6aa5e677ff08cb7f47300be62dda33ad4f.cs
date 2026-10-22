
using System.Net;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
//
// Locks the runtime contract that DW2 evidence captures live:
//   AC#2  Admin DAPR component evidence covers components, sidecar, actors, pub/sub, resiliency, health history.
//   AC#3  Remote sidecar metadata status is per-surface (sidecar / actors / pub/sub each emit their own
//         RemoteMetadataStatus + RemoteEndpoint pair — never collapsed into a single global flag).
//   AC#4  Degraded states stay visible: NotConfigured / Unreachable do NOT downgrade to "Available".
//         Resiliency parser surfaces NotFound / ReadError / ParseError distinctly with
//         IsConfigurationAvailable=false; pub/sub `rules[]` direct-array AND legacy `rules.rules[]`
//         wrapped-object both yield a route deterministically.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until the DW2 live smoke evidence has run
// and the dev confirms the captured artefacts (RemoteMetadataStatus matrix, pub/sub raw shape,
// resiliency parser observed states) match these contracts. Removing Skip per AC means the dev
// has paired the test with the captured evidence row in the DW2 evidence index.
public class Dw2RemoteMetadataPerSurfaceAtddTests {
    private const string SkipReasonAc2 = "ATDD red phase — DW2 AC#2 (Admin DAPR component coverage). Remove Skip after live smoke captures observed component/sidecar/actor/pubsub/resiliency rows in the DW2 evidence index.";
    private const string SkipReasonAc3 = "ATDD red phase — DW2 AC#3 (per-surface RemoteMetadataStatus). Remove Skip after the live RemoteMetadataStatus matrix is recorded with independent rows for sidecar/actors/pubsub.";
    private const string SkipReasonAc4 = "ATDD red phase — DW2 AC#4 (degraded states stay visible). Remove Skip after the smoke captures a degraded sample — empty/Unreachable/parse-error/timeout/4xx/5xx — without flattening it into pass/fail prose.";

    private const string ConfiguredEndpoint = "http://eventstore-sidecar:3501";

    private static (DaprInfrastructureQueryService Service, TestableHttpClientFactory HttpFactory) CreateService(
        DaprClient? daprClient = null,
        string? remoteEndpoint = null,
        string? resiliencyConfigPath = null,
        Action<TestableHttpClientFactory>? configure = null) {
        daprClient ??= Substitute.For<DaprClient>();
        AdminServerOptions options = new() {
            EventStoreDaprHttpEndpoint = remoteEndpoint,
            ResiliencyConfigPath = resiliencyConfigPath,
        };

        TestableHttpClientFactory httpFactory = new();
        configure?.Invoke(httpFactory);

        DaprInfrastructureQueryService service = new(
            daprClient,
            httpFactory,
            Options.Create(options),
            NullLogger<DaprInfrastructureQueryService>.Instance);

        return (service, httpFactory);
    }

    private static DaprMetadata EmptyMetadata() => new(
        id: "eventstore-admin",
        actors: [],
        extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.17.0" },
        components: []);

    [Fact(Skip = SkipReasonAc3)]
    public async Task Sidecar_EmitsNotConfigured_WhenRemoteEndpointBlank() {
        // AC#3 — When AdminServerOptions.EventStoreDaprHttpEndpoint is not configured, the sidecar
        // surface MUST report RemoteMetadataStatus.NotConfigured and RemoteEndpoint=null without
        // attempting any HTTP fetch.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(daprClient, remoteEndpoint: null);

        DaprSidecarInfo? info = await service.GetSidecarInfoAsync();

        _ = info.ShouldNotBeNull();
        info!.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.NotConfigured);
        info.RemoteEndpoint.ShouldBeNull();
    }

    [Fact(Skip = SkipReasonAc3)]
    public async Task Sidecar_EmitsUnreachable_WhenRemoteEndpointConfiguredButFetchFails() {
        // AC#3 + AC#4 — When the endpoint is configured but the remote /v1.0/metadata call throws
        // (port conflict, sidecar down, DNS), the sidecar surface MUST report Unreachable AND keep
        // RemoteEndpoint visible. It MUST NOT degrade to NotConfigured or Available.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(
            daprClient,
            remoteEndpoint: ConfiguredEndpoint,
            configure: f => f.Handler.SetupException(new HttpRequestException("connection refused")));

        DaprSidecarInfo? info = await service.GetSidecarInfoAsync();

        _ = info.ShouldNotBeNull();
        info!.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        info.RemoteEndpoint.ShouldBe(ConfiguredEndpoint);
    }

    [Fact(Skip = SkipReasonAc3)]
    public async Task Actors_RemoteMetadataStatus_IsIndependentFromSidecarStatus() {
        // AC#3 — Each surface (sidecar, actors, pub/sub) computes its own RemoteMetadataStatus.
        // Even if sidecar metadata succeeds, an actor remote-metadata fallback failure MUST be
        // reported on the actors surface independently. One global degraded flag is forbidden.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(
            daprClient,
            remoteEndpoint: ConfiguredEndpoint,
            configure: f => f.Handler.SetupException(new HttpRequestException("port-conflict-3501")));

        DaprActorRuntimeInfo info = await service.GetActorRuntimeInfoAsync();

        info.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        info.RemoteEndpoint.ShouldBe(ConfiguredEndpoint);
        info.ActorTypes.ShouldBeEmpty();
    }

    [Fact(Skip = SkipReasonAc3)]
    public async Task PubSub_RemoteMetadataStatus_IsIndependentFromOtherSurfaces() {
        // AC#3 — Pub/sub MUST report its own RemoteMetadataStatus + RemoteEndpoint pair, even when
        // sidecar/actors fall back identically. Reviewers need a per-surface row in the matrix.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(
            daprClient,
            remoteEndpoint: ConfiguredEndpoint,
            configure: f => f.Handler.SetupException(new HttpRequestException("connection refused")));

        DaprPubSubOverview overview = await service.GetPubSubOverviewAsync();

        overview.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Unreachable);
        overview.RemoteEndpoint.ShouldBe(ConfiguredEndpoint);
    }

    [Fact(Skip = SkipReasonAc4)]
    public async Task PubSub_ParsesRulesArrayDirectShape_FromDaprMetadataV117() {
        // AC#4 — DAPR metadata v1.17 returns subscriptions[].rules as a direct array of {match,path}.
        // The parser MUST yield a non-default route for that shape so the smoke evidence captures
        // a route different from "/" when the runtime exposes it.
        const string payload = """
            {
                "id": "eventstore",
                "components": [
                    { "name": "pubsub", "type": "pubsub.redis", "version": "v1", "capabilities": [] }
                ],
                "subscriptions": [
                    {
                        "pubsubName": "pubsub",
                        "topic": "tenant-counter-events",
                        "type": "DECLARATIVE",
                        "rules": [ { "path": "/events" } ]
                    }
                ]
            }
            """;
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(
            daprClient,
            remoteEndpoint: ConfiguredEndpoint,
            configure: f => f.Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            }));

        DaprPubSubOverview overview = await service.GetPubSubOverviewAsync();

        overview.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        _ = overview.Subscriptions.ShouldHaveSingleItem();
        overview.Subscriptions[0].Route.ShouldBe("/events");
    }

    [Fact(Skip = SkipReasonAc4)]
    public async Task PubSub_ParsesLegacyWrappedRulesShape_ForBackwardCompatibility() {
        // AC#4 — The parser MUST also tolerate the legacy wrapped form '{"rules": {"rules": [...]}}'
        // so older test fixtures and prior DAPR responses still produce a deterministic route.
        const string payload = """
            {
                "id": "eventstore",
                "components": [
                    { "name": "pubsub", "type": "pubsub.redis", "version": "v1", "capabilities": [] }
                ],
                "subscriptions": [
                    {
                        "pubsubName": "pubsub",
                        "topic": "tenant-counter-events",
                        "type": "DECLARATIVE",
                        "rules": { "rules": [ { "path": "/legacy" } ] }
                    }
                ]
            }
            """;
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(EmptyMetadata());

        (DaprInfrastructureQueryService service, _) = CreateService(
            daprClient,
            remoteEndpoint: ConfiguredEndpoint,
            configure: f => f.Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            }));

        DaprPubSubOverview overview = await service.GetPubSubOverviewAsync();

        _ = overview.Subscriptions.ShouldHaveSingleItem();
        overview.Subscriptions[0].Route.ShouldBe("/legacy");
    }

    [Fact(Skip = SkipReasonAc4)]
    public async Task Resiliency_FileNotFound_ReportsUnavailableWithStableMarker() {
        // AC#4 — Resiliency parser MUST classify file-not-found, read-error, and parse-error as
        // distinct unavailable states. IsConfigurationAvailable MUST stay false; ErrorMessage MUST
        // mention the missing path so reviewers can pair the smoke result with the recorded path.
        DaprClient daprClient = Substitute.For<DaprClient>();
        string missingPath = Path.Combine(Path.GetTempPath(), $"dw2-not-found-{Guid.NewGuid():N}.yaml");
        (DaprInfrastructureQueryService service, _) = CreateService(daprClient, resiliencyConfigPath: missingPath);

        DaprResiliencySpec spec = await service.GetResiliencySpecAsync();

        spec.IsConfigurationAvailable.ShouldBeFalse();
        _ = spec.ErrorMessage.ShouldNotBeNull();
        spec.ErrorMessage.ShouldContain("not found");
        spec.ErrorMessage.ShouldContain(missingPath);
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task Components_ProbeTimeout_ProducesDegradedHealth_NotSilentSuccess() {
        // AC#2 + AC#4 — A state-store probe timeout MUST NOT cause GetComponentsAsync to drop the
        // component or report a healthy state. Reviewers MUST see Degraded (or equivalent visible
        // marker) in the components row of the DW2 evidence Admin DAPR table.
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "eventstore-admin",
            actors: [],
            extended: new Dictionary<string, string> { ["daprRuntimeVersion"] = "1.17.0" },
            components: [new DaprComponentsMetadata("statestore", "state.redis", "v1", ["ETAG"])]);
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        _ = daprClient
            .GetStateAsync<string>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("probe timeout"));

        (DaprInfrastructureQueryService service, _) = CreateService(daprClient);

        IReadOnlyList<DaprComponentDetail> components = await service.GetComponentsAsync();

        _ = components.ShouldHaveSingleItem();
        components[0].ComponentName.ShouldBe("statestore");
        // The DW2 contract requires a visible degraded state on probe failure — never a silent
        // collapse to "healthy". This assertion exists to lock that contract; the dev confirms
        // the smoke captures the matching live row before removing Skip.
        components[0].Status.ShouldNotBe(Hexalith.EventStore.Admin.Abstractions.Models.Health.HealthStatus.Healthy);
    }

    private sealed class TestableHttpClientFactory : IHttpClientFactory {
        public Hexalith.EventStore.Admin.Server.Tests.Helpers.TestHttpMessageHandler Handler { get; } = new();

        public HttpClient CreateClient(string name) => new(Handler) {
            BaseAddress = new Uri("http://localhost"),
        };
    }
}
