using System.Text.Json;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionReviewThreeMaintenanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StalledFirstMaintenanceReadUsesSdkBudgetWhileCancelledBeginWaitersLeaveSharedWorkAlone(bool failImmediately, bool dependencyCancelled)
    {
        var observations = new ConcurrentQueue<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Name == "eventstore.projection.cleanup.failures") owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            observations.Enqueue("count=" + value);
            foreach (var tag in tags) observations.Enqueue(tag.Key + "=" + tag.Value);
        });
        listener.Start();
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { ReconciliationTimeout = TimeSpan.FromSeconds(1) }));
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "maintenance", ["writer"]);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Rows());
        var paused = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.AfterTrySaveByteState = (key, bytes) =>
        {
            if (key != scope.StateKey || ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) return;
            SharedProjectionEpochState? state = JsonSerializer.Deserialize<SharedProjectionEpochState>(bytes.Span, JsonSerializerOptions.Web);
            if (state is not { Phase: SharedProjectionEpochPhase.Open, CleanupStreamPageCount: > 0 }) return;
            client.AfterTrySaveByteState = null;
            client.BeforeReadByteStateAsync = async (_, token) =>
            {
                client.BeforeReadByteStateAsync = null;
                paused.TrySetResult(token);
                try
                {
                    if (dependencyCancelled) throw new OperationCanceledException("dependency cancelled independently");
                    if (failImmediately) throw new IOException("maintenance read failed");
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally { cancelled.TrySetResult(); }
            };
        };
        await coordinator.CommitAsync(scope, "first");
        CancellationToken maintenance = await paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        maintenance.CanBeCanceled.ShouldBeTrue();
        if (!failImmediately)
        {
        using var firstWait = new CancellationTokenSource(); using var secondWait = new CancellationTokenSource();
        Task<long> first = coordinator.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>(), firstWait.Token);
        Task<long> second = coordinator.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>(), secondWait.Token);
        firstWait.Cancel(); secondWait.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        await Should.ThrowAsync<OperationCanceledException>(() => second);
        if (!failImmediately) maintenance.IsCancellationRequested.ShouldBeFalse("abandoning callers cancel only their waits");
        }
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WaitForPromotedCleanupAsync(scope).WaitAsync(TimeSpan.FromSeconds(5));
        observations.Count.ShouldBe(3);
        observations.ShouldContain("count=1");
        observations.ShouldContain("reason=" + (failImmediately ? "failure" : "budget-expired"));
        observations.ShouldContain("disposition=foreground-retry");
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.CleanupStreamPageCount.ShouldBeGreaterThan(0);
        (await coordinator.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>())).ShouldBe(2);
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.CleanupStreamPageCount.ShouldBe(0);
        (await coordinator.ReadAsync<JsonObject>(scope, "row")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        static async IAsyncEnumerable<ReadModelBatchOperation> Rows()
        {
            yield return ReadModelBatchOperation.Write("row", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }
    [Fact]
    public async Task MaintenanceBudgetSetupFailureReleasesGateAndNextHealthyBeginReconcilesDurableCleanup()
    {
        var observations = new ConcurrentQueue<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Name == "eventstore.projection.cleanup.failures") owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            observations.Enqueue("count=" + value);
            foreach (var tag in tags) observations.Enqueue(tag.Key + "=" + tag.Value);
        });
        listener.Start();
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var options = new ReadModelBatchOptions { ReconciliationTimeout = TimeSpan.FromDays(100) };
        var store = new DaprReadModelStore(client, Options.Create(options));
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "budget-setup", ["writer"]);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Rows());
        await coordinator.CommitAsync(scope, "first");
        await coordinator.WaitForPromotedCleanupAsync(scope).WaitAsync(TimeSpan.FromSeconds(5));
        observations.ShouldContain("count=1");
        observations.ShouldContain("reason=failure");
        observations.ShouldContain("disposition=foreground-retry");
        observations.ShouldNotContain("reason=budget-expired");
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.CleanupStreamPageCount.ShouldBeGreaterThan(0);
        options.ReconciliationTimeout = TimeSpan.FromSeconds(30);
        (await coordinator.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>()).WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.CleanupStreamPageCount.ShouldBe(0);
        (await coordinator.ReadAsync<JsonObject>(scope, "row")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        static async IAsyncEnumerable<ReadModelBatchOperation> Rows()
        {
            yield return ReadModelBatchOperation.Write("row", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }

}
