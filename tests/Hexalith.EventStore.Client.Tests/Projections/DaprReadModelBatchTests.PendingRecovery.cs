using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Fact]
    public async Task GenericRecoveryRefusesLegacyRelativeTtlWithoutMutatingBytes()
    {
        using var client = new RecordingDaprClient();
        var operation = ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly, TimeSpan.FromMinutes(1));
        var batch = new ReadModelBatch(Scope(), [operation]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!;
        byte[] before = client.ByteStoreValue("detail");
        int writes = client.TrySaveByteOperations.Count;
        (await CreateStore(client).RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.ByteStoreValue("detail").ShouldBe(before);
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("first-compaction")]
    [InlineData("receipt")]
    [InlineData("anchor")]
    public async Task LegacyCommittedRecoverySurvivesRestartAndLeavesWritableEtags(string interruption)
    {
        using var client = new RecordingDaprClient();
        var batch = new ReadModelBatch(Scope(),
            [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(Scope().ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
        bool injected = false;
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (!injected && (interruption == "first-compaction" && key == "detail"
                || interruption == "receipt" && key == Scope().ComputeMarkerKey()
                || interruption == "anchor" && key == "index" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)))
            {
                injected = true;
                throw new IOException("recovery interrupted");
            }
        };
        ReadModelPendingBatchRecoveryResult first = await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), "detail");
        first.IsResolved.ShouldBe(interruption == "none", first.ToString());
        injected.ShouldBe(interruption != "none");
        client.BeforeTrySaveByteState = null!;
        // Recreated adapter has no original caller manifest, values, types or ETags.
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        var detail = await CreateStore(client).GetAsync<Detail>(Store, "detail");
        var index = await CreateStore(client).GetAsync<IndexEntry>(Store, "index");
        detail.Value!.Version.ShouldBe(1); detail.ETag.ShouldNotBeNullOrEmpty();
        index.Value!.Count.ShouldBe(1); index.ETag.ShouldNotBeNullOrEmpty();
        var tail = new ReadModelBatch(Scope() with { BatchId = "tail" },
            [ReadModelBatchOperation.Write("detail", new Detail(2), ReadModelBatchConcurrency.Match(detail.ETag!)),
                ReadModelBatchOperation.Write("index", new IndexEntry(2), ReadModelBatchConcurrency.Match(index.ETag!))]);
        (await CreateStore(client).ExecuteAsync(tail)).IsSuccess.ShouldBeTrue();
        (await CreateStore(client).GetAsync<Detail>(Store, "detail")).Value!.Version.ShouldBe(2);
        string receipt = System.Text.Encoding.UTF8.GetString(client.ByteStoreValue(Scope().ComputeMarkerKey()));
        receipt.ShouldNotContain("\"cand\":"); receipt.ShouldNotContain("\"prev\":"); receipt.ShouldNotContain("manifest");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LegacyMixedDeleteRecoverySurvivesInterruptionAndPersistsAbsence(bool finalAnchor, bool afterDelete)
    {
        using var client = new RecordingDaprClient();
        string deletedKey = finalAnchor ? "index" : "detail";
        string writtenKey = finalAnchor ? "detail" : "index";
        client.SeedByteStore(deletedKey, "{\"old\":true}"u8.ToArray());
        string etag = (await CreateStore(client).GetAsync<Detail>(Store, deletedKey)).ETag!;
        ReadModelBatchOperation deletion = ReadModelBatchOperation.Delete(deletedKey, ReadModelBatchConcurrency.Match(etag));
        ReadModelBatchOperation write = ReadModelBatchOperation.Write(writtenKey, new Detail(2), ReadModelBatchConcurrency.CreateOnly);
        var batch = new ReadModelBatch(Scope(), finalAnchor ? [write, deletion] : [deletion, write]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == writtenKey && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        client.TryDeleteByteOverride = _ => throw new IOException("after commit");
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(Scope().ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Committed);
        client.BeforeTrySaveByteState = null!; client.TryDeleteByteOverride = null!;
        bool interrupted = false;
        if (afterDelete)
            client.AfterTryDeleteByteState = key => { if (key == deletedKey) { interrupted = true; throw new IOException("delete response lost"); } };
        else
            client.TryDeleteByteOverride = key => { if (key == deletedKey) { interrupted = true; throw new IOException("before delete"); } return null; };
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey())).IsResolved.ShouldBeFalse();
        interrupted.ShouldBeTrue();
        client.ByteStoreContains(deletedKey).ShouldBe(!afterDelete);
        client.AfterTryDeleteByteState = null!; client.TryDeleteByteOverride = null!;
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        client.ByteStoreContains(deletedKey).ShouldBeFalse();
        (await CreateStore(client).GetAsync<Detail>(Store, deletedKey)).Value.ShouldBeNull();
        client.ByteStoreValue(writtenKey).ShouldBe(write.CanonicalValue.ToArray());
        // A second process sees physical absence, with no tombstone/envelope resurrection.
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        client.ByteStoreContains(deletedKey).ShouldBeFalse();
        client.ByteStoreValue(writtenKey).ShouldBe(write.CanonicalValue.ToArray());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task LegacyCompactionCasRacePreservesExactReplacementAcrossRestart(bool finalAnchor, bool foreign, bool delete)
    {
        using var client = new RecordingDaprClient();
        string target = finalAnchor ? "index" : "detail";
        client.SeedByteStore(target, "{\"old\":true}"u8.ToArray());
        string etag = (await CreateStore(client).GetAsync<Detail>(Store, target)).ETag!;
        ReadModelBatchOperation targetOperation = delete
            ? ReadModelBatchOperation.Delete(target, ReadModelBatchConcurrency.Match(etag))
            : ReadModelBatchOperation.Write(target, new Detail(1), ReadModelBatchConcurrency.Match(etag));
        ReadModelBatchOperation other = ReadModelBatchOperation.Write(finalAnchor ? "detail" : "index", new Detail(2), ReadModelBatchConcurrency.CreateOnly);
        var batch = new ReadModelBatch(Scope(), finalAnchor ? [other, targetOperation] : [targetOperation, other]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        client.TryDeleteByteOverride = _ => throw new IOException("after commit");
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!; client.TryDeleteByteOverride = null!;
        byte[] replacement = "{\"erased\":true,\"replacement\":\"exact bytes\"}"u8.ToArray();
        if (foreign)
        {
            using var otherClient = new RecordingDaprClient();
            var foreignBatch = new ReadModelBatch(Scope() with { AggregateId = "foreign", BatchId = "foreign" },
                [ReadModelBatchOperation.Write(target, new Detail(9), ReadModelBatchConcurrency.CreateOnly)]);
            (await CreateStore(otherClient).StageAsync(foreignBatch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
            replacement = otherClient.ByteStoreValue(target);
        }
        int participantReads = 0;
        bool replaced = false;
        client.BeforeReadByteState = key => { if (key == target) participantReads++; };
        void ReplaceAfterRead(string key)
        {
            if (key != target || replaced) return;
            participantReads.ShouldBeGreaterThan(0);
            ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue(key))!.ScopeHash.ShouldBe(Scope().ComputeScopeHash());
            replaced = true;
            client.SeedByteStore(key, replacement);
        }
        client.BeforeTrySaveByteState = (key, _) => ReplaceAfterRead(key);
        client.TryDeleteByteOverride = key => { ReplaceAfterRead(key); return null; };
        ReadModelPendingBatchRecoveryResult recovered = await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey());
        replaced.ShouldBeTrue();
        recovered.IsResolved.ShouldBe(!(finalAnchor && foreign));
        client.ByteStoreValue(target).ShouldBe(replacement);
        client.BeforeReadByteState = null!; client.BeforeTrySaveByteState = null!; client.TryDeleteByteOverride = null!;
        ReadModelPendingBatchRecoveryResult restarted = await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), Scope().ComputeMarkerKey());
        restarted.IsResolved.ShouldBe(!(finalAnchor && foreign));
        client.ByteStoreValue(target).ShouldBe(replacement);
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("aborting")]
    [InlineData("guarded")]
    [InlineData("retained")]
    [InlineData("ordinal")]
    [InlineData("expiring")]
    public async Task LegacyCommittedRecoveryRefusesUnsupportedEvidenceWithoutWrites(string fault)
    {
        using var client = new RecordingDaprClient();
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!;
        JsonNode marker = JsonNode.Parse(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
        if (fault == "prepared") marker["st"] = (int)ReadModelBatchMarkerStatus.Prepared;
        if (fault == "aborting") marker["st"] = (int)ReadModelBatchMarkerStatus.Aborting;
        if (fault == "guarded") marker["owners"] = new JsonArray();
        if (fault == "retained") marker["retainedParticipantCount"] = 1;
        if (fault == "ordinal") marker["ops"]![0]!["ord"] = 9;
        if (fault == "expiring") marker["fp"] = "v2:" + marker["fp"]!.GetValue<string>()[3..];
        client.SeedByteStore(Scope().ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        int writes = client.TrySaveByteOperations.Count;
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task LegacyCommittedRecoveryNeverReplaysOverSupersedingErasureOrForeignEnvelope()
    {
        using var client = new RecordingDaprClient();
        var batch = new ReadModelBatch(Scope(),
            [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!;
        byte[] erased = "{\"erased\":true}"u8.ToArray();
        client.SeedByteStore("detail", erased);
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), "index")).IsResolved.ShouldBeTrue();
        client.ByteStoreValue("detail").ShouldBe(erased);
    }

    [Fact]
    public async Task LegacyRecoveryPreservesForeignPendingEnvelopeBytes()
    {
        using var client = new RecordingDaprClient();
        var batch = new ReadModelBatch(Scope(),
            [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.CreateOnly)]);
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "detail" && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("after commit");
        };
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null!;
        client.SeedByteStore("detail", JsonSerializer.SerializeToUtf8Bytes(new Detail(9)));
        var foreign = new ReadModelBatch(Scope() with { AggregateId = "foreign", BatchId = "foreign" },
            [ReadModelBatchOperation.Write("detail", new Detail(10), ReadModelBatchConcurrency.LastWrite)]);
        (await CreateStore(client).StageAsync(foreign)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        byte[] foreignBytes = client.ByteStoreValue("detail");
        (await CreateStore(client).RecoverNonExpiringPendingAsync(Scope(), "index")).IsResolved.ShouldBeTrue();
        client.ByteStoreValue("detail").ShouldBe(foreignBytes);
    }

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
    public async Task PendingKeyRecoveryRequiresSingleOwnerAndStillRejectsCoordinatedOwnerMode(bool coordinated)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "owner", "generation", 1, scope.BatchId, scope.BatchId, "v1");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new
            { ProjectionOwner = owner, ProjectionVersion = "v1" }, ReadModelBatchConcurrency.CreateOnly)])
        { RetainManifestForRecovery = true, OwnerSelection = coordinated ? null : owner, OwnerSelections = coordinated ? [owner] : null };
        if (coordinated)
        {
            await Should.ThrowAsync<ArgumentException>(() => CreateStore(client).ExecuteAsync(batch));
            client.TrySaveByteOperations.ShouldBeEmpty();
            client.ByteStoreContains("detail").ShouldBeFalse();
        }
        else
        {
            (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
            ReadModelBatchEnvelope.FromBytes(client.ByteStoreValue("detail")).ShouldBeNull();
            ReadModelBatchMarker.ReadProof(client.ByteStoreValue(scope.ComputeMarkerKey()))!.OwnerSelection.ShouldBe(owner);
        }
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
