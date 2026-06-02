using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class ReadModelWritePolicyTests {
    private const string StoreName = "statestore";

    // Mutable, JSON-serializable read model used across the tests.
    public sealed class CounterReadModel {
        public int Value { get; set; }

        public List<string> AppliedEvents { get; set; } = [];
    }

    [Fact]
    public async Task UpdateAsync_AbsentKey_CreatesFromNull() {
        var store = new InMemoryReadModelStore();

        CounterReadModel result = await ReadModelWritePolicy.UpdateAsync<CounterReadModel>(
            store,
            StoreName,
            "counter:1",
            current => new CounterReadModel { Value = (current?.Value ?? 0) + 1 });

        result.Value.ShouldBe(1);
        store.Snapshot<CounterReadModel>(StoreName, "counter:1")!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_ExistingKey_AppliesToLoadedValue() {
        var store = new InMemoryReadModelStore();
        store.SeedRaw(StoreName, "counter:1", new CounterReadModel { Value = 10 });

        CounterReadModel result = await ReadModelWritePolicy.UpdateAsync<CounterReadModel>(
            store,
            StoreName,
            "counter:1",
            current => new CounterReadModel { Value = (current?.Value ?? 0) + 5 });

        result.Value.ShouldBe(15);
    }

    [Fact]
    public async Task UpdateAsync_EtagConflictThenSuccess_ReloadsAndMergesLatest() {
        var store = new InMemoryReadModelStore();

        // Force exactly one conflict: a competing writer seeds Value=5 just before our first TrySave,
        // then we clear the hook so the retry succeeds against the latest value.
        store.ConcurrentWriteBeforeTrySave = () => {
            store.SeedRaw(StoreName, "counter:1", new CounterReadModel { Value = 5 });
            store.ConcurrentWriteBeforeTrySave = null;
        };

        CounterReadModel result = await ReadModelWritePolicy.UpdateAsync<CounterReadModel>(
            store,
            StoreName,
            "counter:1",
            current => new CounterReadModel { Value = (current?.Value ?? 0) + 1 });

        // Retry observed the competing write (5) and incremented it — proving reload-and-merge,
        // not a blind overwrite that would have produced 1.
        result.Value.ShouldBe(6);
        store.Snapshot<CounterReadModel>(StoreName, "counter:1")!.Value.ShouldBe(6);
    }

    [Fact]
    public async Task UpdateAsync_PersistentConflict_ThrowsAfterMaxAttempts() {
        var store = new InMemoryReadModelStore();
        ILogger logger = Substitute.For<ILogger>();

        // Every TrySave is preceded by a competing write, so the held ETag is always stale.
        store.ConcurrentWriteBeforeTrySave = () =>
            store.SeedRaw(StoreName, "counter:1", new CounterReadModel { Value = 99 });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            ReadModelWritePolicy.UpdateAsync<CounterReadModel>(
                store,
                StoreName,
                "counter:1",
                current => new CounterReadModel { Value = (current?.Value ?? 0) + 1 },
                new ReadModelWriteContext(Category: "counter"),
                logger,
                maxAttempts: 3));

        ex.Message.ShouldContain("3 attempts");
    }

    [Fact]
    public async Task ApplyEventsAsync_AppliesEachEventToSeededValue() {
        var store = new InMemoryReadModelStore();
        ProjectionEventDto?[] events = [
            CreateEvent("created", "corr-1"),
            CreateEvent("incremented", "corr-1"),
        ];

        CounterReadModel result = await ReadModelWritePolicy.ApplyEventsAsync<CounterReadModel>(
            store,
            StoreName,
            "counter:1",
            events,
            () => new CounterReadModel(),
            (model, evt) => {
                model.Value++;
                model.AppliedEvents.Add(evt.EventTypeName);
            });

        result.Value.ShouldBe(2);
        result.AppliedEvents.ShouldBe(["created", "incremented"]);
    }

    [Fact]
    public async Task MergeAsync_MergesIncomingIntoExistingIndex() {
        var store = new InMemoryReadModelStore();
        store.SeedRaw(StoreName, "index", new CounterReadModel { Value = 1, AppliedEvents = ["a"] });

        CounterReadModel result = await ReadModelWritePolicy.MergeAsync<CounterReadModel>(
            store,
            StoreName,
            "index",
            new CounterReadModel { Value = 1, AppliedEvents = ["b"] },
            () => new CounterReadModel(),
            (existing, incoming) => new CounterReadModel {
                Value = existing.Value + incoming.Value,
                AppliedEvents = [.. existing.AppliedEvents, .. incoming.AppliedEvents],
            });

        result.Value.ShouldBe(2);
        result.AppliedEvents.ShouldBe(["a", "b"]);
    }

    private static ProjectionEventDto CreateEvent(string typeName, string correlationId) =>
        new(typeName, [], "json", 1, DateTimeOffset.UnixEpoch, correlationId);
}
