
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
        _ = await infraService.DidNotReceive().GetComponentsAsync(Arg.Any<CancellationToken>());
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
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, []),
        ];

        _ = infraService.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(components);

        // Return null for existing timeline (first entry today)
        _ = daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((DaprComponentHealthTimeline)null!);

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
    public async Task ExecuteAsync_SkipsWrite_WhenNoComponentsReturned() {
        // Arrange
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();

        // Return empty component list (sidecar unreachable)
        _ = infraService.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DaprComponentDetail>());

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
    public async Task ExecuteAsync_ContinuesOnWriteFailure() {
        // Arrange
        var options = new AdminServerOptions { HealthHistoryEnabled = true };

        DaprClient daprClient = Substitute.For<DaprClient>();
        IDaprInfrastructureQueryService infraService = Substitute.For<IDaprInfrastructureQueryService>();

        IReadOnlyList<DaprComponentDetail> components =
        [
            new DaprComponentDetail("statestore", "state.redis", DaprComponentCategory.StateStore, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, []),
        ];

        _ = infraService.GetComponentsAsync(Arg.Any<CancellationToken>())
            .Returns(components);

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
        _ = await infraService.Received().GetComponentsAsync(Arg.Any<CancellationToken>());
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
