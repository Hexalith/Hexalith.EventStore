using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class DaprProjectionDeliveryRetrySchedulerTests {
    [Fact]
    public async Task ScheduleAsync_DifferentShardWorkItems_PersistToDistinctV2LedgerKeys() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionDeliveryRetryLedger, string))(null!, string.Empty));
        var savedKeys = new List<string>();
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryRetryLedger>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                savedKeys.Add(call.ArgAt<string>(1));
                return true;
            });
        var scheduler = new DaprProjectionDeliveryRetryScheduler(
            daprClient,
            Options.Create(new ProjectionOptions()));

        _ = await scheduler.ScheduleAsync(CreateWorkItem("work-a", "aggregate-a"));
        _ = await scheduler.ScheduleAsync(CreateWorkItem("work-b", "aggregate-b"));

        savedKeys.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        savedKeys.ShouldAllBe(static key => key.StartsWith("projection-delivery-retry:ledger:v2:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScheduleAsync_ExistingWorkId_PreservesPersistedWorkItem() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);
        ProjectionDeliveryRetryWorkItem original = CreateWorkItem("work-a", "aggregate-a");
        ProjectionDeliveryRetryWorkItem replacement = original with {
            Attempt = 4,
            PendingRoutes = ["counter-index"],
        };

        ProjectionDeliveryRetryWorkItem first = await scheduler.ScheduleAsync(original);
        ProjectionDeliveryRetryWorkItem second = await scheduler.ScheduleAsync(replacement);

        first.ShouldBe(original);
        second.ShouldBe(original);
        state.Values.SelectMany(static ledger => ledger.Items).ShouldHaveSingleItem().ShouldBe(original);
    }

    [Fact]
    public async Task UpdateAsync_MissingWorkId_DoesNotResurrectWork() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);

        await scheduler.UpdateAsync(CreateWorkItem("work-a", "aggregate-a") with { Attempt = 1 });

        state.Values.SelectMany(static ledger => ledger.Items).ShouldBeEmpty();
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync(
            default!,
            default!,
            default(ProjectionDeliveryRetryLedger)!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task GetDueAsync_AcrossShards_ReturnsBoundedDeterministicOrder() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem later = CreateWorkItem("work-a", "aggregate-a") with { NextDueUtc = now.AddMinutes(-1) };
        ProjectionDeliveryRetryWorkItem first = CreateWorkItem("work-b", "aggregate-b") with { NextDueUtc = now.AddMinutes(-2) };
        ProjectionDeliveryRetryWorkItem future = CreateWorkItem("work-c", "aggregate-c") with { NextDueUtc = now.AddMinutes(1) };
        _ = await scheduler.ScheduleAsync(later);
        _ = await scheduler.ScheduleAsync(first);
        _ = await scheduler.ScheduleAsync(future);

        IReadOnlyList<ProjectionDeliveryRetryWorkItem> due = await scheduler.GetDueAsync(now, 2);

        due.ShouldBe([first, later]);
        _ = await daprClient.Received(1).GetBulkStateAsync<ProjectionDeliveryRetryLedger>(
            "statestore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Count == 64 && keys.Distinct(StringComparer.Ordinal).Count() == 64),
            8,
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAndDeleteAsync_PersistThenRemoveOnlyTargetWorkItem() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem first = CreateWorkItem("work-a", "aggregate-a") with { NextDueUtc = now.AddMinutes(-1) };
        ProjectionDeliveryRetryWorkItem second = CreateWorkItem("work-b", "aggregate-b") with { NextDueUtc = now.AddMinutes(-1) };
        _ = await scheduler.ScheduleAsync(first);
        _ = await scheduler.ScheduleAsync(second);
        ProjectionDeliveryRetryWorkItem updated = first with {
            Attempt = 2,
            PendingRoutes = ["counter-index"],
        };

        await scheduler.UpdateAsync(updated);
        await scheduler.DeleteAsync(second.WorkId);

        (await scheduler.GetDueAsync(now, 8)).ShouldHaveSingleItem().ShouldBe(updated);
    }

    [Fact]
    public async Task ScheduleAsync_EtagConflict_RetriesTargetShard() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                "projection-delivery-retry:ledger:v1",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryRetryLedger?)null);
        _ = daprClient.GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionDeliveryRetryLedger, string))(null!, string.Empty));
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryRetryLedger>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false, true);
        var scheduler = CreateScheduler(daprClient);

        _ = await scheduler.ScheduleAsync(CreateWorkItem("work-a", "aggregate-a"));

        _ = await daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<ProjectionDeliveryRetryLedger>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDueAsync_LegacyLedgerExists_MigratesBeforeScanningShards() {
        ProjectionDeliveryRetryWorkItem legacyWork = CreateWorkItem("work-a", "aggregate-a") with {
            Attempt = 3,
            NextDueUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal) {
            ["projection-delivery-retry:ledger:v1"] = new([legacyWork]),
        };
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);

        ProjectionDeliveryRetryWorkItem due = (await scheduler.GetDueAsync(DateTimeOffset.UtcNow, 8)).ShouldHaveSingleItem();

        due.ShouldBe(legacyWork);
        state.ShouldNotContainKey("projection-delivery-retry:ledger:v1");
        state.Keys.ShouldContain(static key => key.StartsWith("projection-delivery-retry:ledger:v2:", StringComparison.Ordinal));
    }

    private static DaprProjectionDeliveryRetryScheduler CreateScheduler(DaprClient daprClient) =>
        new(daprClient, Options.Create(new ProjectionOptions()));

    private static DaprClient CreateStatefulDaprClient(
        Dictionary<string, ProjectionDeliveryRetryLedger> state) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => state.GetValueOrDefault(call.ArgAt<string>(1))!);
        _ = daprClient.GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                string key = call.ArgAt<string>(1);
                return ((ProjectionDeliveryRetryLedger, string))(
                    state.GetValueOrDefault(key)!,
                    state.ContainsKey(key) ? "etag" : string.Empty);
            });
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryRetryLedger>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                state[call.ArgAt<string>(1)] = call.ArgAt<ProjectionDeliveryRetryLedger>(2);
                return true;
            });
        _ = daprClient.GetBulkStateAsync<ProjectionDeliveryRetryLedger>(
                "statestore",
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<BulkStateItem<ProjectionDeliveryRetryLedger>>)[.. call
                .ArgAt<IReadOnlyList<string>>(1)
                .Select(key => new BulkStateItem<ProjectionDeliveryRetryLedger>(
                    key,
                    state.GetValueOrDefault(key)!,
                    state.ContainsKey(key) ? "etag" : string.Empty))]);
        _ = daprClient.DeleteStateAsync(
                "statestore",
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                _ = state.Remove(call.ArgAt<string>(1));
                return Task.CompletedTask;
            });
        return daprClient;
    }

    private static ProjectionDeliveryRetryWorkItem CreateWorkItem(string workId, string aggregateId) =>
        new(
            workId,
            "tenant-a",
            "counter",
            aggregateId,
            "counter-service",
            "v1",
            1,
            "message-1",
            ["counter-detail"],
            [],
            "message-1",
            "catalog-fingerprint",
            0,
            DateTimeOffset.UtcNow,
            null);
}
