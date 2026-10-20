using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionCleanupOverlapTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeginPreservesCommittedCleanupEvidenceUntilOriginalWorkerCompletes(bool freshCoordinator)
    {
        var store = new InMemoryReadModelStore();
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Rows());
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stops = 0;
        store.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeReceipt && Interlocked.Increment(ref stops) == 1)
            {
                paused.TrySetResult();
                await release.Task.WaitAsync(token);
            }
        };
        await coordinator.CommitAsync(scope, "first");
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        SharedProjectionEpochCoordinator next = freshCoordinator ? new(store, store) : coordinator;
        Task<long> begin = next.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>());
        try
        {
            if (freshCoordinator)
                await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => begin);
            else
                begin.IsCompleted.ShouldBeFalse("a second worker must not recreate this coordinator's pending cleanup batch");
            (await next.ReadAsync<JsonObject>(scope, "row")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        }
        finally { release.TrySetResult(); }
        await coordinator.WaitForPromotedCleanupAsync(scope).WaitAsync(TimeSpan.FromSeconds(10));
        store.BatchFaultHook = null;
        if (freshCoordinator) begin = next.BeginAsync(scope, "next", "next-inventory", new Dictionary<string, long>());
        (await begin).ShouldBe(2);
        SharedProjectionEpochStatus status = await next.GetStatusAsync(scope);
        status.ActiveGeneration.ShouldBe(1); status.IsBuilding.ShouldBeTrue();
        (await next.ReadAsync<JsonObject>(scope, "row")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        static async IAsyncEnumerable<ReadModelBatchOperation> Rows()
        {
            yield return ReadModelBatchOperation.Write("row", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }
}
