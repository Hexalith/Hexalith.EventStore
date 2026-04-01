#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprStorageQueryServiceTests
{
    private const string StateStoreName = "statestore";

    private static DaprStorageQueryService CreateService(DaprClient? daprClient = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            StateStoreName = StateStoreName,
        });

        return new DaprStorageQueryService(
            daprClient,
            options,
            NullLogger<DaprStorageQueryService>.Instance);
    }

    private static StreamStorageInfo CreateStreamInfo(string tenantId, string aggregateId, long eventCount)
        => new(tenantId, "Counter", aggregateId, "CounterAggregate", eventCount, 1024, false, null);

    // === GetStorageOverviewAsync ===

    [Fact]
    public async Task GetStorageOverviewAsync_ReturnsOverview_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new StorageOverview(1000, 50000, [], 10);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageQueryService service = CreateService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant-a");

        result.TotalEventCount.ShouldBe(1000);
        result.TotalSizeBytes.ShouldBe(50000);
        result.TotalStreamCount.ShouldBe(10);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_UsesAllScope_WhenTenantIdIsNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new StorageOverview(5000, 250000, [], 50);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageQueryService service = CreateService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync(null);

        result.TotalEventCount.ShouldBe(5000);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (StorageOverview?)null);

        DaprStorageQueryService service = CreateService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant-a");

        result.TotalEventCount.ShouldBe(0);
        result.TotalStreamCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_FallsBackToStreamCountIndex_WhenTotalStreamCountIsNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var overviewWithoutStreamCount = new StorageOverview(1000, 50000, []);

        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            "admin:storage-overview:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => overviewWithoutStreamCount);

        daprClient.GetStateAsync<long?>(
            StateStoreName,
            "admin:storage-stream-count:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (long?)42);

        DaprStorageQueryService service = CreateService(daprClient);

        StorageOverview result = await service.GetStorageOverviewAsync("tenant-a");

        result.TotalStreamCount.ShouldBe(42);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_Throws_WhenExceptionThrown()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store down"));

        DaprStorageQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetStorageOverviewAsync("tenant-a"));
    }

    [Fact]
    public async Task GetStorageOverviewAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<StorageOverview>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<StorageOverview?>(_ => throw new OperationCanceledException());

        DaprStorageQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetStorageOverviewAsync("tenant-a", cts.Token));
    }

    // === GetHotStreamsAsync ===

    [Fact]
    public async Task GetHotStreamsAsync_ReturnsTopStreams_OrderedByEventCount()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var streams = new List<StreamStorageInfo>
        {
            CreateStreamInfo("tenant-a", "counter-1", 100),
            CreateStreamInfo("tenant-a", "counter-2", 500),
            CreateStreamInfo("tenant-a", "counter-3", 200),
        };

        daprClient.GetStateAsync<List<StreamStorageInfo>>(
            StateStoreName,
            "admin:storage-hot-streams:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => streams);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<StreamStorageInfo> result = await service.GetHotStreamsAsync("tenant-a", 2);

        result.Count.ShouldBe(2);
        result[0].EventCount.ShouldBe(500);
        result[1].EventCount.ShouldBe(200);
    }

    [Fact]
    public async Task GetHotStreamsAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<StreamStorageInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<StreamStorageInfo>?)null);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<StreamStorageInfo> result = await service.GetHotStreamsAsync("tenant-a", 10);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetHotStreamsAsync_Throws_WhenExceptionThrown()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<StreamStorageInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store down"));

        DaprStorageQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetHotStreamsAsync("tenant-a", 10));
    }

    // === GetCompactionJobsAsync ===

    [Fact]
    public async Task GetCompactionJobsAsync_ReturnsJobs_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var jobs = new List<CompactionJob>
        {
            new("op-1", "tenant-a", null, CompactionJobStatus.Completed, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, 500, 1024, null),
        };

        daprClient.GetStateAsync<List<CompactionJob>>(
            StateStoreName,
            "admin:storage-compaction-jobs:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => jobs);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<CompactionJob> result = await service.GetCompactionJobsAsync("tenant-a");

        result.Count.ShouldBe(1);
        result[0].OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task GetCompactionJobsAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<CompactionJob>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<CompactionJob>?)null);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<CompactionJob> result = await service.GetCompactionJobsAsync("tenant-a");

        result.ShouldBeEmpty();
    }

    // === GetSnapshotPoliciesAsync ===

    [Fact]
    public async Task GetSnapshotPoliciesAsync_ReturnsPolicies_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var policies = new List<SnapshotPolicy>
        {
            new("tenant-a", "Counter", "CounterAggregate", 100, DateTimeOffset.UtcNow),
        };

        daprClient.GetStateAsync<List<SnapshotPolicy>>(
            StateStoreName,
            "admin:storage-snapshot-policies:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => policies);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<SnapshotPolicy> result = await service.GetSnapshotPoliciesAsync("tenant-a");

        result.Count.ShouldBe(1);
        result[0].IntervalEvents.ShouldBe(100);
    }

    [Fact]
    public async Task GetSnapshotPoliciesAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<SnapshotPolicy>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<SnapshotPolicy>?)null);

        DaprStorageQueryService service = CreateService(daprClient);

        IReadOnlyList<SnapshotPolicy> result = await service.GetSnapshotPoliciesAsync("tenant-a");

        result.ShouldBeEmpty();
    }
}
