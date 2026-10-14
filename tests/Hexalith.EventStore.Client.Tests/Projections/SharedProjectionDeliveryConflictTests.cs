using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionDeliveryConflictTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "tenant-a", "widget", "widgets", ["writer"]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingAndReceiptedConflictsSurviveRestartPreservingAcceptedContentsAndCheckpoint(bool receipted)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var accepted = new SharedProjectionDelivery("widget-a", 1, [1], [10]);
        var rejected = accepted with { CanonicalPayload = [2], Checkpoint = [20] };
        (await coordinator.JournalAsync(s_scope, lease, accepted)).ShouldBe(SharedProjectionJournalResult.Journaled);
        if (receipted) _ = await coordinator.CatchUpAsync(s_scope, Fold);
        (await coordinator.JournalAsync(s_scope, lease, rejected)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.JournalAsync(s_scope, lease, rejected)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await coordinator.JournalAsync(s_scope, lease, accepted)).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeTrue();
        status.DeliveryConflicts.Count.ShouldBe(1);
        SharedProjectionDeliveryConflict conflict = status.DeliveryConflicts.Single();
        conflict.SourceStream.ShouldBe("widget-a");
        conflict.EnvelopePosition.ShouldBe(1);
        conflict.AcceptedDigest.ShouldNotBe(conflict.ConflictingDigest);
        conflict.ResolvedByOperationId.ShouldBeNull();
        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!;
        checkpoint.Position.ShouldBe(1);
        checkpoint.Token.ShouldBe(new byte[] { 10 });
        SharedProjectionReadResult<string[]> single = await coordinator.ReadAsync<string[]>(s_scope, "index");
        single.IsAvailable.ShouldBeTrue();
        single.IsDegraded.ShouldBeTrue();
        single.Value.ShouldBe(new[] { "accepted" });
        var related = await coordinator.ReadManyAsync<string[]>(s_scope, ["index"]);
        related.IsDegraded.ShouldBeTrue();
        related.Value!["index"].ShouldBe(single.Value);

        // A different tenant/family cannot read or resolve this scope's disposition.
        var other = s_scope with { TenantId = "tenant-b" };
        _ = await coordinator.RegisterWriterAsync(other, "writer");
        (await coordinator.GetStatusAsync(other)).IsDegraded.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ResolveDeliveryConflictsAsync(s_scope, "missing"));
    }

    [Fact]
    public async Task FailedEvidenceWriteCannotAcknowledgeConflictAndRetryConverges()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var accepted = new SharedProjectionDelivery("widget-a", 1, [1], [10]);
        _ = await coordinator.JournalAsync(s_scope, lease, accepted);
        store.ConcurrentWriteBeforeTrySave = () => throw new IOException("synthetic conflict persistence failure");
        await Should.ThrowAsync<IOException>(() => coordinator.JournalAsync(s_scope, lease, accepted with { CanonicalPayload = [2] }));
        store.ConcurrentWriteBeforeTrySave = null;
        (await coordinator.JournalAsync(s_scope, lease, accepted with { CanonicalPayload = [2] }))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await new SharedProjectionEpochCoordinator(store, store).GetStatusAsync(s_scope)).IsDegraded.ShouldBeTrue();
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.Token.ShouldBe(new byte[] { 10 });
    }

    [Fact]
    public async Task OnlyExplicitCoveringPromotedRepairResolvesEvidenceWhileAbortAndPromotionAloneRetainIt()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var accepted = new SharedProjectionDelivery("widget-a", 1, [1], [10]);
        _ = await coordinator.JournalAsync(s_scope, lease, accepted);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        _ = await coordinator.JournalAsync(s_scope, lease, accepted with { CanonicalPayload = [2] });
        _ = await coordinator.BeginAsync(s_scope, "abort", "sealed", new Dictionary<string, long> { ["widget-a"] = 1 });
        await coordinator.AbortAsync(s_scope, "abort", Fold);
        await coordinator.CompleteAbortAsync(s_scope, "abort");
        (await coordinator.GetStatusAsync(s_scope)).IsDegraded.ShouldBeTrue();
        _ = await coordinator.BeginAsync(s_scope, "incomplete", "sealed", new Dictionary<string, long>());
        _ = await coordinator.StageAsync(s_scope, "incomplete", Writes());
        await coordinator.CommitAsync(s_scope, "incomplete");
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ResolveDeliveryConflictsAsync(s_scope, "incomplete"));
        _ = await coordinator.BeginAsync(s_scope, "repair", "sealed", new Dictionary<string, long> { ["widget-a"] = 1 });
        _ = await coordinator.StageAsync(s_scope, "repair", Writes());
        await coordinator.CommitAsync(s_scope, "repair");
        (await coordinator.ReadAsync<string[]>(s_scope, "index")).IsDegraded.ShouldBeTrue();
        await coordinator.ResolveDeliveryConflictsAsync(s_scope, "repair");
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.ResolveDeliveryConflictsAsync(s_scope, "repair");
        SharedProjectionEpochStatus status = await restarted.GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeFalse();
        status.DeliveryConflicts.Single().ResolvedByOperationId.ShouldBe("repair");
        (await restarted.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeFalse();
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Token.ShouldBe(new byte[] { 10 });
    }

    [Fact]
    public async Task ResolvedEvidenceCapacityCannotHideNewConflictAcrossRestart()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var accepted = new SharedProjectionDelivery("widget-a", 1, [1], [10]);
        _ = await coordinator.JournalAsync(s_scope, lease, accepted);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        var full = entry.Value! with
        {
            DeliveryConflicts = [.. Enumerable.Range(0, 128).Select(index => new SharedProjectionDeliveryConflict(
                "previous", index, new string('a', 64), new string('b', 64), 0, "previous-repair"))],
        };
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, full, entry.ETag!)).ShouldBeTrue();
        (await coordinator.ReadAsync<string[]>(s_scope, "index")).IsDegraded.ShouldBeFalse();
        var rejected = accepted with { CanonicalPayload = [2] };
        (await coordinator.JournalAsync(s_scope, lease, rejected)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.JournalAsync(s_scope, lease, rejected)).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        var status = await restarted.GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeTrue();
        status.DeliveryConflicts.Count.ShouldBe(128);
        status.DeliveryConflictOverflow.ShouldNotBeNull().SourceStream.ShouldBe("widget-a");
        (await restarted.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => restarted.JournalAsync(s_scope, lease,
            accepted with { CanonicalPayload = [3] }));
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Token.ShouldBe(new byte[] { 10 });
    }

    [Fact]
    public async Task LegacyEpochNearByteLimitIsDurablyUnavailableWhenConflictEvidenceCannotFit()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var accepted = new SharedProjectionDelivery("widget-a", 1, [1], [10]);
        _ = await coordinator.JournalAsync(s_scope, lease, accepted);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        // Simulate an existing version-one epoch written before the emergency byte reserve existed.
        var legacy = entry.Value! with { InventoryFingerprint = new string('x', 510 * 1024) };
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, legacy, entry.ETag!)).ShouldBeTrue();
        var read = await coordinator.ReadAsync<string[]>(s_scope, "index");
        read.IsDegraded.ShouldBeTrue();
        read.Value.ShouldBe(new[] { "accepted" });
        try { _ = await coordinator.JournalAsync(s_scope, lease, accepted with { CanonicalPayload = [2] }); }
        catch (InvalidOperationException) { /* No acknowledgement if even reserved evidence cannot fit. */ }
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(s_scope)).IsDegraded.ShouldBeTrue();
        (await restarted.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeTrue();
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.Token.ShouldBe(new byte[] { 10 });
    }

    [Theory]
    [InlineData("empty-repair")]
    [InlineData("blank-repair")]
    [InlineData("overflow-source")]
    [InlineData("overflow-epoch")]
    [InlineData("overflow-resolved")]
    [InlineData("gap-without-required-sequence")]
    [InlineData("gap-wrong-resolution")]
    [InlineData("gap-nonpositive")]
    public async Task CorruptConflictAuditCannotReportCurrentReadiness(string corruption)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        var conflict = new SharedProjectionDeliveryConflict("widget-a", 1, new string('a', 64), new string('b', 64), 0);
        SharedProjectionEpochState invalid = corruption switch
        {
            "empty-repair" => entry.Value! with { DeliveryConflicts = [conflict with { ResolvedByOperationId = "" }] },
            "blank-repair" => entry.Value! with { DeliveryConflicts = [conflict with { ResolvedByOperationId = " " }] },
            "overflow-source" => entry.Value! with { DeliveryConflictOverflow = conflict with { SourceStream = "" } },
            "overflow-epoch" => entry.Value! with { DeliveryConflictOverflow = conflict with { ObservedEpoch = 1 } },
            "gap-without-required-sequence" => entry.Value! with { DeliveryConflicts = [conflict with { ResolvedByOwnerSequenceNumber = 3 }] },
            "gap-wrong-resolution" => entry.Value! with { DeliveryConflicts = [conflict with { RequiredOwnerSequenceNumber = 3, ResolvedByOwnerSequenceNumber = 2 }] },
            "gap-nonpositive" => entry.Value! with { DeliveryConflicts = [conflict with { RequiredOwnerSequenceNumber = 0 }] },
            _ => entry.Value! with { DeliveryConflictOverflow = conflict with { ResolvedByOperationId = "repair" } },
        };
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, invalid, entry.ETag!)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadAsync<string[]>(s_scope, "index"));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadManyAsync<string[]>(s_scope, ["index"]));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.GetStatusAsync(s_scope));
    }

    [Fact]
    public async Task OwnerSequenceGapPersistsUntilExactMissingPrefixAndDeliveryAreFolded()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var first = new SharedProjectionDelivery("widget-a", 1, [1], [1], OwnerSequenceNumber: 1);
        _ = await coordinator.JournalAsync(s_scope, lease, first);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var future = new SharedProjectionDelivery("widget-a", 3, [3], [3], OwnerSequenceNumber: 3);
        (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.Position.ShouldBe(1);
        (await coordinator.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeTrue();
        (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        (await coordinator.GetStatusAsync(s_scope)).DeliveryConflicts.Count.ShouldBe(1);
        _ = await coordinator.JournalAsync(s_scope, lease, new SharedProjectionDelivery("widget-a", 2, [2], [2], OwnerSequenceNumber: 2));
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        (await coordinator.ReadAsync<string[]>(s_scope, "index")).IsDegraded.ShouldBeTrue();
        (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var status = await new SharedProjectionEpochCoordinator(store, store).GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeFalse();
        status.DeliveryConflicts.Single().ResolvedByOwnerSequenceNumber.ShouldBe(3);
        (await coordinator.GetCheckpointAsync(s_scope, "widget-a"))!.OwnerSequenceNumber.ShouldBe(3);
        // A lower owner ordinal at a new envelope is conflicting evidence, never a repairable future gap.
        (await coordinator.JournalAsync(s_scope, lease,
            new SharedProjectionDelivery("widget-a", 4, [4], [4], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await coordinator.GetStatusAsync(s_scope)).IsDegraded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactGapRetryAfterTransientFoldFailureResolvesOnlyItsGap(bool identityConflict)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var first = new SharedProjectionDelivery("widget-a", 1, [1], [1], OwnerSequenceNumber: 1);
        _ = await coordinator.JournalAsync(s_scope, lease, first);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        if (identityConflict)
            _ = await coordinator.JournalAsync(s_scope, lease, first with { CanonicalPayload = [9] });
        var future = new SharedProjectionDelivery("widget-a", 3, [3], [3], OwnerSequenceNumber: 3);
        (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        _ = await coordinator.JournalAsync(s_scope, lease, new("widget-a", 2, [2], [2], OwnerSequenceNumber: 2));
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        _ = await coordinator.JournalAsync(s_scope, lease, future);
        await Should.ThrowAsync<SharedProjectionRetryableFoldException>(() => coordinator.CatchUpAsync(s_scope,
            (delivery, _, _) => throw new SharedProjectionFoldFailureException(delivery.SourceStream,
                delivery.EnvelopePosition, 2, _ => Writes())));
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        var status = await restarted.GetStatusAsync(s_scope);
        status.PendingDeliveryCount.ShouldBe(0);
        status.DeliveryConflicts.Single(conflict => conflict.RequiredOwnerSequenceNumber is not null).IsResolved.ShouldBeTrue();
        status.IsDegraded.ShouldBe(identityConflict);
        (await restarted.GetCheckpointAsync(s_scope, "widget-a"))!.OwnerSequenceNumber.ShouldBe(3);
        (await restarted.ReadAsync<string[]>(s_scope, "index")).IsDegraded.ShouldBe(identityConflict);
        (await restarted.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBe(identityConflict);
        if (identityConflict)
            status.DeliveryConflicts.Single(conflict => conflict.RequiredOwnerSequenceNumber is null).IsResolved.ShouldBeFalse();
    }

    [Fact]
    public async Task CapacityAttemptsNeverConsumePoisonRetryBudgetEvenAtMaximumCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer", ct);
        _ = await coordinator.JournalAsync(s_scope, lease, new("widget-a", 1, [1], [1]), ct);
        var capacity = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(() => coordinator.CatchUpAsync(s_scope,
            (_, _, _) => throw new SharedProjectionCapacityExceededException(), ct));
        capacity.Failure.ReasonCode.ShouldBe("capacity");
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey, ct);
        var head = entry.Value!.Journal.Single();
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, entry.Value with
        {
            Journal = [head with { Failure = head.Failure! with { FailureCount = 1000 } }],
        }, entry.ETag!, ct)).ShouldBeTrue();
        var poison = await Should.ThrowAsync<SharedProjectionRetryableFoldException>(() => coordinator.CatchUpAsync(s_scope,
            (delivery, _, _) => throw new SharedProjectionFoldFailureException(delivery.SourceStream,
                delivery.EnvelopePosition, 2, _ => Writes()), ct));
        poison.Failure.ReasonCode.ShouldBe("fold-failure");
        poison.Failure.FailureCount.ShouldBe(1);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        var pending = await coordinator.GetStatusAsync(s_scope, ct);
        pending.IsDegraded.ShouldBeTrue();
        pending.ParkingEvidence.ShouldBeEmpty();
        pending.PendingFoldFailures.ShouldHaveSingleItem().FailureCount.ShouldBe(1);
        _ = await coordinator.CatchUpAsync(s_scope, Fold, ct);
        var applied = await new SharedProjectionEpochCoordinator(store, store).GetStatusAsync(s_scope, ct);
        applied.PendingDeliveryCount.ShouldBe(0);
        applied.IsDegraded.ShouldBeFalse();
        applied.ParkingEvidence.ShouldBeEmpty();
        (await coordinator.ReadAsync<string[]>(s_scope, "index", ct)).Value.ShouldBe(["accepted"]);
    }

    [Fact]
    public async Task FutureGapIdentityIsStableWhileAcceptedPrefixAdvancesAcrossRestarts()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var future = new SharedProjectionDelivery("owner", 5, [5], [5], OwnerSequenceNumber: 5);
        for (long prefix = 0; prefix < 4; prefix++)
        {
            (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
            (await coordinator.GetStatusAsync(s_scope)).DeliveryConflicts.Count.ShouldBe(1);
            if (prefix == 3)
            {
                (await coordinator.JournalAsync(s_scope, lease, future with { CanonicalPayload = [9] }))
                    .ShouldBe(SharedProjectionJournalResult.DeliveryGap);
            }
            long next = prefix + 1;
            _ = await coordinator.JournalAsync(s_scope, lease, new("owner", next, [(byte)next], [(byte)next], OwnerSequenceNumber: next));
            _ = await coordinator.CatchUpAsync(s_scope, Fold);
            coordinator = new SharedProjectionEpochCoordinator(store, store);
            (await coordinator.GetStatusAsync(s_scope)).DeliveryConflicts.Count.ShouldBe(prefix == 3 ? 2 : 1);
        }
        (await coordinator.JournalAsync(s_scope, lease, future)).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        var status = await new SharedProjectionEpochCoordinator(store, store).GetStatusAsync(s_scope);
        status.DeliveryConflicts.Count.ShouldBe(2);
        status.DeliveryConflicts.Count(item => item.IsResolved).ShouldBe(1);
        status.IsDegraded.ShouldBeTrue();
        (await coordinator.GetCheckpointAsync(s_scope, "owner"))!.OwnerSequenceNumber.ShouldBe(5);
    }

    private static IReadOnlyList<ReadModelBatchOperation> Writes()
        => [ReadModelBatchOperation.Write("index", new[] { "accepted" }, ReadModelBatchConcurrency.LastWrite)];

    private static Task<IReadOnlyList<ReadModelBatchOperation>> Fold(SharedProjectionDelivery _, long __, CancellationToken ___)
        => Task.FromResult(Writes());
}
