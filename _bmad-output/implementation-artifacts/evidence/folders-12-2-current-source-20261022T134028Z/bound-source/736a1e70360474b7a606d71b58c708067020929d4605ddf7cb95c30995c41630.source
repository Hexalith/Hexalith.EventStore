using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionParkingEvidenceTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "tenant-a", "widget", "widgets", ["writer"]);

    [Fact]
    public async Task ParkingSurvivesHealthyDeliveryAndOnlyExplicitCoveringRepairResolvesItsAudit()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        _ = await coordinator.JournalAsync(s_scope, lease, new("poison", 1, [1], [1]));
        _ = await coordinator.CatchUpAsync(s_scope, Park);
        _ = await coordinator.JournalAsync(s_scope, lease, new("healthy", 1, [2], [1]));
        _ = await coordinator.CatchUpAsync(s_scope, Fold);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.ReadAsync<string[]>(s_scope, "index")).IsDegraded.ShouldBeTrue();
        (await coordinator.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeTrue();
        (await coordinator.GetStatusAsync(s_scope)).ParkingEvidence.ShouldHaveSingleItem().ResolvedByOperationId.ShouldBeNull();
        _ = await coordinator.BeginAsync(s_scope, "repair", "sealed", new Dictionary<string, long> { ["poison"] = 1, ["healthy"] = 1 });
        _ = await coordinator.StageAsync(s_scope, "repair", Writes());
        await coordinator.CommitAsync(s_scope, "repair");
        (await coordinator.GetStatusAsync(s_scope)).IsDegraded.ShouldBeTrue();
        await coordinator.ResolveDeliveryConflictsAsync(s_scope, "repair");
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        var status = await restarted.GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeFalse();
        status.ParkingEvidence.ShouldHaveSingleItem().ResolvedByOperationId.ShouldBe("repair");
        (await restarted.GetCheckpointAsync(s_scope, "poison"))!.Position.ShouldBe(1);
    }

    [Fact]
    public async Task FullResolvedParkingAuditRetainsEmergencyUnresolvedEvidence()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var lease = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        var full = entry.Value! with
        {
            ParkingEvidence = [.. Enumerable.Range(0, 128).Select(index => new SharedProjectionParkingEvidence("old", index, 0, 1, "old-repair"))],
        };
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, full, entry.ETag!)).ShouldBeTrue();
        _ = await coordinator.JournalAsync(s_scope, lease, new("poison", 1, [1], [1]));
        _ = await coordinator.CatchUpAsync(s_scope, Park);
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        var status = await restarted.GetStatusAsync(s_scope);
        status.IsDegraded.ShouldBeTrue();
        status.ParkingEvidence.Count.ShouldBe(128);
        status.ParkingEvidenceOverflow.ShouldNotBeNull().SourceStream.ShouldBe("poison");
        (await restarted.ReadManyAsync<string[]>(s_scope, ["index"])).IsDegraded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("blank-repair")]
    [InlineData("source")]
    [InlineData("epoch")]
    [InlineData("count")]
    [InlineData("resolved-overflow")]
    public async Task MalformedParkingAuditFailsClosed(string corruption)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "writer");
        var entry = await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        var parked = new SharedProjectionParkingEvidence("poison", 1, 0, 1);
        var invalid = corruption switch
        {
            "blank-repair" => entry.Value! with { ParkingEvidence = [parked with { ResolvedByOperationId = " " }] },
            "source" => entry.Value! with { ParkingEvidenceOverflow = parked with { SourceStream = "" } },
            "epoch" => entry.Value! with { ParkingEvidenceOverflow = parked with { ObservedEpoch = 1 } },
            "count" => entry.Value! with { ParkingEvidence = [parked with { FailureCount = 1001 }] },
            _ => entry.Value! with { ParkingEvidenceOverflow = parked with { ResolvedByOperationId = "repair" } },
        };
        (await store.TrySaveAsync(s_scope.StoreName, s_scope.StateKey, invalid, entry.ETag!)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadAsync<string[]>(s_scope, "index"));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadManyAsync<string[]>(s_scope, ["index"]));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.GetStatusAsync(s_scope));
    }

    private static IReadOnlyList<ReadModelBatchOperation> Writes()
        => [ReadModelBatchOperation.Write("index", new[] { "accepted" }, ReadModelBatchConcurrency.LastWrite)];

    private static Task<IReadOnlyList<ReadModelBatchOperation>> Park(SharedProjectionDelivery delivery, long _, CancellationToken __)
        => throw new SharedProjectionFoldFailureException(delivery.SourceStream, delivery.EnvelopePosition, 1, _ => Writes());

    private static Task<IReadOnlyList<ReadModelBatchOperation>> Fold(SharedProjectionDelivery _, long __, CancellationToken ___)
        => Task.FromResult(Writes());
}
