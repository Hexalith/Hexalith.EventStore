using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed partial class SharedProjectionBoundedIdentityTests
{
    private static readonly SharedProjectionScope s_inheritedScope = s_scope with { CopyOnWrite = true };

    [Fact]
    public async Task OmittedOwnerRetainsExactCapturedReplayAfterAnotherOwnerPromotes()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_inheritedScope, "writer");
        _ = await coordinator.BeginAsync(s_inheritedScope, "first", "inventory-first",
            new Dictionary<string, long> { ["owner-a"] = 2 });
        var reference = await coordinator.CapturedIdentities.BeginAsync(s_inheritedScope, "first", "owner-a", 1);
        await coordinator.CapturedIdentities.AppendAsync(s_inheritedScope, "first", reference, 1, [2], Digest(1), [string.Empty]);
        reference = await coordinator.CapturedIdentities.SealAsync(s_inheritedScope, "first", reference);
        _ = await coordinator.StageStreamingAsync(s_inheritedScope, "first", InheritedWrites("view", 1));
        await coordinator.CommitAsync(s_inheritedScope, "first", requireOwnerSequenceHandoff: true);
        await coordinator.SeedCapturedOwnerIdentityAsync(s_inheritedScope, "first", "owner-a", 1, reference);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_inheritedScope, "first");
        _ = await coordinator.CatchUpAsync(s_inheritedScope, Fold);
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        byte[] before = store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"))!;

        _ = await coordinator.BeginAsync(s_inheritedScope, "other", "inventory-other",
            new Dictionary<string, long> { ["owner-b"] = 3 });
        _ = await coordinator.StageStreamingAsync(s_inheritedScope, "other", InheritedWrites("other-view", 1));
        await coordinator.CommitAsync(s_inheritedScope, "other");
        _ = await coordinator.CatchUpAsync(s_inheritedScope, Fold);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a")).ShouldBe(before);
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_inheritedScope.StoreName, s_inheritedScope.StateKey)).Value!;
        state.CaptureHighWatermarks.Keys.ShouldBe(["owner-b"]);

        lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!.CapturedIdentityReference.ShouldBe(reference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedOmissionPreservesPagedAndInlineOwnerProofAfterOrdinaryGrowth(bool legacyInline)
    {
        var (store, coordinator, reference) = await BeginInheritedOwnerAsync(65, legacyInline);
        var oldLease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, oldLease, Delivery(66))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_inheritedScope, Fold)).ShouldBe(1);
        byte[] before = store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"))!;

        foreach (string operation in new[] { "other-first", "other-second", "other-third" })
        {
            await PromoteOtherOwnerAsync(coordinator, operation);
            coordinator = new SharedProjectionEpochCoordinator(store, store);
            var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
            (await coordinator.JournalAsync(s_inheritedScope, oldLease, Delivery(1)))
                .ShouldBe(SharedProjectionJournalResult.StaleLease);
            foreach (long sequence in new long[] { 1, 64, 65 })
                (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(sequence)))
                    .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
            (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value!["count"]!.GetValue<long>().ShouldBe(66);
            store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a")).ShouldBe(before);
            var checkpoint = (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!;
            checkpoint.OwnerSequenceNumber.ShouldBe(66);
            checkpoint.CapturedIdentityReference.ShouldBe(reference);
            (await coordinator.GetStatusAsync(s_inheritedScope)).PendingDeliveryCount.ShouldBe(0);
        }

        // A later ordinary ordinal is outside the sealed captured prefix even though
        // the retained checkpoint has advanced. Its missing proof cannot be acknowledged.
        var currentLease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, currentLease, Delivery(66)))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("position")]
    [InlineData("sequence")]
    public async Task InheritedOwnerRejectsChangedHistoricalIdentity(string changed)
    {
        var (store, coordinator, _) = await BeginInheritedOwnerAsync(65);
        await PromoteOtherOwnerAsync(coordinator, "other");
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        byte[] checkpoint = store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"))!;
        SharedProjectionDelivery altered = changed switch
        {
            "payload" => Delivery(1) with { CanonicalPayload = [99] },
            "position" => Delivery(1) with { EnvelopePosition = 3 },
            _ => Delivery(1) with { OwnerSequenceNumber = 2 },
        };

        (await coordinator.JournalAsync(s_inheritedScope, lease, altered)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        var conflict = (await coordinator.GetStatusAsync(s_inheritedScope)).DeliveryConflicts.ShouldHaveSingleItem();
        conflict.SourceStream.ShouldBe("owner-a");
        conflict.EnvelopePosition.ShouldBe(altered.EnvelopePosition);
        conflict.IsResolved.ShouldBeFalse();
        store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a")).ShouldBe(checkpoint);
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
    }

    [Theory]
    [InlineData("missing-inventory")]
    [InlineData("retired-inventory")]
    [InlineData("missing-leaf")]
    [InlineData("corrupt-root")]
    [InlineData("wrong-source")]
    public async Task InheritedOwnerMissingOrCorruptProofFailsClosed(string corruption)
    {
        var (store, coordinator, captured) = await BeginInheritedOwnerAsync(65);
        var reference = captured!;
        await PromoteOtherOwnerAsync(coordinator, "other");
        string inventoryKey = SharedProjectionCapturedIdentityStore.InventoryKey(reference);
        if (corruption == "missing-inventory")
        {
            var entry = await store.GetAsync<SharedProjectionCapturedIdentityInventory>(s_inheritedScope.StoreName, inventoryKey);
            (await store.TryEraseAsync(s_inheritedScope.StoreName, inventoryKey, entry.ETag!)).ShouldBeTrue();
        }
        else if (corruption == "retired-inventory")
        {
            var inventory = (await store.GetAsync<SharedProjectionCapturedIdentityInventory>(s_inheritedScope.StoreName, inventoryKey)).Value!;
            store.SeedRaw(s_inheritedScope.StoreName, inventoryKey, inventory with { Retired = true });
        }
        else if (corruption == "missing-leaf")
        {
            var leaf = await coordinator.CapturedIdentities.ReadPageAsync(s_inheritedScope, reference, 1);
            string key = SharedProjectionCapturedIdentityStore.NodeKey(reference,
                Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(leaf))));
            var entry = await store.GetAsync<SharedProjectionCapturedIdentityPage>(s_inheritedScope.StoreName, key);
            (await store.TryEraseAsync(s_inheritedScope.StoreName, key, entry.ETag!)).ShouldBeTrue();
        }
        else if (corruption == "corrupt-root")
        {
            string key = SharedProjectionCapturedIdentityStore.NodeKey(reference, reference.RootDigest);
            var page = (await store.GetAsync<SharedProjectionCapturedIdentityPage>(s_inheritedScope.StoreName, key)).Value!;
            store.SeedRaw(s_inheritedScope.StoreName, key, page with { LastPosition = page.LastPosition - 1 });
        }
        else
        {
            var owner = (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!;
            store.SeedRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"), owner with
            {
                CapturedIdentityReference = reference with { SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("owner-b"))) },
            });
        }
        byte[] checkpoint = store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"))!;
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        var before = await coordinator.GetStatusAsync(s_inheritedScope);

        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1)));
        store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a")).ShouldBe(checkpoint);
        var after = await coordinator.GetStatusAsync(s_inheritedScope);
        after.Epoch.ShouldBe(before.Epoch);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.PendingDeliveryCount.ShouldBe(0);
        after.DeliveryConflicts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritedOwnerCheckpointWithoutSelectedProofCannotAcknowledgeHistory(bool futureReference)
    {
        var (store, coordinator, captured) = await BeginInheritedOwnerAsync(1);
        var reference = captured!;
        await PromoteOtherOwnerAsync(coordinator, "other");
        var owner = (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!;
        if (futureReference)
        {
            // Retain matching sealed inventory and authenticated nodes so that only
            // the selected-generation fence can reject this unselected reference.
            long futureEpoch = (await coordinator.GetStatusAsync(s_inheritedScope)).Epoch + 1;
            var future = reference with { Epoch = futureEpoch };
            store.SeedRaw(s_inheritedScope.StoreName, SharedProjectionCapturedIdentityStore.InventoryKey(reference),
                new SharedProjectionCapturedIdentityInventory(future));
            owner = owner with { CapturedIdentityReference = future };
        }
        else owner = owner with { CapturedIdentityReference = null };
        store.SeedRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"), owner);
        byte[] checkpoint = store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a"))!;
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");

        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        store.SnapshotRaw(s_inheritedScope.StoreName, s_inheritedScope.CheckpointKey("owner-a")).ShouldBe(checkpoint);
        (await coordinator.GetStatusAsync(s_inheritedScope)).PendingDeliveryCount.ShouldBe(0);
    }

    [Fact]
    public async Task InheritedOwnerKeepsExactNextSequenceAndGapChecks()
    {
        var (_, coordinator, _) = await BeginInheritedOwnerAsync(65);
        await PromoteOtherOwnerAsync(coordinator, "other");
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(67))).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(66))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_inheritedScope, Fold)).ShouldBe(1);
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(67))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_inheritedScope, Fold)).ShouldBe(1);
        (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(67);
        (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value!["count"]!.GetValue<long>().ShouldBe(67);
        (await coordinator.GetStatusAsync(s_inheritedScope)).DeliveryConflicts.ShouldHaveSingleItem().IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task ReplacementGenerationDoesNotProveAnOmittedOwnerWithoutCopyOnWriteInheritance()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "first", 1);
        var reference = await CaptureAsync(coordinator, "first", 1);
        await PromoteAsync(coordinator, "first", 1, reference);
        (await coordinator.ReadAsync<JsonObject>(s_scope, "view")).Value.ShouldNotBeNull();
        _ = await coordinator.BeginAsync(s_scope, "other", "inventory-other",
            new Dictionary<string, long> { ["owner-b"] = 3 });
        await coordinator.StageAsync(s_scope, "other", [ReadModelBatchOperation.Write("other-view",
            new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "other");
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        (await coordinator.ReadAsync<JsonObject>(s_scope, "view")).Value.ShouldBeNull();
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(reference);
        var lease = await coordinator.RefreshLeaseAsync(s_scope, "writer");

        (await coordinator.JournalAsync(s_scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyOnWriteMigrationCannotResurrectOwnerProofDroppedByAnEarlierReplacement(bool legacyInline)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "first", 1);
        SharedProjectionCapturedIdentityReference? reference = null;
        if (legacyInline)
        {
            await coordinator.StageAsync(s_scope, "first", [ReadModelBatchOperation.Write("view",
                new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite)]);
            await coordinator.CommitAsync(s_scope, "first", requireOwnerSequenceHandoff: true);
            await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "first", "owner-a", 1, capturedEventDigests: Digest(1));
            await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "first");
            _ = await coordinator.CatchUpAsync(s_scope, Fold);
        }
        else
        {
            reference = await CaptureAsync(coordinator, "first", 1);
            await PromoteAsync(coordinator, "first", 1, reference);
        }
        (await coordinator.ReadAsync<JsonObject>(s_scope, "view")).Value.ShouldNotBeNull();
        byte[] checkpoint = store.SnapshotRaw(s_scope.StoreName, s_scope.CheckpointKey("owner-a"))!;

        _ = await coordinator.BeginAsync(s_scope, "replacement", "inventory-replacement",
            new Dictionary<string, long> { ["owner-b"] = 3 });
        await coordinator.StageAsync(s_scope, "replacement", [ReadModelBatchOperation.Write("other-view",
            new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "replacement");
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        (await coordinator.ReadAsync<JsonObject>(s_scope, "view")).Value.ShouldBeNull();

        // The migration keeps the same durable namespace and inherits generation 2,
        // which had already discarded A's view while retaining its checkpoint.
        s_inheritedScope.ComputeHash().ShouldBe(s_scope.ComputeHash());
        await PromoteOtherOwnerAsync(coordinator, "migration");
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_inheritedScope.StoreName, s_inheritedScope.StateKey)).Value!;
        state.CopyOnWriteAnchorGeneration.ShouldBe(2);
        (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value.ShouldBeNull();
        store.SnapshotRaw(s_scope.StoreName, s_scope.CheckpointKey("owner-a")).ShouldBe(checkpoint);
        (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!.CapturedIdentityReference.ShouldBe(reference);
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");

        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
    }

    [Fact]
    public async Task CopyOnWriteMigrationRetainsPagedOwnerProofFromItsSelectedAnchorSnapshot()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "first", 1);
        var first = await CaptureAsync(coordinator, "first", 1);
        await PromoteAsync(coordinator, "first", 1, first);
        _ = await coordinator.BeginAsync(s_scope, "anchor", "inventory-anchor",
            new Dictionary<string, long> { ["owner-a"] = 2 });
        var selected = await CaptureAsync(coordinator, "anchor", 1);
        await PromoteAsync(coordinator, "anchor", 1, selected);
        selected.Epoch.ShouldBe(2);

        await PromoteOtherOwnerAsync(coordinator, "migration");
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_inheritedScope.StoreName, s_inheritedScope.StateKey)).Value!;
        state.CopyOnWriteAnchorGeneration.ShouldBe(selected.Epoch);
        (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!.CapturedIdentityReference.ShouldBe(selected);
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");

        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await coordinator.GetStatusAsync(s_inheritedScope)).PendingDeliveryCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritedOwnerProofRemainsSelectedAfterALaterRebuildAborts(bool captureInheritedOwner)
    {
        var (store, coordinator, reference) = await BeginInheritedOwnerAsync(65);
        await PromoteOtherOwnerAsync(coordinator, "other");
        var capture = new Dictionary<string, long> { ["owner-b"] = 3 };
        if (captureInheritedOwner) capture["owner-a"] = 130;
        _ = await coordinator.BeginAsync(s_inheritedScope, "aborted", "inventory-aborted",
            capture);
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Backpressure);
        await coordinator.AbortAsync(s_inheritedScope, "aborted", Fold, deferOpen: true);
        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Backpressure);
        await coordinator.CompleteAbortAsync(s_inheritedScope, "aborted");
        var status = await coordinator.GetStatusAsync(s_inheritedScope);
        status.Epoch.ShouldBeGreaterThan(status.ActiveGeneration);
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_inheritedScope.StoreName, s_inheritedScope.StateKey)).Value!;
        state.CaptureHighWatermarks.ShouldBeEmpty();
        state.CaptureChunks.ShouldBeNull();

        (await coordinator.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await coordinator.GetCheckpointAsync(s_inheritedScope, "owner-a"))!.CapturedIdentityReference.ShouldBe(reference);
        (await coordinator.ReadAsync<JsonObject>(s_inheritedScope, "view")).Value!["count"]!.GetValue<int>().ShouldBe(65);
    }

    [Fact]
    public async Task EpochChangeDuringInheritedIdentityReadRechecksTheLeaseBeforeAcknowledging()
    {
        var (store, coordinator, _) = await BeginInheritedOwnerAsync(65);
        await PromoteOtherOwnerAsync(coordinator, "other");
        var lease = await coordinator.RefreshLeaseAsync(s_inheritedScope, "writer");
        var intercepted = Substitute.For<IReadModelStore>();
        intercepted.GetAsync<SharedProjectionEpochState>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.GetAsync<SharedProjectionEpochState>(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        intercepted.GetAsync<SharedProjectionCheckpoint>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.GetAsync<SharedProjectionCheckpoint>(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        intercepted.GetAsync<SharedProjectionDeliveryReceipt>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.GetAsync<SharedProjectionDeliveryReceipt>(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        intercepted.GetAsync<SharedProjectionCapturedIdentityInventory>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.GetAsync<SharedProjectionCapturedIdentityInventory>(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        bool changed = false;
        intercepted.GetAsync<SharedProjectionCapturedIdentityPage>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var page = await store.GetAsync<SharedProjectionCapturedIdentityPage>(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2));
                if (!changed)
                {
                    changed = true;
                    _ = await coordinator.BeginAsync(s_inheritedScope, "racing", "inventory-racing", new Dictionary<string, long>());
                }
                return page;
            });
        var reading = new SharedProjectionEpochCoordinator(intercepted, store);

        (await reading.JournalAsync(s_inheritedScope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.StaleLease);
        changed.ShouldBeTrue();
        (await coordinator.GetStatusAsync(s_inheritedScope)).PendingDeliveryCount.ShouldBe(0);
    }

    private static async Task<(InMemoryReadModelStore Store, SharedProjectionEpochCoordinator Coordinator,
        SharedProjectionCapturedIdentityReference? Reference)> BeginInheritedOwnerAsync(int count, bool legacyInline = false)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_inheritedScope, "writer");
        _ = await coordinator.BeginAsync(s_inheritedScope, "first", "inventory-first",
            new Dictionary<string, long> { ["owner-a"] = count * 2L });
        SharedProjectionCapturedIdentityReference? reference = null;
        if (!legacyInline)
        {
            reference = await coordinator.CapturedIdentities.BeginAsync(s_inheritedScope, "first", "owner-a", count);
            for (int first = 1; first <= count; first += SharedProjectionCapturedIdentityStore.EntriesPerPage)
            {
                int take = Math.Min(SharedProjectionCapturedIdentityStore.EntriesPerPage, count - first + 1);
                await coordinator.CapturedIdentities.AppendAsync(s_inheritedScope, "first", reference, first,
                    Enumerable.Range(first, take).Select(sequence => sequence * 2L).ToArray(),
                    Enumerable.Range(first, take).SelectMany(sequence => Digest(sequence)).ToArray(),
                    Enumerable.Repeat(string.Empty, take).ToArray());
            }
            reference = await coordinator.CapturedIdentities.SealAsync(s_inheritedScope, "first", reference);
        }
        _ = await coordinator.StageStreamingAsync(s_inheritedScope, "first", InheritedWrites("view", count));
        await coordinator.CommitAsync(s_inheritedScope, "first", requireOwnerSequenceHandoff: true);
        if (reference is not null)
            await coordinator.SeedCapturedOwnerIdentityAsync(s_inheritedScope, "first", "owner-a", count, reference);
        else
            await coordinator.SeedCapturedOwnerSequenceAsync(s_inheritedScope, "first", "owner-a", count,
                capturedEventDigests: Enumerable.Range(1, count).SelectMany(sequence => Digest(sequence)).ToArray());
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_inheritedScope, "first");
        _ = await coordinator.CatchUpAsync(s_inheritedScope, Fold);
        return (store, coordinator, reference);
    }

    private static async Task PromoteOtherOwnerAsync(SharedProjectionEpochCoordinator coordinator, string operation)
    {
        _ = await coordinator.BeginAsync(s_inheritedScope, operation, "inventory-" + operation,
            new Dictionary<string, long> { ["owner-b"] = 3 });
        _ = await coordinator.StageStreamingAsync(s_inheritedScope, operation, InheritedWrites("other-view", 1));
        await coordinator.CommitAsync(s_inheritedScope, operation);
        _ = await coordinator.CatchUpAsync(s_inheritedScope, Fold);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> InheritedWrites(string key, int count)
    {
        yield return ReadModelBatchOperation.Write(key, new JsonObject { ["count"] = count }, ReadModelBatchConcurrency.LastWrite);
        await Task.Yield();
    }
}
