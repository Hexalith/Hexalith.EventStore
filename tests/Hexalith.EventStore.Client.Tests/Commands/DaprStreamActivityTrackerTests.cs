using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Client.Tests.Commands;

public class DaprStreamActivityTrackerTests {
    private const string ActivityIndexKey = "admin:stream-activity:all";
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private DaprStreamActivityTracker CreateTracker()
        => new(_daprClient, Options.Create(new CommandStatusOptions()), NullLogger<DaprStreamActivityTracker>.Instance);

    [Fact]
    public async Task TrackAsync_NewStream_InsertsNewSummary() {
        DaprStreamActivityTracker tracker = CreateTracker();
        SetupGetStateAndEtag(ActivityIndexKey, null, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 3, DateTimeOffset.UtcNow);

        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<StreamSummary>>(items =>
                items.Count == 1
                && items[0].TenantId == "tenant-a"
                && items[0].Domain == "Counter"
                && items[0].AggregateId == "agg-1"
                && items[0].EventCount == 3
                && items[0].LastEventSequence == 3
                && items[0].HasSnapshot == false
                && items[0].StreamStatus == StreamStatus.Active),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_ExistingStream_AccumulatesEventCountAndSequence() {
        DaprStreamActivityTracker tracker = CreateTracker();
        var existing = new List<StreamSummary>
        {
            new("tenant-a", "Counter", "agg-1", 3, DateTimeOffset.UtcNow.AddMinutes(-5), 3, true, StreamStatus.Active),
        };
        SetupGetStateAndEtag(ActivityIndexKey, existing, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 2, DateTimeOffset.UtcNow);

        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<StreamSummary>>(items =>
                items.Count == 1
                && items[0].EventCount == 5
                && items[0].LastEventSequence == 5
                && items[0].HasSnapshot == true),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_ZeroNewEvents_IsNoOp() {
        DaprStreamActivityTracker tracker = CreateTracker();

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 0, DateTimeOffset.UtcNow);

        _ = await _daprClient.DidNotReceive().GetStateAndETagAsync<List<StreamSummary>>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_DifferentAggregates_KeepsBothEntries() {
        DaprStreamActivityTracker tracker = CreateTracker();
        var existing = new List<StreamSummary>
        {
            new("tenant-a", "Counter", "agg-1", 2, DateTimeOffset.UtcNow.AddMinutes(-5), 2, false, StreamStatus.Active),
        };
        SetupGetStateAndEtag(ActivityIndexKey, existing, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-2", 1, DateTimeOffset.UtcNow);

        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<StreamSummary>>(items =>
                items.Count == 2
                && items.Any(s => s.AggregateId == "agg-1")
                && items.Any(s => s.AggregateId == "agg-2")),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_SameAggregateDifferentTenants_KeepsBothEntries() {
        DaprStreamActivityTracker tracker = CreateTracker();
        var existing = new List<StreamSummary>
        {
            new("tenant-b", "Counter", "agg-1", 2, DateTimeOffset.UtcNow.AddMinutes(-5), 2, false, StreamStatus.Active),
        };
        SetupGetStateAndEtag(ActivityIndexKey, existing, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 1, DateTimeOffset.UtcNow);

        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<StreamSummary>>(items =>
                items.Count == 2
                && items.Any(s => s.TenantId == "tenant-a")
                && items.Any(s => s.TenantId == "tenant-b")),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_EtagMismatch_RetriesUntilSaveSucceeds() {
        DaprStreamActivityTracker tracker = CreateTracker();
        _ = _daprClient.GetStateAndETagAsync<List<StreamSummary>>(
            "statestore",
            ActivityIndexKey,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                ([], "etag-1"),
                ([], "etag-2"));

        _ = _daprClient.TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Any<List<StreamSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 1, DateTimeOffset.UtcNow);

        _ = await _daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Any<List<StreamSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_DaprThrows_SwallowsException() {
        DaprStreamActivityTracker tracker = CreateTracker();
        _ = _daprClient.GetStateAndETagAsync<List<StreamSummary>>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        await tracker.TrackAsync("tenant-a", "Counter", "agg-1", 1, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task TrackAsync_ExceedsMaxEntries_TrimsOldestByLastActivityUtc() {
        DaprStreamActivityTracker tracker = CreateTracker();
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        var existing = new List<StreamSummary>();
        for (int i = 0; i < 1000; i++) {
            existing.Add(new StreamSummary(
                "tenant-a",
                "Counter",
                $"agg-{i}",
                1,
                baseTime.AddMinutes(-i),
                1,
                false,
                StreamStatus.Active));
        }

        SetupGetStateAndEtag(ActivityIndexKey, existing, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync("tenant-a", "Counter", "agg-new", 1, baseTime.AddMinutes(1));

        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<StreamSummary>>(items =>
                items.Count == 1000
                && items[0].AggregateId == "agg-new"
                && !items.Any(s => s.AggregateId == "agg-999")),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    private void SetupGetStateAndEtag(string key, List<StreamSummary>? value, string etag) => _ = _daprClient.GetStateAndETagAsync<List<StreamSummary>>(
            "statestore",
            key,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((List<StreamSummary>, string))(value!, etag));

    private void SetupTrySave(string key, bool result) => _ = _daprClient.TrySaveStateAsync(
            "statestore",
            key,
            Arg.Any<List<StreamSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(result);
}
