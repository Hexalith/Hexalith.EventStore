using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Qualifies typed refusal without acknowledging or dropping prepared shared deliveries.</summary>
public sealed class SharedProjectionWriteConflictTests
{
    /// <summary>Both exact owning-store conflict classifications survive; a cold lawful retry persists the selected value.</summary>
    [Theory]
    [InlineData(ReadModelBatchConflictKind.Identity)]
    [InlineData(ReadModelBatchConflictKind.Optimistic)]
    public async Task ConflictRetainsPreparedJournalUntilAColdRetryActuallyCompletes(ReadModelBatchConflictKind kind)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore(); var batches = Substitute.For<IReadModelBatchStore>();
        batches.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReadModelBatchResult(ReadModelBatchStatus.Conflict, "fixture", kind, null)));
        var scope = new SharedProjectionScope("statestore", "tenant-a", "folders", "scan-conflict", ["writer"]) { CopyOnWrite = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, batches);
        var lease = await coordinator.RegisterWriterAsync(scope, "writer", token);
        (await coordinator.JournalAsync(scope, lease, new("folder-a", 1, [1], [1], OwnerSequenceNumber: 1), token))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        var refusal = await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.CatchUpAsync(scope,
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([ReadModelBatchOperation.Write("view", new JsonObject { ["sequence"] = 1 }, ReadModelBatchConcurrency.LastWrite)]), token));
        refusal.InnerException!.Message.ShouldEndWith("Conflict/" + kind);
        (await coordinator.GetStatusAsync(scope, token)).PendingDeliveryCount.ShouldBe(1);
        (await store.GetAsync<JsonObject>(scope.StoreName, scope.PhysicalKey(0, "view"), token)).Value.ShouldBeNull();
        var cold = new SharedProjectionEpochCoordinator(store, store);
        await cold.CatchUpAsync(scope, (_, _, _) => throw new InvalidOperationException("Prepared bytes must be resumed without a new fold."), token);
        (await cold.GetStatusAsync(scope, token)).PendingDeliveryCount.ShouldBe(0);
        (await cold.ReadAsync<JsonObject>(scope, "view", token)).Value!["sequence"]!.GetValue<int>().ShouldBe(1);
    }
}
