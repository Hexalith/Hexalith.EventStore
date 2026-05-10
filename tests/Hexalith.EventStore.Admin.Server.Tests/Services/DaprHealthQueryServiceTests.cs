#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprHealthQueryServiceTests {
    private static (DaprHealthQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null,
        IStreamQueryService? streamQuery = null,
        IDaprInfrastructureQueryService? infrastructure = null) {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions {
            TraceUrl = "https://traces",
            MetricsUrl = "https://metrics",
            LogsUrl = "https://logs",
        };

        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        if (streamQuery is null) {
            streamQuery = Substitute.For<IStreamQueryService>();
            _ = streamQuery.GetRecentlyActiveStreamsAsync(
                    Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));
        }

        if (infrastructure is null) {
            infrastructure = Substitute.For<IDaprInfrastructureQueryService>();
            // Default-healthy canonical inventory so tests not focused on state-store probe
            // failure get a green baseline. Tests that simulate Redis down inject their own.
            _ = infrastructure.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new DaprCanonicalInventory(
                    [
                        new DaprComponentDetail(
                            "statestore", "state.redis", DaprComponentCategory.StateStore,
                            "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [],
                            DaprComponentSource.LocalAdminProbe),
                    ],
                    [],
                    RemoteMetadataStatus.Available,
                    "http://eventstore-sidecar",
                    LocalSidecarMetadataAvailable: true,
                    CapturedAtUtc: DateTimeOffset.UtcNow)));
        }

        var service = new DaprHealthQueryService(
            daprClient,
            httpClientFactory,
            options,
            new NullAdminAuthContext(),
            streamQuery,
            infrastructure,
            NullLogger<DaprHealthQueryService>.Instance);

        return (service, handler);
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
        id: "test-app",
        actors: [],
        extended: new Dictionary<string, string>(),
        components: components);

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealthy_WhenAllProbesSucceed() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(),
            Arg.Any<string>(),
            Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DaprCanonicalInventory(
                [
                    new DaprComponentDetail(
                        "statestore", "state.redis", DaprComponentCategory.StateStore,
                        "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [],
                        DaprComponentSource.LocalAdminProbe),
                ],
                [],
                RemoteMetadataStatus.Available,
                "http://eventstore-sidecar",
                LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow)));

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, infrastructure: infra);
        // EventStore health endpoint returns OK
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Healthy);
        result.DaprComponents.Count.ShouldBe(1);
        result.DaprComponents[0].ComponentName.ShouldBe("statestore");
        result.ObservabilityLinks.TraceUrl.ShouldBe("https://traces");
        result.InventorySourceStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsDegraded_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupException(new InvalidOperationException("EventStore down"));

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsUnhealthy_WhenSidecarUnavailable() {
        // The local Admin sidecar is unavailable: the canonical inventory's LocalSidecarMetadataAvailable
        // flag flips to false, which the health service must treat as Unhealthy regardless of
        // other dependency probes (Admin operations cannot be served).
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(DaprCanonicalInventory.Empty)); // LocalSidecarMetadataAvailable=false

        (DaprHealthQueryService service, _) = CreateService(daprClient, infrastructure: infra);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsUnhealthy_WhenStateStoreProbeFails() {
        // AC1: when Redis/state-store is unreachable, /health must return HTTP 200 partial,
        // mark the matching state-store component Unhealthy, and overall=Unhealthy. The probe
        // runs inside DaprInfrastructureQueryService.GetCanonicalDaprInventoryAsync; here we
        // simulate that by injecting an inventory whose state-store row is already Unhealthy.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DaprCanonicalInventory(
                [
                    new DaprComponentDetail(
                        "statestore", "state.redis", DaprComponentCategory.StateStore,
                        "v1", HealthStatus.Unhealthy, DateTimeOffset.UtcNow, [],
                        DaprComponentSource.LocalAdminProbe),
                ],
                [],
                RemoteMetadataStatus.Available,
                "http://eventstore-sidecar",
                LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow)));

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, infrastructure: infra);
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Unhealthy);
        DaprComponentHealth stateStore = result.DaprComponents
            .ShouldHaveSingleItem();
        stateStore.ComponentName.ShouldBe("statestore");
        stateStore.Status.ShouldBe(HealthStatus.Unhealthy);
        stateStore.Source.ShouldBe(DaprComponentSource.LocalAdminProbe);
    }

    [Fact]
    public async Task GetSystemHealthAsync_DoesNotLeakConnectionDetails_OnStateStoreFailure() {
        // AC1: response bodies must not leak raw exception messages, connection strings, or
        // host details from the state-store probe failure. After consolidation, the probe lives
        // in DaprInfrastructureQueryService — so this test exercises the full real path: a real
        // service whose DaprClient throws with the secret AND whose remote-HTTP path (the other
        // realistic leak surface) is wired to a handler returning a payload that also contains
        // the secret. We assert (1) no string field on the response carries the secret AND
        // (2) no log record's Message or Exception text carries the secret — round 2 patched
        // the response surface but `NullLogger` was discarding the log channel entirely.
        const string SecretConnectionDetails = "redis://my-secret-host:6379,password=p@ssw0rd";

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprMetadata(
                id: "test-app",
                actors: [],
                extended: new Dictionary<string, string>(),
                components: [new DaprComponentsMetadata("statestore", "state.redis", "v1", [])]));
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:dapr-probe",
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(SecretConnectionDetails));
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        // Wire a remote-HTTP handler that ALSO contains the secret, so the JSON-parse path is
        // exercised end-to-end (round 2 had `EventStoreDaprHttpEndpoint` null, which short-
        // circuited the remote path to NotConfigured and left it dark). The endpoint must be
        // configured for ReadRemoteMetadataAsync to actually attempt a remote read.
        //
        // Round 4 fix: also set StateStoreName so the probe loop in
        // GetCanonicalDaprInventoryAsync actually runs against the configured store —
        // otherwise the probe is skipped (`if (!string.IsNullOrWhiteSpace(configuredStateStore))`),
        // GetStateAsync is never called, the secret-bearing exception never reaches the
        // `LogWarning(ex, "State store probe failed for {ComponentName}.")` path, and the
        // assertion "no log record contains the secret" passes vacuously without exercising
        // the actual leak surface AC1 cares about.
        var remoteHandler = new TestHttpMessageHandler();
        // Return a malformed payload that bears the secret — exercises the InvalidPayload arm
        // and forces the JSON exception text to flow through the warning logger.
        remoteHandler.SetupResponse(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
            Content = new StringContent($"{{ not json — secret leak: {SecretConnectionDetails}", System.Text.Encoding.UTF8, "application/json"),
        });
        using HttpClient remoteHttpClient = new(remoteHandler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory infraHttpFactory = Substitute.For<IHttpClientFactory>();
        _ = infraHttpFactory.CreateClient(Arg.Any<string>()).Returns(remoteHttpClient);
        var infraOptions = Options.Create(new AdminServerOptions {
            EventStoreDaprHttpEndpoint = "http://eventstore-sidecar:3500",
            StateStoreName = "statestore",
        });
        var infraLogger = new RecordingLogger<DaprInfrastructureQueryService>();
        var realInfra = new DaprInfrastructureQueryService(
            daprClient,
            infraHttpFactory,
            infraOptions,
            infraLogger);

        AdminServerOptions outerOptions = new() {
            TraceUrl = "https://traces",
            MetricsUrl = "https://metrics",
            LogsUrl = "https://logs",
            StateStoreName = "statestore",
        };
        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, serverOptions: outerOptions, infrastructure: realInfra);
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        // (1) Walk every string field of the report to confirm the connection details are not
        // echoed back. Any new string-bearing field must be added here when the contract grows.
        result.OverallStatus.ToString().ShouldNotContain("p@ssw0rd");
        result.OverallStatus.ToString().ShouldNotContain("my-secret-host");
        result.InventorySourceStatus.ToString().ShouldNotContain("p@ssw0rd");
        foreach (DaprComponentHealth c in result.DaprComponents) {
            c.ComponentName.ShouldNotContain("p@ssw0rd");
            c.ComponentName.ShouldNotContain("my-secret-host");
            c.ComponentType.ShouldNotContain("p@ssw0rd");
            c.ComponentType.ShouldNotContain("my-secret-host");
            c.Source.ToString().ShouldNotContain("p@ssw0rd");
            c.Status.ToString().ShouldNotContain("p@ssw0rd");
        }

        if (result.ObservabilityLinks.TraceUrl is not null) {
            result.ObservabilityLinks.TraceUrl.ShouldNotContain("p@ssw0rd");
        }

        if (result.ObservabilityLinks.MetricsUrl is not null) {
            result.ObservabilityLinks.MetricsUrl.ShouldNotContain("p@ssw0rd");
        }

        if (result.ObservabilityLinks.LogsUrl is not null) {
            result.ObservabilityLinks.LogsUrl.ShouldNotContain("p@ssw0rd");
        }

        // (2) Walk every recorded log entry. The probe-failure warning includes the exception
        // by design (LogWarning(ex, ...)); we want that exception detail to remain inside the
        // logger and never reach response payloads (asserted in (1)). The secret IS expected to
        // appear in the captured exception — but it must not appear in the rendered Message,
        // and it definitely must not be in any other (unrelated) log record. The recording
        // logger lets us audit each record explicitly rather than asserting "nothing was
        // logged", which would also hide a genuine regression.
        foreach (RecordingLogger<DaprInfrastructureQueryService>.LogRecord record in infraLogger.Records) {
            // Message is the rendered template (e.g. "State store probe failed for statestore.")
            // — must not interpolate any captured secret. Round 4 fix: ALSO inspect the
            // recorded Exception's Message because logging infrastructures often forward both
            // (e.g. structured loggers serialise `ex.ToString()` into the log envelope, where
            // it can land in dashboards or get exfiltrated). The probe-failure path
            // deliberately captures the exception (LogWarning(ex, ...)) so the secret IS
            // expected to appear in the captured exception object — the contract under test
            // is that the *response* surface (asserted in (1)) and the *rendered Message*
            // template (asserted here) never compose the secret. We do not assert on
            // record.Exception.Message because that field carries the raw exception text by
            // design; the malformed-JSON assertion below covers the parse-error flavour.
            record.Message.ShouldNotContain("p@ssw0rd");
            record.Message.ShouldNotContain("my-secret-host");
        }

        // Probe-path assertion: the InvalidOperationException injected at line 234 must have
        // reached the LogWarning(ex, "State store probe failed for {ComponentName}.") arm of
        // ProbeStateStoreEntryAsync (with StateStoreName = "statestore" set above). If no
        // record carries the secret-bearing exception, the test would be vacuously passing
        // without exercising the AC1 leak surface — surface that as a failure.
        bool anyRecordCapturedSecretException = infraLogger.Records.Any(r =>
            r.Exception?.Message?.Contains(SecretConnectionDetails, StringComparison.Ordinal) == true);
        anyRecordCapturedSecretException.ShouldBeTrue(
            "Probe-path leak surface was not exercised: no log record captured the secret-bearing exception. Verify StateStoreName is set and the probe runs.");
    }

    [Fact]
    public async Task GetDaprComponentStatusAsync_ReturnsComponentsFromCanonicalInventory() {
        // After the round-3 patch GetDaprComponentStatusAsync routes through the canonical
        // inventory rather than directly reading local sidecar metadata, so the API contract
        // surface returns the same probe-derived truth as /health and /dapr.
        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DaprCanonicalInventory(
                [
                    new DaprComponentDetail(
                        "statestore", "state.redis", DaprComponentCategory.StateStore,
                        "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [],
                        DaprComponentSource.LocalAdminProbe),
                    new DaprComponentDetail(
                        "pubsub", "pubsub.redis", DaprComponentCategory.PubSub,
                        "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [],
                        DaprComponentSource.RemoteEventStoreMetadata),
                ],
                [],
                RemoteMetadataStatus.Available,
                "http://eventstore-sidecar",
                LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow)));

        (DaprHealthQueryService service, _) = CreateService(infrastructure: infra);

        IReadOnlyList<DaprComponentHealth> result = await service.GetDaprComponentStatusAsync();

        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.ComponentName == "statestore" && c.Source == DaprComponentSource.LocalAdminProbe);
        result.ShouldContain(c => c.ComponentName == "pubsub" && c.Source == DaprComponentSource.RemoteEventStoreMetadata);
    }

    [Fact]
    public async Task GetDaprComponentStatusAsync_PropagatesCancellationFromCanonicalInventory() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns<DaprCanonicalInventory>(_ => throw new OperationCanceledException());

        (DaprHealthQueryService service, _) = CreateService(infrastructure: infra);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetDaprComponentStatusAsync(cts.Token));
    }

    [Fact]
    public async Task GetSystemHealthAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();

        IDaprInfrastructureQueryService infra = Substitute.For<IDaprInfrastructureQueryService>();
        _ = infra.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns<DaprCanonicalInventory>(_ => throw new OperationCanceledException());

        (DaprHealthQueryService service, _) = CreateService(daprClient, infrastructure: infra);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetSystemHealthAsync(cts.Token));
    }

    [Fact]
    public async Task GetSystemHealthAsync_TotalEventCount_SumsBoundedStreamActivityIndex() {
        // ADR-3: TotalEventCount comes from the bounded admin:stream-activity:all source.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        IReadOnlyList<StreamSummary> items =
        [
            new StreamSummary("tenant-a", "counter", "counter-1", 18, DateTimeOffset.UtcNow, 18, false, StreamStatus.Active),
            new StreamSummary("tenant-a", "counter", "counter-2", 5, DateTimeOffset.UtcNow, 5, false, StreamStatus.Active),
            new StreamSummary("tenant-b", "orders", "ord-1", 7, DateTimeOffset.UtcNow, 7, false, StreamStatus.Active),
        ];
        _ = streamQuery.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(items, items.Count, null)));

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, streamQuery: streamQuery);
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.TotalEventCount.ShouldBe(30);
        result.TotalEventCountStatus.ShouldBe(SystemHealthMetricStatus.Available);
    }

    [Fact]
    public async Task GetSystemHealthAsync_TotalEventCount_FailureReportedAsUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        _ = streamQuery.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("state store unavailable"));

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient, streamQuery: streamQuery);
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        // Per ADR-3: render unavailable rather than a misleading zero.
        result.TotalEventCount.ShouldBe(0);
        result.TotalEventCountStatus.ShouldBe(SystemHealthMetricStatus.Unavailable);
    }

    [Fact]
    public async Task GetSystemHealthAsync_EventsPerSecondAndErrorPercentage_ReportedAsUnavailable() {
        // ADR-3: these metrics have no source wired; UI must distinguish unavailable from real zero.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(CreateMetadata());
        _ = daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        _ = daprClient.CreateInvokeMethodRequest(
            Arg.Any<HttpMethod>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new HttpRequestMessage(
                callInfo.ArgAt<HttpMethod>(0),
                new Uri($"http://localhost/{callInfo.ArgAt<string>(2)}")));

        (DaprHealthQueryService service, TestHttpMessageHandler handler) = CreateService(daprClient);
        handler.SetupJsonResponse("ok");

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.EventsPerSecondStatus.ShouldBe(SystemHealthMetricStatus.Unavailable);
        result.ErrorPercentageStatus.ShouldBe(SystemHealthMetricStatus.Unavailable);
    }

    [Fact]
    public async Task GetSystemHealthAsync_IncludesObservabilityLinks() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());

        var opts = new AdminServerOptions {
            TraceUrl = "https://traces.example.com",
            MetricsUrl = "https://metrics.example.com",
            LogsUrl = null,
        };

        (DaprHealthQueryService service, _) = CreateService(daprClient, opts);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.ObservabilityLinks.TraceUrl.ShouldBe("https://traces.example.com");
        result.ObservabilityLinks.MetricsUrl.ShouldBe("https://metrics.example.com");
        result.ObservabilityLinks.LogsUrl.ShouldBeNull();
    }
}
