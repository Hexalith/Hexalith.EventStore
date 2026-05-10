
using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprHealthHistoryCollectorTests {
    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ExitsImmediately() {
        // Arrange
        var options = new AdminServerOptions { HealthHistoryEnabled = false };
        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        // Act - start and stop immediately
        await collector.StartAsync(cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();
        await collector.StopAsync(default);

        // Assert - infrastructure service should never be called
        _ = await infraService.DidNotReceive().GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabled_CapturesSnapshot() {
        // Arrange
        var options = new AdminServerOptions {
            HealthHistoryEnabled = true,
            HealthHistoryCaptureIntervalSeconds = 60,
        };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, fakeClock.GetUtcNow(), [], DaprComponentSource.LocalAdminProbe),
        ];

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                components, [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: fakeClock.GetUtcNow()));

        // Return an empty-but-non-null timeline (first entry today). A null! cast would mask
        // a future short-circuit-on-null behaviour added to the collector — the empty
        // timeline is the canonical "first entry today, nothing persisted yet" sentinel.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        bool saveObserved = false;
        _ = daprClient.SaveStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DaprComponentHealthTimeline>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => { saveObserved = true; return Task.CompletedTask; });

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        // Act — drive past the 15s startup delay deterministically.
        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => saveObserved);

        // Assert - state should have been saved
        await daprClient.Received().SaveStateAsync(
            "statestore",
            Arg.Is<string>(k => k.StartsWith("admin:health-history:")),
            Arg.Any<DaprComponentHealthTimeline>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWrite_WhenNoComponentsAndRemoteUnavailable() {
        // Arrange
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        bool inventoryObserved = false;
        // Return empty component list with remote unreachable (do not overwrite history)
        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                inventoryObserved = true;
                return new DaprCanonicalInventory(
                    [], [], RemoteMetadataStatus.Unreachable,
                    "http://eventstore-sidecar", LocalSidecarMetadataAvailable: false,
                    CapturedAtUtc: fakeClock.GetUtcNow());
            });

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        // Act — wait for inventory to be queried; SaveStateAsync should never fire on this branch.
        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => inventoryObserved);

        // Assert - SaveStateAsync should NOT have been called
        await daprClient.DidNotReceive().SaveStateAsync(
            Arg.Any<string>(),
            Arg.Is<string>(k => k.StartsWith("admin:health-history:")),
            Arg.Any<DaprComponentHealthTimeline>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PersistsEmptyAvailableSample_AsRealZero() {
        // Round 3 patch F18 — refined skip rule: the collector now distinguishes "remote
        // confirmed empty payload" (persist as a real-zero sample) from "no usable evidence"
        // (skip to preserve last-good). An Available status with zero components is the
        // canonical "no components configured" signal and must be recorded — operators need
        // to see real-zero in the timeline.
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                [], [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: fakeClock.GetUtcNow()));

        // Empty existing timeline — first sample today.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        bool saveObserved = false;
        _ = daprClient.SaveStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DaprComponentHealthTimeline>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => { saveObserved = true; return Task.CompletedTask; });

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => saveObserved);

        // Available + zero components is persisted as a real-zero sample — the SaveStateAsync
        // call carries an empty Entries collection with HasData: true.
        await daprClient.Received().SaveStateAsync(
            Arg.Any<string>(),
            Arg.Is<string>(k => k.StartsWith("admin:health-history:")),
            Arg.Is<DaprComponentHealthTimeline>(t => t.HasData && t.Entries.Count == 0),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWrite_WhenRemoteUnreachableAndNoUsableEvidence() {
        // AC4: never overwrite a previous healthy timeline when canonical evidence is empty
        // (remote unreachable AND no probed/local-attributed component rows). Preserves
        // last-good history.
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        bool inventoryObserved = false;
        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                inventoryObserved = true;
                return new DaprCanonicalInventory(
                    [], [], RemoteMetadataStatus.Unreachable,
                    "http://eventstore-sidecar", LocalSidecarMetadataAvailable: false,
                    CapturedAtUtc: fakeClock.GetUtcNow());
            });

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => inventoryObserved);

        await daprClient.DidNotReceive().SaveStateAsync(
            Arg.Any<string>(),
            Arg.Is<string>(k => k.StartsWith("admin:health-history:")),
            Arg.Any<DaprComponentHealthTimeline>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PersistsRemotePubSubRow_WhenInventoryIncludesPubSub() {
        // ST5 cross-page regression: a fixture with remote `pubsub` metadata yields a Pub/Sub
        // entry on the persisted timeline. Pairs with the same regression on /dapr and /health.
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, fakeClock.GetUtcNow(), [], DaprComponentSource.LocalAdminProbe),
            new DaprComponentDetail("pubsub", "pubsub.redis", DaprComponentCategory.PubSub, "v1", HealthStatus.Healthy, fakeClock.GetUtcNow(), [], DaprComponentSource.RemoteEventStoreMetadata),
        ];

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                components, [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: fakeClock.GetUtcNow()));

        // Seed an empty-but-non-null timeline so the test exercises the
        // "first sample of the day" path explicitly. Returning null! would mask a future
        // defensive null short-circuit added to the collector.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        DaprComponentHealthTimeline? captured = null;
        _ = daprClient.SaveStateAsync(
                Arg.Any<string>(),
                Arg.Is<string>(k => k.StartsWith("admin:health-history:")),
                Arg.Any<DaprComponentHealthTimeline>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                captured = callInfo.ArgAt<DaprComponentHealthTimeline>(2);
                return Task.CompletedTask;
            });

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => captured is not null);

        _ = captured.ShouldNotBeNull();
        captured.Entries.ShouldContain(e =>
            e.ComponentName == "pubsub"
            && e.ComponentType == "pubsub.redis"
            && e.Status == HealthStatus.Healthy);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesOnWriteFailure() {
        // Arrange
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();
        FakeTimeProvider fakeClock = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, fakeClock.GetUtcNow(), [], DaprComponentSource.LocalAdminProbe),
        ];

        bool inventoryObserved = false;
        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                inventoryObserved = true;
                return new DaprCanonicalInventory(
                    components, [], RemoteMetadataStatus.Available,
                    "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                    CapturedAtUtc: fakeClock.GetUtcNow());
            });

        // Seed an empty-but-non-null timeline so the test exercises the
        // "first sample of the day" path explicitly. Returning null! would mask a future
        // defensive null short-circuit added to the collector.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        // Fail on save
        _ = daprClient.SaveStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DaprComponentHealthTimeline>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Write failed"));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService, fakeClock);

        using CancellationTokenSource cts = new();

        await DriveFirstCaptureAsync(collector, fakeClock, cts, () => inventoryObserved);

        // Assert - service was called (collector didn't crash)
        _ = await infraService.Received().GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>());
    }

    private static DaprHealthHistoryCollector CreateCollector(
        AdminServerOptions options,
        DaprClient daprClient,
        IDaprInfrastructureQueryService infraService,
        TimeProvider? timeProvider = null) {
        ServiceCollection services = new();
        _ = services.AddSingleton(daprClient);
        _ = services.AddSingleton(infraService);
        ServiceProvider provider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IOptions<AdminServerOptions> opts = Options.Create(options);

        return new DaprHealthHistoryCollector(
            scopeFactory,
            opts,
            NullLogger<DaprHealthHistoryCollector>.Instance,
            timeProvider);
    }

    /// <summary>
    /// Round 5 P11: deterministic helper that drives the collector past its 15-second startup
    /// delay using a <see cref="FakeTimeProvider"/> instead of a 17-second wall-clock sleep.
    /// Advances the fake clock past the startup delay and polls <paramref name="observationReady"/>
    /// for up to 2 seconds so the awaited continuation can run. Tests can pass a predicate that
    /// closes over an <see cref="NSubstitute"/> received-call check or a captured-state flag.
    /// </summary>
    private static async Task DriveFirstCaptureAsync(
        DaprHealthHistoryCollector collector,
        FakeTimeProvider fakeClock,
        CancellationTokenSource cts,
        Func<bool> observationReady) {
        await collector.StartAsync(cts.Token);

        // Advance in small slices so ExecuteAsync has a chance to schedule its TimeProvider-backed
        // startup delay before the fake clock moves past it. A single eager Advance can race the
        // background task and leave the delay scheduled in the future.
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !observationReady()) {
            fakeClock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(20).ConfigureAwait(false);
        }

        await cts.CancelAsync();
        await collector.StopAsync(default);

        observationReady().ShouldBeTrue("collector should capture its first snapshot after the fake startup delay is advanced");
    }
}
