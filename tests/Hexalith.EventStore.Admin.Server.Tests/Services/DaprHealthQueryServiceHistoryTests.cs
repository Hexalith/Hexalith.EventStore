#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

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

public class DaprHealthQueryServiceHistoryTests
{
    private static DaprHealthQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions();

        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        return new DaprHealthQueryService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprHealthQueryService>.Instance);
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_ReturnsMergedEntries_FromMultipleDays()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset yesterday = now.AddDays(-1);

        // Two day partitions
        var todayTimeline = new DaprComponentHealthTimeline(
            [new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddHours(-1))],
            HasData: true);

        var yesterdayTimeline = new DaprComponentHealthTimeline(
            [new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Degraded, yesterday)],
            HasData: true);

        string todayKey = $"admin:health-history:{now:yyyyMMdd}";
        string yesterdayKey = $"admin:health-history:{yesterday:yyyyMMdd}";

        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", todayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(todayTimeline);

        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", yesterdayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(yesterdayTimeline);

        DaprHealthQueryService service = CreateService(daprClient);

        DaprComponentHealthTimeline result = await service.GetComponentHealthHistoryAsync(
            yesterday.AddHours(-1), now.AddHours(1), null, default);

        result.HasData.ShouldBeTrue();
        result.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_FiltersByComponentName_CaseInsensitive()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var timeline = new DaprComponentHealthTimeline(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now),
                new DaprHealthHistoryEntry("pubsub", "pubsub.redis", HealthStatus.Healthy, now),
            ],
            HasData: true);

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(timeline);

        DaprHealthQueryService service = CreateService(daprClient);

        DaprComponentHealthTimeline result = await service.GetComponentHealthHistoryAsync(
            now.AddHours(-1), now.AddHours(1), "STATESTORE", default);

        result.Entries.Count.ShouldBe(1);
        result.Entries[0].ComponentName.ShouldBe("statestore");
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_FiltersByTimeRange()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var timeline = new DaprComponentHealthTimeline(
            [
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-30)),
                new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Degraded, now.AddHours(-3)),
            ],
            HasData: true);

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(timeline);

        DaprHealthQueryService service = CreateService(daprClient);

        // Query last hour only
        DaprComponentHealthTimeline result = await service.GetComponentHealthHistoryAsync(
            now.AddHours(-1), now, null, default);

        result.Entries.Count.ShouldBe(1);
        result.Entries[0].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_ReturnsEmptyTimeline_WhenNoData()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns((DaprComponentHealthTimeline)null!);

        DaprHealthQueryService service = CreateService(daprClient);

        DaprComponentHealthTimeline result = await service.GetComponentHealthHistoryAsync(
            now.AddHours(-1), now, null, default);

        result.HasData.ShouldBeFalse();
        result.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_TruncatesResults_WhenExceedingCap()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Generate entries exceeding the cap
        var entries = Enumerable.Range(0, 15)
            .Select(i => new DaprHealthHistoryEntry("statestore", "state.redis", HealthStatus.Healthy, now.AddMinutes(-i)))
            .ToList();

        var timeline = new DaprComponentHealthTimeline(entries.AsReadOnly(), HasData: true);

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(timeline);

        // Set low cap for testing
        var options = new AdminServerOptions { MaxHealthHistoryEntriesPerQuery = 10 };
        DaprHealthQueryService service = CreateService(daprClient, options);

        DaprComponentHealthTimeline result = await service.GetComponentHealthHistoryAsync(
            now.AddHours(-1), now.AddHours(1), null, default);

        result.IsTruncated.ShouldBeTrue();
        result.Entries.Count.ShouldBe(10);
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_Throws_WhenStateStoreUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unavailable"));

        DaprHealthQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetComponentHealthHistoryAsync(
                now.AddHours(-1), now, null, default));
    }

    [Fact]
    public async Task GetComponentHealthHistoryAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string dayKey = $"admin:health-history:{now:yyyyMMdd}";
        daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            "statestore", dayKey, cancellationToken: Arg.Any<CancellationToken>())
            .Returns<DaprComponentHealthTimeline>(_ => throw new OperationCanceledException());

        DaprHealthQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetComponentHealthHistoryAsync(
                now.AddHours(-1), now, null, cts.Token));
    }
}
