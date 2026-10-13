using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionCopyOnWriteTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "global", "widget",
        "copy-on-write", ["writer"]) { CopyOnWrite = true };

    [Fact]
    public async Task SparseStagesRetainUnchangedValuesAcrossRepeatedSelections()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("alpha", "a1"), ("beta", "b1"));
        await StageAndCommitAsync(coordinator, "second", ("alpha", "a2"));
        await StageAndCommitAsync(coordinator, "third", ("gamma", "c1"));

        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldNotBeNull().Value.ShouldBe("a2");
        (await coordinator.ReadAsync<Sample>(s_scope, "beta")).Value.ShouldNotBeNull().Value.ShouldBe("b1");
        (await coordinator.ReadAsync<Sample>(s_scope, "gamma")).Value.ShouldNotBeNull().Value.ShouldBe("c1");
    }

    [Fact]
    public async Task RepeatedReplacementRetiresOnlyTheObsoletePhysicalVersion()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("alpha", "a1"));
        await StageAndCommitAsync(coordinator, "second", ("alpha", "a2"));
        await StageAndCommitAsync(coordinator, "third", ("alpha", "a3"));

        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "alpha")))
            .Value.ShouldBeNull();
        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(2, "alpha")))
            .Value.ShouldNotBeNull();
        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(3, "alpha")))
            .Value.ShouldNotBeNull();
        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldNotBeNull().Value
            .ShouldBe("a3");
    }

    [Fact]
    public async Task AbandonedStageCannotBecomeInheritedAfterAnotherTenantPromotes()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("alpha", "selected"));
        _ = await coordinator.BeginAsync(s_scope, "abandoned", "inventory-abandoned",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, "abandoned", Writes(("alpha", "abandoned")));
        await coordinator.AbortAsync(s_scope, "abandoned",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        await StageAndCommitAsync(coordinator, "another-tenant", ("beta", "new"));

        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldNotBeNull().Value
            .ShouldBe("selected");
        (await coordinator.ReadAsync<Sample>(s_scope, "beta")).Value.ShouldNotBeNull().Value
            .ShouldBe("new");
    }

    [Fact]
    public async Task SelectedDeleteMasksAnInheritedValueAcrossLaterPromotions()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("alpha", "old"));
        _ = await coordinator.BeginAsync(s_scope, "delete", "inventory-delete",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, "delete", Deletes("alpha"));
        await coordinator.CommitAsync(s_scope, "delete");
        await StageAndCommitAsync(coordinator, "unrelated", ("beta", "new"));

        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldBeNull();
        (await coordinator.ReadAsync<Sample>(s_scope, "beta")).Value.ShouldNotBeNull().Value
            .ShouldBe("new");
    }

    [Fact]
    public async Task PromotedRetirementReclaimsOldValueButAbortedRetirementDoesNot()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("node-old", "old"), ("root", "old"));

        _ = await coordinator.BeginAsync(s_scope, "abort-retire", "inventory-abort-retire",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, "abort-retire",
            Retiring("node-old", 1, "node-unused", "unused"));
        await coordinator.AbortAsync(s_scope, "abort-retire",
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "node-old")))
            .Value.ShouldNotBeNull();

        _ = await coordinator.BeginAsync(s_scope, "replace", "inventory-replace",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, "replace",
            Retiring("node-old", 1, "root", "new"));
        await coordinator.CommitAsync(s_scope, "replace");
        _ = await coordinator.BeginAsync(s_scope, "reconcile-retire", "inventory-reconcile-retire",
            new Dictionary<string, long>());

        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "node-old")))
            .Value.ShouldBeNull();
        (await coordinator.ReadAsync<Sample>(s_scope, "root")).Value.ShouldNotBeNull().Value
            .ShouldBe("new");
    }

    [Fact]
    public async Task RetirementCleanupRetriesWhenMarkerValueWasAlreadyDeleted()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("node-old", "old"));

        _ = await coordinator.BeginAsync(s_scope, "retire", "inventory-retire",
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, "retire",
            Retiring("node-old", 1, "root", "new"));
        SharedProjectionEpochState state = store.Snapshot<SharedProjectionEpochState>(
            s_scope.StoreName, s_scope.StateKey)!;
        string physicalMarker = s_scope.PhysicalKey(state.Epoch, "__cow-retired:0");
        ReadModelEntry<SharedProjectionRetiredValue> marker = await store
            .GetAsync<SharedProjectionRetiredValue>(s_scope.StoreName, physicalMarker);
        marker.Value.ShouldNotBeNull();

        // Reproduce an interrupted metadata cleanup after its first delete, while the
        // durable stream page and selected value are still present.
        (await store.TryEraseAsync(s_scope.StoreName, physicalMarker, marker.ETag!)).ShouldBeTrue();
        await coordinator.CommitAsync(s_scope, "retire");
        _ = await coordinator.BeginAsync(s_scope, "reconcile", "inventory-reconcile",
            new Dictionary<string, long>());

        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "node-old")))
            .Value.ShouldBeNull();
        (await coordinator.ReadAsync<Sample>(s_scope, "root")).Value.ShouldNotBeNull().Value
            .ShouldBe("new");
    }

    [Fact]
    public async Task OrdinaryRetirementResumesAfterRootBatchAndBeforeLaterDelivery()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        await StageAndCommitAsync(coordinator, "first", ("node-old", "old"), ("root", "old"));
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "writer");
        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("owner-one", 1, [1], [1], OwnerSequenceNumber: 1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);

        // Background cleanup shares the hook; interrupt retirement whenever the new root is durable.
        store.BatchFaultHook = async (phase, _, cancellationToken) =>
        {
            if (phase == ReadModelBatchPhase.BeforeDispatch
                && (await store.GetAsync<Sample>(s_scope.StoreName,
                    s_scope.PhysicalKey(1, "root"), cancellationToken)).Value?.Value == "new")
            {
                throw new IOException("Interrupted after the new root batch.");
            }
        };
        await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() =>
            coordinator.CatchUpAsync(s_scope, Fold));
        store.BatchFaultHook = null;
        (await coordinator.ReadAsync<Sample>(s_scope, "root")).Value.ShouldNotBeNull().Value
            .ShouldBe("new");
        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "node-old")))
            .Value.ShouldNotBeNull();

        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("owner-two", 1, [2], [2], OwnerSequenceNumber: 1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_scope, Fold)).ShouldBe(2);
        (await store.GetAsync<Sample>(s_scope.StoreName, s_scope.PhysicalKey(1, "node-old")))
            .Value.ShouldBeNull();
        (await coordinator.ReadAsync<Sample>(s_scope, "root")).Value.ShouldNotBeNull().Value
            .ShouldBe("later");

        async Task<IReadOnlyList<ReadModelBatchOperation>> Fold(
            SharedProjectionDelivery delivery, long _, CancellationToken cancellationToken)
        {
            if (delivery.SourceStream == "owner-two")
            {
                (await store.GetAsync<Sample>(s_scope.StoreName,
                    s_scope.PhysicalKey(1, "node-old"), cancellationToken)).Value.ShouldBeNull();
                return [ReadModelBatchOperation.Write("root", new Sample("later"),
                    ReadModelBatchConcurrency.LastWrite)];
            }

            var operations = new List<ReadModelBatchOperation>();
            for (int index = 0; index < 30; index++)
                operations.Add(ReadModelBatchOperation.Write($"padding-{index:D2}",
                    new Sample("padding"), ReadModelBatchConcurrency.LastWrite));
            operations.Add(ReadModelBatchOperation.Write("node-new", new Sample("new"),
                ReadModelBatchConcurrency.LastWrite));
            operations.Add(ReadModelBatchOperation.Write("root", new Sample("new"),
                ReadModelBatchConcurrency.LastWrite));
            operations.Add(SharedProjectionRetiredValue.Stage(0, 1, "node-old"));
            return operations;
        }
    }

    [Fact]
    public async Task AppendedPagesCanBeReadPrivatelyRetriedSealedAndPromoted()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        _ = await coordinator.BeginAsync(s_scope, "append", "inventory-append",
            new Dictionary<string, long>());
        ReadModelBatchOperation first = ReadModelBatchOperation.Write("alpha", new Sample("one"),
            ReadModelBatchConcurrency.LastWrite);
        ReadModelBatchOperation second = ReadModelBatchOperation.Write("beta", new Sample("two"),
            ReadModelBatchConcurrency.LastWrite);
        await coordinator.AppendStreamingPageAsync(s_scope, "append", 0, [first]);
        await coordinator.AppendStreamingPageAsync(s_scope, "append", 0, [first]);
        (await coordinator.GetStreamingStageStatusAsync(s_scope, "append"))
            .CompletedPageCount.ShouldBe(1);
        (await coordinator.ReadGenerationAsync<Sample>(s_scope, 1, "alpha"))
            .Value.ShouldNotBeNull().Value.ShouldBe("one");
        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldBeNull();
        await coordinator.AppendStreamingPageAsync(s_scope, "append", 1, [second]);
        string fingerprint = await coordinator.SealStreamingAsync(s_scope, "append");
        (await coordinator.SealStreamingAsync(s_scope, "append")).ShouldBe(fingerprint);
        (await coordinator.GetStreamingStageStatusAsync(s_scope, "append"))
            .IsSealed.ShouldBeTrue();
        await coordinator.CommitAsync(s_scope, "append");
        (await coordinator.ReadAsync<Sample>(s_scope, "alpha")).Value.ShouldNotBeNull().Value
            .ShouldBe("one");
        (await coordinator.ReadAsync<Sample>(s_scope, "beta")).Value.ShouldNotBeNull().Value
            .ShouldBe("two");
    }

    private static async Task StageAndCommitAsync(SharedProjectionEpochCoordinator coordinator,
        string operationId, params (string Key, string Value)[] values)
    {
        _ = await coordinator.BeginAsync(s_scope, operationId, "inventory-" + operationId,
            new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(s_scope, operationId, Writes(values));
        await coordinator.CommitAsync(s_scope, operationId);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> Writes(
        params (string Key, string Value)[] values)
    {
        foreach ((string key, string value) in values)
        {
            yield return ReadModelBatchOperation.Write(key, new Sample(value),
                ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> Deletes(string key)
    {
        yield return ReadModelBatchOperation.Delete(key, ReadModelBatchConcurrency.IdempotentAbsent);
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> Retiring(
        string oldKey, long oldGeneration, string newKey, string newValue)
    {
        yield return ReadModelBatchOperation.Write(newKey, new Sample(newValue),
            ReadModelBatchConcurrency.LastWrite);
        yield return SharedProjectionRetiredValue.Stage(0, oldGeneration, oldKey);
        await Task.Yield();
    }

}
