
using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [], DaprComponentSource.LocalAdminProbe),
        ];

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                components, [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        // Return an empty-but-non-null timeline (first entry today). A null! cast would mask
        // a future short-circuit-on-null behaviour added to the collector — the empty
        // timeline is the canonical "first entry today, nothing persisted yet" sentinel.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        // Act - start, wait for initial delay + first capture, then stop
        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17)); // 15s delay + 2s buffer
        await cts.CancelAsync();
        await collector.StopAsync(default);

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

        // Return empty component list with remote unreachable (do not overwrite history)
        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                [], [], RemoteMetadataStatus.Unreachable,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: false,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        // Act
        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17));
        await cts.CancelAsync();
        await collector.StopAsync(default);

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

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                [], [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        // Empty existing timeline — first sample today.
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DaprComponentHealthTimeline([], HasData: false));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17));
        await cts.CancelAsync();
        await collector.StopAsync(default);

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

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                [], [], RemoteMetadataStatus.Unreachable,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: false,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17));
        await cts.CancelAsync();
        await collector.StopAsync(default);

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

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [], DaprComponentSource.LocalAdminProbe),
            new DaprComponentDetail("pubsub", "pubsub.redis", DaprComponentCategory.PubSub, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [], DaprComponentSource.RemoteEventStoreMetadata),
        ];

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                components, [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((DaprComponentHealthTimeline)null!);

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

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17));
        await cts.CancelAsync();
        await collector.StopAsync(default);

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

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, [], DaprComponentSource.LocalAdminProbe),
        ];

        _ = infraService.GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprCanonicalInventory(
                components, [], RemoteMetadataStatus.Available,
                "http://eventstore-sidecar", LocalSidecarMetadataAvailable: true,
                CapturedAtUtc: DateTimeOffset.UtcNow));

        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((DaprComponentHealthTimeline)null!);

        // Fail on save
        _ = daprClient.SaveStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DaprComponentHealthTimeline>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Write failed"));

        DaprHealthHistoryCollector collector = CreateCollector(options, daprClient, infraService);

        using CancellationTokenSource cts = new();

        // Act - should not throw
        await collector.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(17));
        await cts.CancelAsync();
        await collector.StopAsync(default);

        // Assert - service was called (collector didn't crash)
        _ = await infraService.Received().GetCanonicalDaprInventoryAsync(Arg.Any<CancellationToken>());
    }

    private static DaprHealthHistoryCollector CreateCollector(
        AdminServerOptions options,
        DaprClient daprClient,
        IDaprInfrastructureQueryService infraService) {
        ServiceCollection services = new();
        _ = services.AddSingleton(daprClient);
        _ = services.AddSingleton(infraService);
        ServiceProvider provider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IOptions<AdminServerOptions> opts = Options.Create(options);

        return new DaprHealthHistoryCollector(
            scopeFactory,
            opts,
            NullLogger<DaprHealthHistoryCollector>.Instance);
    }
}
