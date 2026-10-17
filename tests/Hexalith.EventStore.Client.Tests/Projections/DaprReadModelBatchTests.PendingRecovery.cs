using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Fact]
    public async Task PendingConditionalManifestIsRecoveredAcrossAdapterRestartThenFreshConflictRetry()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        client.SeedByteStore("detail", JsonSerializer.SerializeToUtf8Bytes(new Detail(1)));
        client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(new IndexEntry(1)));
        var detail = await store.GetAsync<Detail>(Store, "detail");
        var index = await store.GetAsync<IndexEntry>(Store, "index");
        ReadModelBatch batch = new(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(2), ReadModelBatchConcurrency.Match(detail.ETag!)),
            ReadModelBatchOperation.Write("index", new IndexEntry(2), ReadModelBatchConcurrency.Match(index.ETag!))]) { RetainManifestForRecovery = true };
        client.BeforeTrySaveByteState = (key, _) => { if (key == "index") throw new IOException("outage"); };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        (await store.GetAsync<Detail>(Store, "detail")).Value!.Version.ShouldBe(1);
        (await store.GetAsync<IndexEntry>(Store, "index")).Value!.Count.ShouldBe(1);
        client.BeforeTrySaveByteState = null!;
        // A competing aggregate changed the shared index before our pending installation reached it.
        client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(new IndexEntry(3)));
        store = CreateStore(client);
        var recovered = await store.RecoverPendingAsync(Scope() with { BatchId = "unknown-original-etags" }, "detail");
        recovered.IsResolved.ShouldBeTrue(); recovered.WasRecovered.ShouldBeTrue();
        (await store.GetAsync<Detail>(Store, "detail")).Value!.Version.ShouldBe(1);
        (await store.GetAsync<IndexEntry>(Store, "index")).Value!.Count.ShouldBe(3);
        detail = await store.GetAsync<Detail>(Store, "detail"); index = await store.GetAsync<IndexEntry>(Store, "index");
        var retry = new ReadModelBatch(Scope() with { BatchId = "fresh-etags" },
            [ReadModelBatchOperation.Write("detail", new Detail(2), ReadModelBatchConcurrency.Match(detail.ETag!)),
                ReadModelBatchOperation.Write("index", new IndexEntry(4), ReadModelBatchConcurrency.Match(index.ETag!))]) { RetainManifestForRecovery = true };
        (await store.ExecuteAsync(retry)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<Detail>(Store, "detail")).Value!.Version.ShouldBe(2);
        (await store.GetAsync<IndexEntry>(Store, "index")).Value!.Count.ShouldBe(4);
    }

    [Fact]
    public async Task CommittedRecoveryFinishesOwnEnvelopeWithoutRollingBackNewerSharedIndex()
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        var batch = new ReadModelBatch(Scope(),
            [ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        client.BeforeTrySaveByteState = (key, value) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(value.Span)) throw new IOException("compaction outage");
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        var index = await store.GetAsync<IndexEntry>(Store, "index");
        index.Value!.Count.ShouldBe(1); index.ETag.ShouldNotBeNullOrEmpty();
        client.BeforeTrySaveByteState = null!;
        var later = new ReadModelBatch(Scope() with { AggregateId = "other-aggregate", BatchId = "later" },
            [ReadModelBatchOperation.Write("index", new IndexEntry(2), ReadModelBatchConcurrency.Match(index.ETag!)),
                ReadModelBatchOperation.Write("other-detail", new Detail(2), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        (await store.ExecuteAsync(later)).IsSuccess.ShouldBeTrue();
        byte[] newer = client.ByteStoreValue("index");
        store = CreateStore(client);
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeTrue();
        client.ByteStoreValue("index").ShouldBe(newer);
        (await store.GetAsync<Detail>(Store, "detail")).Value!.Version.ShouldBe(1);
        ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldBeNull();
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        client.ByteStoreValue("index").ShouldBe(newer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingKeyRecoveryCannotBypassGuardedOwnerMode(bool coordinated)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "owner", "generation", 1, scope.BatchId, scope.BatchId, "v1");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new
            { ProjectionOwner = owner, ProjectionVersion = "v1" }, ReadModelBatchConcurrency.CreateOnly)])
        { RetainManifestForRecovery = true, OwnerSelection = coordinated ? null : owner, OwnerSelections = coordinated ? [owner] : null };
        await Should.ThrowAsync<ArgumentException>(() => CreateStore(client).ExecuteAsync(batch));
        client.TrySaveByteOperations.ShouldBeEmpty();
        client.ByteStoreContains("detail").ShouldBeFalse();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("projection")]
    [InlineData("candidate")]
    [InlineData("ordinal")]
    [InlineData("fingerprint")]
    [InlineData("manifest")]
    [InlineData("status")]
    [InlineData("previous")]
    public async Task PendingRecoveryRefusesMismatchedScopeAndCorruptEvidenceWithoutWrites(string fault)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        ReadModelBatchScope requested = fault switch
        {
            "tenant" => Scope() with { TenantId = "other" }, "domain" => Scope() with { Domain = "other" },
            "aggregate" => Scope() with { AggregateId = "other" }, "projection" => Scope() with { ProjectionType = "other" }, _ => Scope(),
        };
        if (fault is "candidate" or "ordinal" or "fingerprint" or "previous")
        {
            JsonNode envelope = JsonNode.Parse(client.ByteStoreValue("detail"))!;
            if (fault == "previous") envelope["prev"] = "broken-base64";
            if (fault == "candidate") envelope["cand"] = Convert.ToBase64String("{}"u8.ToArray());
            if (fault == "ordinal") envelope["ord"] = 99;
            if (fault == "fingerprint") envelope["fp"] = "invalid";
            client.SeedByteStore("detail", JsonSerializer.SerializeToUtf8Bytes(envelope));
        }
        if (fault is "manifest" or "status")
        {
            JsonNode marker = JsonNode.Parse(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
            if (fault == "status") marker["st"] = 999;
            else marker["manifest"] = Convert.ToBase64String("{}"u8.ToArray());
            client.SeedByteStore(Scope().ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        }
        int writes = client.TrySaveByteOperations.Count;
        (await store.RecoverPendingAsync(requested, "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }
}
