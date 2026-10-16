using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using Dapr.Actors.Client;
using Dapr.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionDeliveryRetrySchedulerTests
{
    [Theory]
    [InlineData("source_proof_missing")]
    [InlineData("guard_activation_required")]
    [InlineData("cleanup_failed")]
    [InlineData("cleanup_threw")]
    [InlineData("newer_completed_head")]
    public async Task GuardedFailure_RealSchedulerChargesNineActivationsAndKeepsStablePendingWorkAfterAttemptSaturates(string failure)
    {
        var ledgers = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        var scheduler = CreateScheduler(CreateStatefulDaprClient(ledgers));
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-15T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        ProjectionDeliveryRetryWorkItem original = CreateWorkItem("guard-work", "guard-aggregate") with { NextDueUtc = clock.GetUtcNow() };
        await scheduler.ScheduleAsync(original).ConfigureAwait(true);
        IProjectionSourceGuardDelivery guard = Substitute.For<IProjectionSourceGuardDelivery>();
        bool current = failure is "cleanup_failed" or "cleanup_threw" or "newer_completed_head";
        if (failure == "cleanup_threw")
            guard.ReclaimCompletedReplayAsync(Arg.Any<AggregateIdentity>(), Arg.Any<ProjectionDeliveryRetryWorkItem>(),
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns<bool>(_ => throw new IOException("Injected cleanup transport failure."));
        guard.TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(
            new ProjectionSourceGuardDeliveryResult(true, current, current ? null : failure)
            {
                ObservedHeadSequence = original.HeadSequence + (failure == "newer_completed_head" ? 1 : 0),
                ObservedHeadMessageId = original.HeadMessageId, CatalogFingerprint = original.CatalogFingerprint,
            });
        using var worker = new ProjectionDeliveryRetryWorker(scheduler, Substitute.For<INamedProjectionDispatchCoordinator>(),
            Substitute.For<IActorProxyFactory>(), Substitute.For<IEventPayloadProtectionService>(), Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()), Substitute.For<IProjectionRebuildCheckpointStore>(), clock,
            NullLogger<ProjectionDeliveryRetryWorker>.Instance, guard);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        for (int activation = 1; activation <= 9; activation++)
        {
            await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);
            ProjectionDeliveryRetryWorkItem pending = ledgers.Values.SelectMany(static ledger => ledger.Items).Single();
            pending.Attempt.ShouldBe(Math.Min(activation, 8));
            pending.Revision.ShouldBe(activation * 2);
            pending.DispatchId.ShouldBe(original.DispatchId);
            pending.HeadMessageId.ShouldBe(original.HeadMessageId);
            pending.PendingRoutes.ShouldBe(original.PendingRoutes);
            pending.TerminalRoutes.ShouldBeEmpty();
            pending.LeaseOwner.ShouldBeNull();
            pending.LastReasonCode.ShouldBe(failure == "cleanup_threw" ? "delivery_state_unavailable"
                : current ? "delivery_reconciliation_required" : failure);
            pending.NextDueUtc.ShouldBe(clock.GetUtcNow() + TimeSpan.FromSeconds(Math.Pow(2, Math.Min(activation, 8) - 1)));
            clock.Advance(pending.NextDueUtc - clock.GetUtcNow());
        }
        cost[ProjectionDeliveryCost.SchedulerScans].ShouldBe(9);
        cost[ProjectionDeliveryCost.RetryActivations].ShouldBe(9);
        cost[ProjectionDeliveryCost.SchedulerShardReads].ShouldBe(9 * (64 + 2)); // full scan plus claim/update shard reads
        cost[ProjectionDeliveryCost.SchedulerReadBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.SchedulerWriteAttempts].ShouldBe(18);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionRetryCost " + JsonSerializer.Serialize(new
        {
            failure, activations = cost[ProjectionDeliveryCost.RetryActivations], saturatedAttempt = 8,
            cost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => cost[category]),
        }));
    }

    [Fact]
    public async Task GuardedTailAlreadyUpdatedWork_OriginalRevisionCannotDoubleDeferItsAttemptOrDueTime()
    {
        var ledgers = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal);
        var scheduler = CreateScheduler(CreateStatefulDaprClient(ledgers));
        var clock = new FakeTimeProvider();
        ProjectionDeliveryRetryWorkItem original = CreateWorkItem("guard-work", "guard-aggregate") with { NextDueUtc = clock.GetUtcNow() };
        await scheduler.ScheduleAsync(original).ConfigureAwait(true);
        IProjectionSourceGuardDelivery guard = Substitute.For<IProjectionSourceGuardDelivery>();
        DateTimeOffset due = clock.GetUtcNow().AddMinutes(4);
        guard.TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            ProjectionDeliveryRetryWorkItem claimed = (await scheduler.TryAcquireAsync(original, "tail-owner", clock.GetUtcNow(),
                TimeSpan.FromMinutes(1)).ConfigureAwait(false))!;
            await scheduler.TryUpdateAsync(claimed with { Attempt = 3, NextDueUtc = due, LastReasonCode = "delivery_in_progress" })
                .ConfigureAwait(false);
            return new ProjectionSourceGuardDeliveryResult(true, false, "delivery_in_progress");
        });
        using var worker = new ProjectionDeliveryRetryWorker(scheduler, Substitute.For<INamedProjectionDispatchCoordinator>(),
            Substitute.For<IActorProxyFactory>(), Substitute.For<IEventPayloadProtectionService>(), Options.Create(new EventStoreActorOptions()),
            Options.Create(new ProjectionDispatchOptions()), Substitute.For<IProjectionRebuildCheckpointStore>(), clock,
            NullLogger<ProjectionDeliveryRetryWorker>.Instance, guard);
        await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);
        ProjectionDeliveryRetryWorkItem pending = ledgers.Values.SelectMany(static ledger => ledger.Items).Single();
        pending.Attempt.ShouldBe(3);
        pending.Revision.ShouldBe(2);
        pending.NextDueUtc.ShouldBe(due);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedShardOccupancy_ChargesEverySerializedEntryAndPreservesAllUnrelatedPendingWork(bool uneven)
    {
        DateTimeOffset due = DateTimeOffset.UtcNow.AddMinutes(-1);
        Dictionary<string, ProjectionDeliveryRetryLedger> state = OccupiedLedgers(uneven, due);
        var scheduler = CreateScheduler(CreateStatefulDaprClient(state));
        long expectedBytes = state.Values.Sum(static ledger => (long)JsonSerializer.SerializeToUtf8Bytes(ledger, JsonSerializerOptions.Web).Length);
        foreach (ProjectionDeliveryRetryLedger ledger in state.Values)
            JsonSerializer.SerializeToUtf8Bytes(ledger, JsonSerializerOptions.Web).Length.ShouldBeLessThanOrEqualTo(512 + (4096 * ledger.Items.Count));
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await scheduler.GetDueAsync(due.AddSeconds(1), 64).ConfigureAwait(true)).Count.ShouldBe(64);
        cost[ProjectionDeliveryCost.SchedulerScans].ShouldBe(1);
        cost[ProjectionDeliveryCost.SchedulerShardReads].ShouldBe(64);
        cost[ProjectionDeliveryCost.SchedulerEntries].ShouldBe(4096 + (uneven ? 64 : 0));
        cost[ProjectionDeliveryCost.SchedulerReadBytes].ShouldBe(expectedBytes);
        expectedBytes.ShouldBeLessThanOrEqualTo(16_809_984 + (uneven ? 262_144 : 0));
        cost[ProjectionDeliveryCost.SchedulerWriteAttempts].ShouldBe(0);
        state.Values.Sum(static ledger => ledger.Items.Count).ShouldBe(4096 + (uneven ? 64 : 0));
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionSchedulerCost " + JsonSerializer.Serialize(new
        {
            uneven, shards = 64, items = cost[ProjectionDeliveryCost.SchedulerEntries],
            reads = cost[ProjectionDeliveryCost.SchedulerShardReads], bytes = cost[ProjectionDeliveryCost.SchedulerReadBytes],
            writeAttempts = cost[ProjectionDeliveryCost.SchedulerWriteAttempts],
        }));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public async Task SharedShardMutationsChargeEveryActualPreAndPostBodyIncludingFourLostCasAttempts(bool uneven, int refused)
    {
        DateTimeOffset due = DateTimeOffset.UtcNow.AddMinutes(-1);
        Dictionary<string, ProjectionDeliveryRetryLedger> state = OccupiedLedgers(uneven, due);
        using DaprClient client = CreateStatefulDaprClient(state);
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(client);
        string key = "projection-delivery-retry:ledger:v2:00";
        ProjectionDeliveryRetryWorkItem original = state[key].Items[0];
        ProjectionDeliveryRetryWorkItem[] unrelated = [.. state.Values.SelectMany(static ledger => ledger.Items)
            .Where(item => item.WorkId != original.WorkId)];
        long scan = state.Values.Sum(static ledger => (long)JsonSerializer.SerializeToUtf8Bytes(ledger, JsonSerializerOptions.Web).Length);
        long before = JsonSerializer.SerializeToUtf8Bytes(state[key], JsonSerializerOptions.Web).Length;
        var attempted = new List<long>();
        client.TrySaveStateAsync("statestore", Arg.Any<string>(), Arg.Any<ProjectionDeliveryRetryLedger>(), Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
        {
            var replacement = call.ArgAt<ProjectionDeliveryRetryLedger>(2);
            long bytes = JsonSerializer.SerializeToUtf8Bytes(replacement, JsonSerializerOptions.Web).Length;
            bytes.ShouldBeLessThanOrEqualTo(512L + 4096L * replacement.Items.Count);
            attempted.Add(bytes);
            if (attempted.Count <= refused) return false;
            state[call.ArgAt<string>(1)] = replacement;
            return true;
        });
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await scheduler.GetDueAsync(due.AddSeconds(1), 64).ConfigureAwait(true)).Count.ShouldBe(64);
        ProjectionDeliveryRetryWorkItem first = (await scheduler.TryAcquireAsync(original, "first-owner", due,
            TimeSpan.FromMinutes(1)).ConfigureAwait(true)).ShouldNotBeNull();
        long leased = JsonSerializer.SerializeToUtf8Bytes(state[key], JsonSerializerOptions.Web).Length;
        (await scheduler.TryUpdateAsync(first with { Attempt = 1 }).ConfigureAwait(true)).ShouldBeTrue();
        long updated = JsonSerializer.SerializeToUtf8Bytes(state[key], JsonSerializerOptions.Web).Length;
        ProjectionDeliveryRetryWorkItem second = (await scheduler.TryAcquireAsync(state[key].Items[0], "second-owner", due,
            TimeSpan.FromMinutes(1)).ConfigureAwait(true)).ShouldNotBeNull();
        long leasedAgain = JsonSerializer.SerializeToUtf8Bytes(state[key], JsonSerializerOptions.Web).Length;
        (await scheduler.TryDeleteAsync(second).ConfigureAwait(true)).ShouldBeTrue();
        long final = JsonSerializer.SerializeToUtf8Bytes(state[key], JsonSerializerOptions.Web).Length;
        cost[ProjectionDeliveryCost.SchedulerShardReads].ShouldBe(68 + refused);
        cost[ProjectionDeliveryCost.SchedulerReadBytes].ShouldBe(scan + before * (1 + refused) + leased + updated + leasedAgain);
        cost[ProjectionDeliveryCost.SchedulerWriteAttempts].ShouldBe(4 + refused);
        cost[ProjectionDeliveryCost.SchedulerWriteBytes].ShouldBe(attempted.Sum());
        attempted.Sum().ShouldBe(leased * (1 + refused) + updated + leasedAgain + final);
        cost[ProjectionDeliveryCost.SchedulerCasAttempts].ShouldBe(8 + refused);
        state.Values.SelectMany(static ledger => ledger.Items).OrderBy(static item => item.WorkId)
            .ShouldBe(unrelated.OrderBy(static item => item.WorkId));
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionSchedulerMutationCost " + JsonSerializer.Serialize(new
        { uneven, refused, occupancy = uneven ? 128 : 64, scanBytes = scan, before, leased, updated, leasedAgain, final,
            attemptedWriteBytes = attempted, cost = Enum.GetValues<ProjectionDeliveryCost>()
                .ToDictionary(static category => category.ToString(), category => cost[category]) }));
    }

    [Fact]
    public async Task ColdLegacyMigrationChargesMarkerLegacyRowsDeletionAndProtocolSaveBeforeTheWarmScan()
    {
        ProjectionDeliveryRetryWorkItem original = CreateWorkItem("cold-work", "cold-aggregate");
        var legacy = new ProjectionDeliveryRetryLedger([original]);
        var state = new Dictionary<string, ProjectionDeliveryRetryLedger>(StringComparer.Ordinal)
            { ["projection-delivery-retry:ledger:v1"] = legacy };
        using DaprClient client = CreateStatefulDaprClient(state);
        string? marker = null;
        client.GetStateAsync<string>("statestore", "projection-delivery-retry:protocol",
            consistencyMode: Arg.Any<ConsistencyMode?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => marker!);
        client.SaveStateAsync("statestore", "projection-delivery-retry:protocol", Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call => { marker = call.ArgAt<string>(2); return Task.CompletedTask; });
        DaprProjectionDeliveryRetryScheduler scheduler = CreateScheduler(client, enableLegacyMigration: true);
        using ProjectionDeliveryCostScope cold = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await scheduler.GetDueAsync(DateTimeOffset.MaxValue, 64).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe(original);
        cold[ProjectionDeliveryCost.SchedulerMigrationReads].ShouldBe(2);
        cold[ProjectionDeliveryCost.SchedulerMigrationReadBytes].ShouldBe(JsonSerializer.SerializeToUtf8Bytes(legacy, JsonSerializerOptions.Web).Length);
        cold[ProjectionDeliveryCost.SchedulerMigrationEntries].ShouldBe(1);
        cold[ProjectionDeliveryCost.SchedulerMigrationWrites].ShouldBe(2);
        cold[ProjectionDeliveryCost.SchedulerMigrationWriteBytes].ShouldBe(JsonSerializer.SerializeToUtf8Bytes(marker, JsonSerializerOptions.Web).Length);
        cold[ProjectionDeliveryCost.TransactionAttempts].ShouldBe(1);
        cold[ProjectionDeliveryCost.StateRpcAttempts].ShouldBe(7);
        marker.ShouldBe("v2-ready:v1-writers-quiesced");
        state.ShouldNotContainKey("projection-delivery-retry:ledger:v1");
        state.Values.SelectMany(static ledger => ledger.Items).ShouldHaveSingleItem().ShouldBe(original);
        using ProjectionDeliveryCostScope warm = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await scheduler.GetDueAsync(DateTimeOffset.MaxValue, 64).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe(original);
        warm[ProjectionDeliveryCost.SchedulerMigrationReads].ShouldBe(0);
        warm[ProjectionDeliveryCost.SchedulerMigrationWrites].ShouldBe(0);
        warm[ProjectionDeliveryCost.SchedulerShardReads].ShouldBe(64);
        warm[ProjectionDeliveryCost.StateRpcAttempts].ShouldBe(1);
    }

    private static Dictionary<string, ProjectionDeliveryRetryLedger> OccupiedLedgers(bool uneven, DateTimeOffset due)
    {
        List<ProjectionDeliveryRetryWorkItem>[] shards = [.. Enumerable.Range(0, 64).Select(static _ => new List<ProjectionDeliveryRetryWorkItem>())];
        int remaining = 4096 + (uneven ? 64 : 0);
        for (int candidate = 0; remaining > 0; candidate++)
        {
            string id = "occupied-" + candidate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int shard = SHA256.HashData(Encoding.UTF8.GetBytes(id))[0] % 64;
            if (shards[shard].Count >= (uneven && shard == 0 ? 128 : 64)) continue;
            ProjectionDeliveryRetryWorkItem item = CreateWorkItem(id, "aggregate-" + id) with { NextDueUtc = due };
            JsonSerializer.SerializeToUtf8Bytes(item, JsonSerializerOptions.Web).Length.ShouldBeLessThanOrEqualTo(4096);
            shards[shard].Add(item);
            remaining--;
        }
        return Enumerable.Range(0, 64).ToDictionary(shard => "projection-delivery-retry:ledger:v2:"
            + shard.ToString("x2", System.Globalization.CultureInfo.InvariantCulture), shard => new ProjectionDeliveryRetryLedger(shards[shard]), StringComparer.Ordinal);
    }
}
