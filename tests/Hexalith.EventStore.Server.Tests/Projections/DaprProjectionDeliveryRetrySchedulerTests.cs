using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
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
    public async Task UpdateAsync_UnclaimedWorkIsRejectedWithoutResurrection() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            scheduler.UpdateAsync(CreateWorkItem("work-a", "aggregate-a") with { Attempt = 1 }));

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
    public async Task ClaimedUpdateAndDelete_PersistThenRemoveOnlyTargetWorkItem() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var scheduler = CreateScheduler(daprClient);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem first = CreateWorkItem("work-a", "aggregate-a") with { NextDueUtc = now.AddMinutes(-1) };
        ProjectionDeliveryRetryWorkItem second = CreateWorkItem("work-b", "aggregate-b") with { NextDueUtc = now.AddMinutes(-1) };
        first = await scheduler.ScheduleAsync(first);
        second = await scheduler.ScheduleAsync(second);
        ProjectionDeliveryRetryWorkItem claimedFirst = (await scheduler.TryAcquireAsync(
            first,
            "replica-first",
            now,
            TimeSpan.FromMinutes(1))).ShouldNotBeNull();
        ProjectionDeliveryRetryWorkItem claimedSecond = (await scheduler.TryAcquireAsync(
            second,
            "replica-second",
            now,
            TimeSpan.FromMinutes(1))).ShouldNotBeNull();
        ProjectionDeliveryRetryWorkItem updated = claimedFirst with {
            Attempt = 2,
            PendingRoutes = ["counter-index"],
        };

        (await scheduler.TryUpdateAsync(updated)).ShouldBeTrue();
        (await scheduler.TryDeleteAsync(claimedSecond)).ShouldBeTrue();

        ProjectionDeliveryRetryWorkItem persisted = (await scheduler.GetDueAsync(now, 8)).ShouldHaveSingleItem();
        persisted.Attempt.ShouldBe(2);
        persisted.PendingRoutes.ShouldBe(["counter-index"]);
        persisted.LeaseOwner.ShouldBeNull();
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
        var scheduler = CreateScheduler(daprClient, enableLegacyMigration: true);

        ProjectionDeliveryRetryWorkItem due = (await scheduler.GetDueAsync(DateTimeOffset.UtcNow, 8)).ShouldHaveSingleItem();

        due.ShouldBe(legacyWork);
        state.ShouldNotContainKey("projection-delivery-retry:ledger:v1");
        state.Keys.ShouldContain(static key => key.StartsWith("projection-delivery-retry:ledger:v2:", StringComparison.Ordinal));
        await daprClient.Received(1).SaveStateAsync(
            "statestore",
            "projection-delivery-retry:protocol",
            "v2-ready:v1-writers-quiesced",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDueAsync_MalformedLegacyItem_DoesNotPoisonMaintenanceMigration() {
        ProjectionDeliveryRetryWorkItem malformed = CreateWorkItem("work-invalid", "aggregate-a") with {
            PendingRoutes = null!,
        };
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal) {
            ["projection-delivery-retry:ledger:v1"] = new([malformed]),
        };
        DaprClient daprClient = CreateStatefulDaprClient(state);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(daprClient, enableLegacyMigration: true);

        IReadOnlyList<ProjectionDeliveryRetryWorkItem> due = await scheduler.GetDueAsync(DateTimeOffset.UtcNow, 8);

        due.ShouldBeEmpty();
        state.ShouldNotContainKey("projection-delivery-retry:ledger:v1");
        await daprClient.Received(1).SaveStateAsync(
            "statestore",
            "projection-delivery-retry:protocol",
            "v2-ready:v1-writers-quiesced",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAcquireAsync_SerializesDifferentHeadsForTheSameAggregateAcrossReplicas() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(daprClient);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem older = await scheduler.ScheduleAsync(CreateWorkItem("work-old", "aggregate-a"));
        ProjectionDeliveryRetryWorkItem newer = await scheduler.ScheduleAsync(
            CreateWorkItem("work-new", "aggregate-a") with { HeadSequence = 2, HeadMessageId = "message-2" });

        ProjectionDeliveryRetryWorkItem? claimedNewer = await scheduler.TryAcquireAsync(
            newer,
            "replica-new",
            now,
            TimeSpan.FromMinutes(1));
        ProjectionDeliveryRetryWorkItem? blockedOlder = await scheduler.TryAcquireAsync(
            older,
            "replica-old",
            now,
            TimeSpan.FromMinutes(1));

        claimedNewer.ShouldNotBeNull();
        blockedOlder.ShouldBeNull();
    }

    [Fact]
    public async Task TryUpdateAsync_RejectsStaleClaimedRevision() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(daprClient);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem scheduled = await scheduler.ScheduleAsync(CreateWorkItem("work-a", "aggregate-a"));
        ProjectionDeliveryRetryWorkItem claimed = (await scheduler.TryAcquireAsync(
            scheduled,
            "replica-a",
            now,
            TimeSpan.FromMinutes(1))).ShouldNotBeNull();

        bool first = await scheduler.TryUpdateAsync(claimed with { Attempt = 1 });
        bool stale = await scheduler.TryUpdateAsync(claimed with { Attempt = 7 });

        first.ShouldBeTrue();
        stale.ShouldBeFalse();
        state.Values.SelectMany(static ledger => ledger.Items).ShouldHaveSingleItem().Attempt.ShouldBe(1);
    }

    [Fact]
    public async Task GetDueAsync_MalformedPersistedItemDoesNotPoisonValidWork() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectionDeliveryRetryWorkItem valid = CreateWorkItem("work-valid", "aggregate-a") with {
            NextDueUtc = now.AddMinutes(-1),
        };
        ProjectionDeliveryRetryWorkItem malformed = CreateWorkItem("work-invalid", "aggregate-b") with {
            PendingRoutes = null!,
            NextDueUtc = now.AddMinutes(-1),
        };
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal) {
            ["projection-delivery-retry:ledger:v2:00"] = new([malformed, valid]),
        };
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(CreateStatefulDaprClient(state));

        IReadOnlyList<ProjectionDeliveryRetryWorkItem> due = await scheduler.GetDueAsync(now, 8);

        due.ShouldHaveSingleItem().WorkId.ShouldBe("work-valid");
    }

    [Fact]
    public async Task ScheduleAsync_RejectsReservationFenceOutsidePendingRoutes() {
        var scheduler = CreateScheduler(Substitute.For<DaprClient>());
        ProjectionDeliveryRetryWorkItem malformed = CreateWorkItem("work-invalid", "aggregate-a") with {
            ReservationFencingTokens = new Dictionary<string, long>(StringComparer.Ordinal) {
                ["counter-index"] = 1,
            },
        };

        _ = await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleAsync(malformed));
    }

    [Fact]
    public async Task ScheduleAsync_PreservesPositivePendingRouteReservationFence() {
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(CreateStatefulDaprClient(state));
        ProjectionDeliveryRetryWorkItem work = CreateWorkItem("work-fenced", "aggregate-a") with {
            ReservationFencingTokens = new Dictionary<string, long>(StringComparer.Ordinal) {
                ["counter-detail"] = 7,
            },
        };

        ProjectionDeliveryRetryWorkItem saved = await scheduler.ScheduleAsync(work);

        saved.ReservationFencingTokens["counter-detail"].ShouldBe(7);
    }

    private static DaprProjectionDeliveryRetryScheduler CreateScheduler(
        DaprClient daprClient,
        bool enableLegacyMigration = false) =>
        new(
            daprClient,
            Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDispatchOptions {
                EnableLegacyRetryLedgerMigration = enableLegacyMigration,
                LegacyRetryLedgerWritersQuiesced = enableLegacyMigration,
                LegacyRetryLedgerMigrationMarker = enableLegacyMigration ? "v1-writers-quiesced" : null,
            }));

    private static DaprClient CreateStatefulDaprClient(
        Dictionary<string, ProjectionDeliveryRetryLedger> state) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var leases = new Dictionary<string, ProjectionDeliveryRetryLease>(StringComparer.Ordinal);
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
        _ = daprClient.GetStateAndETagAsync<ProjectionDeliveryRetryLease>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                string key = call.ArgAt<string>(1);
                return ((ProjectionDeliveryRetryLease, string))(
                    leases.GetValueOrDefault(key)!,
                    leases.ContainsKey(key) ? "etag" : string.Empty);
            });
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryRetryLease>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                leases[call.ArgAt<string>(1)] = call.ArgAt<ProjectionDeliveryRetryLease>(2);
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
        _ = daprClient.ExecuteStateTransactionAsync(
                "statestore",
                Arg.Any<IReadOnlyList<StateTransactionRequest>>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                foreach (StateTransactionRequest request in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1)) {
                    if (request.OperationType == StateOperationType.Delete) {
                        _ = state.Remove(request.Key);
                    }
                }

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
