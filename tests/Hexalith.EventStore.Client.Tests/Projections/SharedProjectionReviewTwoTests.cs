using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionReviewTwoTests
{
    private static readonly DateTimeOffset s_deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Fact]
    public async Task PersistedPreparedCatchUpRetainsDeadlinesAfterStoreAndCoordinatorReplacementWithoutRefolding()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddMinutes(-1));
        var options = Options.Create(new ReadModelBatchOptions { TimeProvider = clock });
        var store = new DaprReadModelStore(client, options);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "journal-expiry", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>());
        lease = await coordinator.RefreshLeaseAsync(scope, "writer");
        (await coordinator.JournalAsync(scope, lease, new("owner", 1, [1], [1]))).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.StageAsync(scope, "rebuild", [ReadModelBatchOperation.Write("checkpoint", Value(0), ReadModelBatchConcurrency.LastWrite)]);
        await coordinator.CommitAsync(scope, "rebuild");
        int folds = 0;
        bool interrupted = false;
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (!interrupted && key == scope.PhysicalKey(1, "expiring") && ReadModelBatchEnvelope.IsEnvelope(bytes.Span))
            { interrupted = true; throw new InvalidOperationException("prepared batch installation stopped"); }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(scope, (_, _, _) =>
        {
            folds++;
            return Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
                ReadModelBatchOperation.WriteExpiring("expiring", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline),
                ReadModelBatchOperation.WriteExpiring("receipt", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline),
                ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite)]);
        }));
        SharedProjectionEpochState pending = (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!;
        pending.Journal.Single().PreparedReady.ShouldBeTrue();
        pending.Journal.Single().Prepared!.Where(mutation => mutation.Key != "checkpoint")
            .All(mutation => mutation.ExpiresAtUtc == s_deadline).ShouldBeTrue();
        byte[] originalPrepared = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pending.Journal.Single().Prepared);
        folds.ShouldBe(1);
        client.BeforeTrySaveByteState = null;
        clock.UtcNow = s_deadline;
        store = new DaprReadModelStore(client, options);
        coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        (await coordinator.CatchUpAsync(scope, (_, _, _) => throw new InvalidOperationException("must replay original prepared bytes"))).ShouldBe(1);
        originalPrepared.ShouldNotBeEmpty();
        client.ByteStoreContains(scope.PhysicalKey(1, "expiring")).ShouldBeFalse();
        client.ByteStoreContains(scope.PhysicalKey(1, "receipt")).ShouldBeFalse();
        var selected = await coordinator.ReadAsync<JsonObject>(scope, "expiring");
        selected.IsAvailable.ShouldBeTrue(); selected.Value.ShouldBeNull(); selected.Generation.ShouldBe(1);
        (await coordinator.ReadAsync<JsonObject>(scope, "checkpoint")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        (await coordinator.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(0);
        (await coordinator.GetCheckpointAsync(scope, "owner"))!.Position.ShouldBe(1);
        folds.ShouldBe(1);
    }

    [Fact]
    public async Task AbandonedSameKeyReplacementOfPromotedPointerKeepsOriginalDeadline()
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddMinutes(-1));
        var store = new InMemoryReadModelStore(); store.BatchOptions.TimeProvider = clock;
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "promoted-expiry", ["writer"]) { CopyOnWrite = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Writes(true));
        await coordinator.CommitAsync(scope, "first");
        _ = await coordinator.BeginAsync(scope, "abandoned", "inventory2", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "abandoned", Writes(false));
        await coordinator.AbortAsync(scope, "abandoned", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        clock.UtcNow = s_deadline.AddTicks(-1);
        (await coordinator.ReadAsync<JsonObject>(scope, "model")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        clock.UtcNow = s_deadline;
        var expired = await coordinator.ReadAsync<JsonObject>(scope, "model");
        expired.IsAvailable.ShouldBeTrue(); expired.Value.ShouldBeNull();
        static async IAsyncEnumerable<ReadModelBatchOperation> Writes(bool first)
        {
            yield return first ? ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline)
                : ReadModelBatchOperation.Write("model", Value(99), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }
}
