using Dapr.Client;

using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>
/// Deterministic coordinated-batch tests against the DAPR adapter over the stateful
/// <see cref="RecordingDaprClient"/>. These prove exact transaction request shape/bytes and that success
/// follows read-back proof rather than a void response; the live-sidecar lane is authoritative for
/// persisted DAPR/Redis end state.
/// </summary>
public class DaprReadModelBatchTests {
    private const string Store = "statestore";

    public sealed record Detail(int Version);

    public sealed record IndexEntry(int Count);

    private static ReadModelBatchScope Scope() =>
        new(Store, "tenant-1", "counter", "agg-1", "counterView", "01J0BATCH0000000000000000");

    private static ReadModelBatch WriteBatch() =>
        new(
            Scope(),
            [
                ReadModelBatchOperation.Write("detail:agg-1", new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("index:counterView", new IndexEntry(2), ReadModelBatchConcurrency.LastWrite),
            ]);

    private static DaprReadModelStore CreateStore(RecordingDaprClient client, ReadModelBatchStoreProfile? profile = null) {
        var options = new ReadModelBatchOptions();
        if (profile is not null) {
            options.StoreProfiles[Store] = profile.Value;
        }

        return new DaprReadModelStore(client, Options.Create(options));
    }

    [Fact]
    public async Task ExecuteAsync_ResumableProfile_PersistsCanonicalValuesAndRetainsReceipt() {
        var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch());

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value!.Version.ShouldBe(2);
        (await store.GetAsync<IndexEntry>(Store, "index:counterView")).Value!.Count.ShouldBe(2);

        string markerKey = ReadModelBatchKeys.MarkerKey(Scope().ComputeScopeHash());
        client.ByteStoreContains(markerKey).ShouldBeTrue("the terminal completion receipt must be retained");
        client.ByteStoreValue("detail:agg-1")
            .ShouldBe(ReadModelBatchCanonicalJson.Serialize(new Detail(2)).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_TransactionQualifiedProfile_IssuesExactlyOneOrderedTransaction() {
        var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client, ReadModelBatchStoreProfile.TransactionQualified);

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch());

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        client.ExecuteStateTransactionCallCount.ShouldBe(1);

        IReadOnlyList<StateTransactionRequest> operations = client.TransactionOperations;
        operations.Count.ShouldBe(3); // two logical operations plus terminal completion evidence
        operations[0].Key.ShouldBe("detail:agg-1");
        operations[0].OperationType.ShouldBe(StateOperationType.Upsert);
        operations[0].Value.ShouldBe(ReadModelBatchCanonicalJson.Serialize(new Detail(2)).ToArray());
        operations[1].Key.ShouldBe("index:counterView");
        operations[2].Key.ShouldBe(ReadModelBatchKeys.MarkerKey(Scope().ComputeScopeHash()));
    }

    [Fact]
    public async Task ExecuteAsync_TransactionQualifiedProfile_AmbiguousDispatch_DoesNotReportSuccess() {
        var client = new RecordingDaprClient {
            ExecuteStateTransactionException = new Dapr.DaprException("ambiguous transaction dispatch"),
        };
        DaprReadModelStore store = CreateStore(client, ReadModelBatchStoreProfile.TransactionQualified);

        ReadModelBatchResult result = await store.ExecuteAsync(WriteBatch());

        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ReadModelBatchStatus.Incomplete);
        (await store.GetAsync<Detail>(Store, "detail:agg-1")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CompletedRetry_ReturnsAlreadyCompleted() {
        var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        _ = await store.ExecuteAsync(WriteBatch());

        ReadModelBatchResult retry = await store.ExecuteAsync(WriteBatch());

        retry.Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_IdentityReuse_DifferentFingerprint_ReturnsIdentityConflict() {
        var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        _ = await store.ExecuteAsync(WriteBatch());

        ReadModelBatch different = new(
            Scope(),
            [ReadModelBatchOperation.Write("detail:agg-1", new Detail(9), ReadModelBatchConcurrency.LastWrite)]);
        ReadModelBatchResult result = await store.ExecuteAsync(different);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Identity);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeDispatch_Throws() {
        var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => store.ExecuteAsync(WriteBatch(), source.Token));
    }
}
