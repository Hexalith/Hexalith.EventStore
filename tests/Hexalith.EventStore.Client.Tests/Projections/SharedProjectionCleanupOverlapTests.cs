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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerSequenceHandoffWaitsForThisCoordinatorsPromotedCleanup(bool pagedIdentity)
    {
        var store = new InMemoryReadModelStore();
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "handoff", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        byte[] digest = SharedProjectionCapturedEventIdentity.Compute("owner-a", 2, 1, [7]);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long> { ["owner-a"] = 2 });
        SharedProjectionCapturedIdentityReference? reference = null;
        if (pagedIdentity)
        {
            reference = await coordinator.CapturedIdentities.BeginAsync(scope, "first", "owner-a", 1);
            await coordinator.CapturedIdentities.AppendAsync(scope, "first", reference, 1, [2], digest, [string.Empty]);
            reference = await coordinator.CapturedIdentities.SealAsync(scope, "first", reference);
        }
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
        await coordinator.CommitAsync(scope, "first", requireOwnerSequenceHandoff: true);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task seed = reference is not null
            ? coordinator.SeedCapturedOwnerIdentityAsync(scope, "first", "owner-a", 1, reference)
            : coordinator.SeedCapturedOwnerSequenceAsync(scope, "first", "owner-a", 1, capturedEventDigests: digest);
        try
        {
            // The cleanup's state CAS recovers a pending epoch envelope, so the fenced handoff batch must not overlap it.
            seed.IsCompleted.ShouldBeFalse("the handoff must wait for this coordinator's pending promoted cleanup");
        }
        finally { release.TrySetResult(); }
        await seed.WaitAsync(TimeSpan.FromSeconds(10));
        store.BatchFaultHook = null;
        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(1);
        checkpoint.CapturedIdentityReference.ShouldBe(reference);
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.CleanupStreamPageCount.ShouldBe(0);
        static async IAsyncEnumerable<ReadModelBatchOperation> Rows()
        {
            yield return ReadModelBatchOperation.Write("row", new JsonObject { ["count"] = 1 }, ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }
}
