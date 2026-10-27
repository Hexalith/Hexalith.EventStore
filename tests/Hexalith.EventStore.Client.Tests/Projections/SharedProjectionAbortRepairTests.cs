using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Checks certified repair against effective COW storage and interrupted normal drain.</summary>
public sealed class SharedProjectionAbortRepairTests
{
    private static readonly SharedProjectionScope s_scope = new("statestore", "tenant", "widget", "repair", ["writer"]);

    /// <summary>Completed repair survives a prepared or committed ordinary journal interruption.</summary>
    /// <param name="committed">Whether interruption follows the actual catch-up commit.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedRepairResumesAfterPreparedOrCommittedCatchUp(bool committed)
    {
        var store = new InMemoryReadModelStore();
        var states = new AbortRepairFaultStore(store);
        var batches = new AbortRepairBatchFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(states, batches);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "index"), Value(7));
        long epoch = await BeginAbortAsync(coordinator, s_scope, 2);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "metadata"), Value(99));
        var request = await RequestAsync(coordinator, s_scope, epoch, 0);
        await coordinator.RepairAbortMetadataAsync(s_scope, request);
        if (committed)
        {
            batches.AfterExecute = batch =>
            {
                if (batch.Operations.Any(operation => operation.Key == s_scope.PhysicalKey(0, "index")))
                {
                    batches.AfterExecute = null;
                    throw new IOException("Interrupted after committed catch-up");
                }
            };
        }
        else
        {
            states.InterruptAfterSave = value => value is SharedProjectionEpochState state
                && state.Journal.Any(item => item.Prepared is not null || item.PreparedChunks is not null);
        }
        if (committed)
            await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.AbortAsync(s_scope, "repair", Fold(coordinator, s_scope)));
        else
            await Should.ThrowAsync<IOException>(() => coordinator.AbortAsync(s_scope, "repair", Fold(coordinator, s_scope)));
        (await coordinator.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(2);
        var cold = new SharedProjectionEpochCoordinator(store, store);
        await cold.RepairAbortMetadataAsync(s_scope, request);
        await Should.ThrowAsync<InvalidOperationException>(() => cold.RepairAbortMetadataAsync(s_scope, request with { CertificateId = "foreign" }));
        await cold.AbortAsync(s_scope, "repair", Fold(cold, s_scope));
        (await cold.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        (await cold.ReadAsync<Dictionary<string, int>>(s_scope, "index")).Value!["value"].ShouldBe(9);
    }

    /// <summary>Nonzero selected generations hide repaired inherited metadata after process recreation.</summary>
    /// <param name="inherited">Whether metadata is inherited from the earlier generation.</param>
    /// <param name="jsonNull">Whether the physically present metadata is JSON null.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CopyOnWriteRepairDeletesEffectiveMetadataAndPreservesNullEvidence(bool inherited, bool jsonNull)
    {
        SharedProjectionScope scope = s_scope with { CopyOnWrite = true };
        var store = new InMemoryReadModelStore();
        var batches = new AbortRepairBatchFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, batches);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "seed", "seed", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "seed", Stream([Write("index", 7),
            ReadModelBatchOperation.Write("metadata", JsonDocument.Parse(jsonNull ? "null" : "{\"bad\":1}"), ReadModelBatchConcurrency.LastWrite)]));
        await coordinator.CommitAsync(scope, "seed");
        _ = await coordinator.CatchUpAsync(scope, Fold(coordinator, scope));
        _ = await coordinator.BeginAsync(scope, "advance", "advance", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "advance", Stream(inherited ? [Write("other", 1)] :
            [Write("other", 1), ReadModelBatchOperation.Write("metadata", JsonDocument.Parse(jsonNull ? "null" : "{\"bad\":2}"), ReadModelBatchConcurrency.LastWrite)]));
        await coordinator.CommitAsync(scope, "advance");
        _ = await coordinator.CatchUpAsync(scope, Fold(coordinator, scope));
        long epoch = await BeginAbortAsync(coordinator, scope, 1);
        var request = await RequestAsync(coordinator, scope, epoch, 2);
        request.Values[0].SourceGeneration.ShouldBe(inherited ? 1 : 2);
        request.Values[0].OriginalJson.ShouldBe(jsonNull ? "null" : inherited ? "{\"bad\":1}" : "{\"bad\":2}");
        batches.FailRepair = true;
        await Should.ThrowAsync<IOException>(() => coordinator.RepairAbortMetadataAsync(scope, request));
        var cold = new SharedProjectionEpochCoordinator(store, store);
        await cold.RepairAbortMetadataAsync(scope, request);
        (await cold.ReadGenerationJsonAsync(scope, 2, "metadata")).ETag.ShouldBeNull();
        await cold.AbortAsync(scope, "repair", Fold(cold, scope));
        (await new SharedProjectionEpochCoordinator(store, store).ReadGenerationAsync<Dictionary<string, int>>(scope, 2, "metadata")).Value.ShouldBeNull();
        (await cold.ReadAsync<Dictionary<string, int>>(scope, "index")).Value!["value"].ShouldBe(8);
        (await cold.GetAbortRepairAsync(scope)).ShouldNotBeNull().Request.Values[0].OriginalJson.ShouldBe(request.Values[0].OriginalJson);
    }

    /// <summary>Source mutation during the final metadata deletion cannot complete the reservation.</summary>
    [Fact]
    public async Task SourceMutationDuringFinalDeleteRetainsReservedFence()
    {
        var store = new InMemoryReadModelStore();
        var batches = new AbortRepairBatchFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, batches);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "index"), JsonDocument.Parse("null"));
        long epoch = await BeginAbortAsync(coordinator, s_scope, 1);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "metadata"), Value(99));
        var request = await RequestAsync(coordinator, s_scope, epoch, 0);
        request.SourceGuards[0].ContentDigest.ShouldBe(Convert.ToHexString(SHA256.HashData("null"u8)));
        batches.AfterExecute = _ => store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "index"), Value(8));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.RepairAbortMetadataAsync(s_scope, request));
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)).Value!;
        state.AbortRepairCompleted.ShouldBeFalse(); state.AbortRepair.ShouldNotBeNull();
        (await coordinator.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(1);
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(s_scope, Fold(coordinator, s_scope)));
    }

    /// <summary>Corrupt durable repair evidence is refused before status reads or phase changes.</summary>
    /// <param name="corruption">The sole corrupt persisted field.</param>
    [Theory]
    [InlineData("fingerprint")]
    [InlineData("epoch")]
    [InlineData("bounds")]
    [InlineData("source")]
    [InlineData("completed-without-receipt")]
    [InlineData("reserved-open")]
    public async Task CorruptPersistedRepairStateFailsClosed(string corruption)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "index"), Value(7));
        long epoch = await BeginAbortAsync(coordinator, s_scope, 1);
        store.SeedRaw(s_scope.StoreName, s_scope.PhysicalKey(0, "metadata"), Value(99));
        var request = await RequestAsync(coordinator, s_scope, epoch, 0);
        await coordinator.RepairAbortMetadataAsync(s_scope, request);
        var state = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)).Value!;
        var receipt = state.AbortRepair!;
        SharedProjectionAbortRepairRequest altered = corruption switch
        {
            "epoch" => request with { Epoch = epoch + 1 },
            "bounds" => request with { Values = Enumerable.Repeat(request.Values[0], 9).ToArray() },
            "source" => request with { SourceGuards = [request.SourceGuards[0] with { ContentDigest = "bad" }] },
            _ => request
        };
        receipt = new(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(altered))), altered);
        state = state with { AbortRepair = corruption == "fingerprint" ? receipt with { Fingerprint = "wrong" } : receipt };
        if (corruption == "completed-without-receipt") state = state with { AbortRepair = null };
        if (corruption == "reserved-open") state = state with { AbortRepairCompleted = false, Phase = SharedProjectionEpochPhase.Open };
        store.SeedRaw(s_scope.StoreName, s_scope.StateKey, state);
        string? etag = (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)).ETag;
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.GetStatusAsync(s_scope));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.GetAbortRepairAsync(s_scope));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadAsync<Dictionary<string, int>>(s_scope, "index"));
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(s_scope, "other", "other", new Dictionary<string, long>()));
        (await store.GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey)).ETag.ShouldBe(etag);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> Stream(IEnumerable<ReadModelBatchOperation> operations)
    {
        foreach (ReadModelBatchOperation operation in operations) yield return operation;
        await Task.CompletedTask;
    }

    private static Dictionary<string, int> Value(int value) => new() { ["value"] = value };
    private static ReadModelBatchOperation Write(string key, int value) => ReadModelBatchOperation.Write(key, Value(value), ReadModelBatchConcurrency.LastWrite);

    private static async Task<long> BeginAbortAsync(SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope, int deliveries)
    {
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        long epoch = await coordinator.BeginAsync(scope, "repair", "inventory", new Dictionary<string, long>());
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, "writer");
        for (int position = 1; position <= deliveries; position++)
            _ = await coordinator.JournalAsync(scope, lease, new("owner", position, [(byte)position], []));
        await Should.ThrowAsync<IOException>(() => coordinator.AbortAsync(scope, "repair", (_, _, _) => throw new IOException("metadata")));
        return epoch;
    }

    private static async Task<SharedProjectionAbortRepairRequest> RequestAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, long epoch, long generation)
    {
        var metadata = await coordinator.ReadGenerationJsonAsync(scope, generation, "metadata");
        var source = await coordinator.ReadGenerationJsonAsync(scope, generation, "index");
        return new("repair", "inventory", epoch, generation, "certificate",
            [new("metadata", metadata.ETag!, metadata.Value?.ToJsonString() ?? "null")
                { SourceGeneration = metadata.SourceGeneration, PointerETag = metadata.PointerETag }])
        {
            SourceGuards = [new("index", source.ETag ?? string.Empty, string.IsNullOrEmpty(source.ETag) ? null
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Value?.ToJsonString() ?? "null"))))
                { SourceGeneration = source.SourceGeneration, PointerETag = source.PointerETag }]
        };
    }

    private static Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> Fold(
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope)
        => async (_, generation, token) => [Write("index", (await coordinator.ReadGenerationAsync<Dictionary<string, int>>(scope, generation, "index", token)).Value!["value"] + 1)];
}
