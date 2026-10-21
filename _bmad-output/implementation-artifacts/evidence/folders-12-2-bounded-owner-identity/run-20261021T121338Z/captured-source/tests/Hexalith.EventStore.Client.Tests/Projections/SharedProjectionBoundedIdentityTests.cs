using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionBoundedIdentityTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "tenant-a", "widget", "bounded", ["writer"]);

    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task CompleteOriginalOwnerUsesBoundedNodesAndRetainsHistoricalProofAfterGrowth(int count)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "large", count);
        var reference = await CaptureAsync(coordinator, "large", count);
        reference.Count.ShouldBe(count);
        reference.Height.ShouldBeGreaterThan(0);
        JsonSerializer.SerializeToUtf8Bytes(reference).Length.ShouldBeLessThan(1024);
        await PromoteAsync(coordinator, "large", count, reference);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RefreshLeaseAsync(s_scope, "writer");
        foreach (long sequence in new long[] { 1, 64, 65, count / 2, count })
            (await coordinator.JournalAsync(s_scope, lease, Delivery(sequence))).ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
        (await coordinator.JournalAsync(s_scope, lease, Delivery(count + 1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(reference);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        (await coordinator.ReadAsync<JsonObject>(s_scope, "view")).Value!["count"]!.GetValue<long>().ShouldBe(count + 1);
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1) with { CanonicalPayload = [99] })).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await coordinator.GetStatusAsync(s_scope)).DeliveryConflicts.ShouldNotBeEmpty();
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(count + 1);
        for (long sequence = 1; sequence <= count; sequence += SharedProjectionCapturedIdentityStore.EntriesPerPage)
        {
            var page = await coordinator.CapturedIdentities.ReadPageAsync(s_scope, reference, sequence);
            page.Count.ShouldBeLessThanOrEqualTo(128);
            JsonSerializer.SerializeToUtf8Bytes(page).Length.ShouldBeLessThan(16_384);
            store.SnapshotRaw(s_scope.StoreName, SharedProjectionCapturedIdentityStore.NodeKey(reference,
                Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)))))!.Length.ShouldBeLessThan(16_384);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(129)]
    public async Task EveryRetainedPrefixPageIncludingPartialLeafIsComparedBeforePromotion(int changed)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "first", 129);
        var first = await CaptureAsync(coordinator, "first", 129);
        await PromoteAsync(coordinator, "first", 129, first);
        _ = await coordinator.BeginAsync(s_scope, "next", "inventory-next", new Dictionary<string, long> { ["owner-a"] = 260 });
        var next = await CaptureAsync(coordinator, "next", 130, changed);
        (await coordinator.CanPromoteCapturedOwnerIdentityAsync(s_scope, "next", "owner-a", 130, next)).ShouldBeFalse();
        (await coordinator.GetStatusAsync(s_scope)).ActiveGeneration.ShouldBe(first.Epoch);
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(first);
    }

    [Fact]
    public async Task LegacyInlinePrefixUpgradesAndMalformedInlineFailsClosed()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "upgrade", 130);
        var reference = await CaptureAsync(coordinator, "upgrade", 130);
        byte[] inline = Enumerable.Range(1, 65).SelectMany(sequence => Digest(sequence)).ToArray();
        var checkpoint = new SharedProjectionCheckpoint(130, [1], 65, inline);
        (await coordinator.CapturedIdentities.MatchesPrefixAsync(s_scope, reference, checkpoint)).ShouldBeTrue();
        inline[^1] ^= 1;
        (await coordinator.CapturedIdentities.MatchesPrefixAsync(s_scope, reference, checkpoint)).ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.MatchesPrefixAsync(
            s_scope, reference, checkpoint with { CapturedEventDigests = new byte[33] }));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.MatchesPrefixAsync(
            s_scope, reference, checkpoint with { OwnerSequenceNumber = 1, CapturedEventDigests = new byte[64] }));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.ReadPageAsync(
            s_scope with { TenantId = "other-tenant" }, reference, 1));
    }

    [Fact]
    public async Task DistantMessageDuplicatesMissingAndCorruptNodesFailClosed()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "duplicate", 130);
        var reserved = await coordinator.CapturedIdentities.BeginAsync(s_scope, "duplicate", "owner-a", 130);
        await AppendAsync(coordinator, "duplicate", reserved, 1, 64);
        await Should.ThrowAsync<InvalidOperationException>(() => AppendAsync(coordinator, "duplicate", reserved, 65, 64, repeatedMessage: 1));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.SealAsync(s_scope, "duplicate", reserved));
        await AppendAsync(coordinator, "duplicate", reserved, 65, 64);
        await AppendAsync(coordinator, "duplicate", reserved, 129, 2);
        var reference = await coordinator.CapturedIdentities.SealAsync(s_scope, "duplicate", reserved);
        string rootKey = SharedProjectionCapturedIdentityStore.NodeKey(reference, reference.RootDigest);
        var root = (await store.GetAsync<SharedProjectionCapturedIdentityPage>(s_scope.StoreName, rootKey)).Value!;
        store.SeedRaw(s_scope.StoreName, rootKey, root with { LastPosition = 259 });
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.ReadPageAsync(s_scope, reference, 1));
        store.SeedRaw(s_scope.StoreName, rootKey, root);
        var first = (await coordinator.CapturedIdentities.ReadPageAsync(s_scope, reference, 1));
        string firstKey = SharedProjectionCapturedIdentityStore.NodeKey(reference, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(first))));
        var entry = await store.GetAsync<SharedProjectionCapturedIdentityPage>(s_scope.StoreName, firstKey);
        (await store.TryEraseAsync(s_scope.StoreName, firstKey, entry.ETag!)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.ValidateCompleteAsync(s_scope, reference));
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1)]
    public async Task AbortRetiresPreparedPagesWithoutAllowingDelayedInstallers(ReadModelBatchPhase phase, int ordinal)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "retire", 64);
        var reference = await coordinator.CapturedIdentities.BeginAsync(s_scope, "retire", "owner-a", 64);
        store.BatchFaultHook = (current, index, _) => current == phase && index == ordinal
            ? throw new InvalidOperationException("interrupted identity process") : Task.CompletedTask;
        await Should.ThrowAsync<InvalidOperationException>(() => AppendAsync(coordinator, "retire", reference, 1, 64));
        store.BatchFaultHook = null;
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        await coordinator.AbortAsync(s_scope, "retire", Fold, deferOpen: true);
        await coordinator.CapturedIdentities.RetireAsync(s_scope, reference);
        await coordinator.CompleteAbortAsync(s_scope, "retire");
        await coordinator.CapturedIdentities.RetireAsync(s_scope, reference);
        (await store.GetAsync<SharedProjectionCapturedIdentityInventory>(s_scope.StoreName,
            SharedProjectionCapturedIdentityStore.InventoryKey(reference))).Value!.Retired.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => AppendAsync(coordinator, "retire", reference, 1, 64));
        (await coordinator.GetStatusAsync(s_scope)).ActiveGeneration.ShouldBe(0);
    }

    [Fact]
    public async Task InheritedOwnerProofCannotBeRetiredByAnOlderGeneration()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "first", 65);
        var first = await CaptureAsync(coordinator, "first", 65);
        await PromoteAsync(coordinator, "first", 65, first);
        _ = await coordinator.BeginAsync(s_scope, "later", "inventory-later", new Dictionary<string, long> { ["owner-a"] = 130 });
        await coordinator.StageAsync(s_scope, "later", [ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = 65 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "later");
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CapturedIdentities.RetireAsync(s_scope, first));
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(first);
        await coordinator.CapturedIdentities.ValidateCompleteAsync(s_scope, first);
    }

    [Fact]
    public async Task MultipleOriginalOwnersKeepDistinctProofAndCoveredPendingJournalCatchUp()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.BeginAsync(s_scope, "owners", "inventory", new Dictionary<string, long>
            { ["owner-a"] = 130, ["owner-b"] = 130 }, allowCoveredJournal: true);
        var references = new Dictionary<string, SharedProjectionCapturedIdentityReference>();
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            var reserved = await coordinator.CapturedIdentities.BeginAsync(s_scope, "owners", owner, 65);
            for (int first = 1; first <= 65; first += 64)
            {
                int count = Math.Min(64, 66 - first);
                await coordinator.CapturedIdentities.AppendAsync(s_scope, "owners", reserved, first,
                    Enumerable.Range(first, count).Select(sequence => sequence * 2L).ToArray(),
                    Enumerable.Range(first, count).SelectMany(sequence => SharedProjectionCapturedEventIdentity.Compute(owner, sequence * 2, sequence, [7])).ToArray(),
                    Enumerable.Range(first, count).Select(sequence => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("message-" + sequence)))).ToArray());
            }
            references[owner] = await coordinator.CapturedIdentities.SealAsync(s_scope, "owners", reserved);
        }
        references["owner-a"].NamespaceHash.ShouldNotBe(references["owner-b"].NamespaceHash);
        (await coordinator.CanPromoteCapturedJournalOwnerIdentityAsync(s_scope, "owners", "owner-a", 65, references["owner-a"])).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CanPromoteCapturedJournalOwnerIdentityAsync(s_scope, "owners", "owner-a", 65, references["owner-b"]));
        await coordinator.StageAsync(s_scope, "owners", [ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = 65 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "owners", requireOwnerSequenceHandoff: true);
        foreach (string owner in new[] { "owner-a", "owner-b" })
            await coordinator.SeedCapturedOwnerIdentityAsync(s_scope, "owners", owner, 65, references[owner]);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, "owners");
        (await coordinator.CatchUpAsync(s_scope, (_, _, _) => throw new InvalidOperationException("Covered original history must not fold twice."))).ShouldBe(1);
        (await coordinator.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            (await coordinator.GetCheckpointAsync(s_scope, owner))!.CapturedIdentityReference.ShouldBe(references[owner]);
            var current = await coordinator.RefreshLeaseAsync(s_scope, "writer");
            (await coordinator.JournalAsync(s_scope, current, Delivery(1) with { SourceStream = owner }))
                .ShouldBe(owner == "owner-a" ? SharedProjectionJournalResult.AlreadyJournaled : SharedProjectionJournalResult.AlreadyCaptured);
        }
    }

    [Fact]
    public async Task ConcurrentEpochChangeCannotBeOverwrittenByAStaleCheckpointSeed()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "race-seed", 65);
        var reference = await CaptureAsync(coordinator, "race-seed", 65);
        await coordinator.StageAsync(s_scope, "race-seed", [ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = 65 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "race-seed", requireOwnerSequenceHandoff: true);
        byte[]? changedFence = null;
        store.BatchFaultHook = async (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.AfterInstallOperation && ordinal == 0 && changedFence is null)
            {
                var current = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)).Value!;
                store.SeedRaw(s_scope.StoreName, s_scope.StateKey, current with { OperationId = "concurrent-operation" });
                changedFence = store.SnapshotRaw(s_scope.StoreName, s_scope.StateKey);
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.SeedCapturedOwnerIdentityAsync(s_scope, "race-seed", "owner-a", 65, reference));
        changedFence.ShouldNotBeNull();
        store.SnapshotRaw(s_scope.StoreName, s_scope.StateKey).ShouldBe(changedFence);
        (await coordinator.GetCheckpointAsync(s_scope, "owner-a")).ShouldBeNull();
    }

    [Fact]
    public async Task OrdinaryCheckpointMigrationPreservesLegacyInlineEpochProof()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        var legacy = new SharedProjectionCheckpoint(2, JsonSerializer.SerializeToUtf8Bytes(2L), 1, Digest(1));
        store.SeedRaw(s_scope.StoreName, s_scope.StateKey, entry.Value! with
        {
            ConsumerCheckpoints = new Dictionary<string, SharedProjectionCheckpoint> { ["owner-a"] = legacy }
        });
        (await coordinator.JournalAsync(s_scope, lease, Delivery(2))).ShouldBe(SharedProjectionJournalResult.Journaled);
        var migrated = (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!;
        migrated.OwnerSequenceNumber.ShouldBe(2);
        migrated.CapturedEventDigests.ShouldBe(legacy.CapturedEventDigests);
    }

    [Fact]
    public async Task LegacySeedRemainsCallableAfterDefaultCommitWithoutHandoffOptIn()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = await BeginAsync(store, "legacy-default-commit", 1);
        await coordinator.StageAsync(s_scope, "legacy-default-commit", [ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, "legacy-default-commit");
        await coordinator.SeedCapturedOwnerSequenceAsync(s_scope, "legacy-default-commit", "owner-a", 1, capturedEventDigests: Digest(1));
        var checkpoint = (await coordinator.GetCheckpointAsync(s_scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(1);
        checkpoint.CapturedEventDigests.ShouldBe(Digest(1));
        (await coordinator.JournalAsync(s_scope, await coordinator.RefreshLeaseAsync(s_scope, "writer"), Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
    }

    private static async Task<SharedProjectionEpochCoordinator> BeginAsync(InMemoryReadModelStore store, string operation, int count)
    {
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        _ = await coordinator.BeginAsync(s_scope, operation, "inventory-" + operation, new Dictionary<string, long> { ["owner-a"] = count * 2L });
        return coordinator;
    }

    private static async Task<SharedProjectionCapturedIdentityReference> CaptureAsync(SharedProjectionEpochCoordinator coordinator, string operation, int count, int changed = -1)
    {
        var reference = await coordinator.CapturedIdentities.BeginAsync(s_scope, operation, "owner-a", count);
        for (int first = 1; first <= count; first += SharedProjectionCapturedIdentityStore.EntriesPerPage)
            await AppendAsync(coordinator, operation, reference, first, Math.Min(SharedProjectionCapturedIdentityStore.EntriesPerPage, count - first + 1), changed);
        return await coordinator.CapturedIdentities.SealAsync(s_scope, operation, reference);
    }

    private static Task AppendAsync(SharedProjectionEpochCoordinator coordinator, string operation,
        SharedProjectionCapturedIdentityReference reference, int first, int count, int changed = -1, int repeatedMessage = -1)
        => coordinator.CapturedIdentities.AppendAsync(s_scope, operation, reference, first,
            Enumerable.Range(first, count).Select(sequence => sequence * 2L).ToArray(),
            Enumerable.Range(first, count).SelectMany(sequence => Digest(sequence, sequence == changed)).ToArray(),
            Enumerable.Range(first, count).Select(sequence => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                "message-" + (repeatedMessage > 0 && sequence == first ? repeatedMessage : sequence))))).ToArray());

    private static byte[] Digest(long sequence, bool changed = false)
        => SharedProjectionCapturedEventIdentity.Compute("owner-a", sequence * 2, sequence, changed ? [99] : [7]);
    private static SharedProjectionDelivery Delivery(long sequence) => new("owner-a", sequence * 2, [7], JsonSerializer.SerializeToUtf8Bytes(sequence * 2), OwnerSequenceNumber: sequence);
    private static Task<IReadOnlyList<ReadModelBatchOperation>> Fold(SharedProjectionDelivery delivery, long _, CancellationToken token)
        => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = delivery.OwnerSequenceNumber }, ReadModelBatchConcurrency.LastWrite)]);
    private static async Task PromoteAsync(SharedProjectionEpochCoordinator coordinator, string operation, int count, SharedProjectionCapturedIdentityReference reference)
    {
        (await coordinator.CanPromoteCapturedOwnerIdentityAsync(s_scope, operation, "owner-a", count, reference)).ShouldBeTrue();
        await coordinator.StageAsync(s_scope, operation, [ReadModelBatchOperation.Write("view", new JsonObject { ["count"] = count }, ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(s_scope, operation, requireOwnerSequenceHandoff: true);
        await coordinator.SeedCapturedOwnerIdentityAsync(s_scope, operation, "owner-a", count, reference);
        await coordinator.CompleteOwnerSequenceHandoffAsync(s_scope, operation);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
    }
}
