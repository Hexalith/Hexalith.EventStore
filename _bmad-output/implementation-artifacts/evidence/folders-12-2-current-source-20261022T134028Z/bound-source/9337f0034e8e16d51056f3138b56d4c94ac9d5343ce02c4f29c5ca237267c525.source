using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ContentFreeCompletion_MixedRoutesRetainEachOriginalReceiptDiscriminator(ReadModelBatchStoreProfile profile)
    {
        using var backend = new RecordingDaprClient();
        var store = CreateStore(backend, profile);
        var scope = Scope() with { ProjectionType = "rebuild" };
        var modern = CompletionBatch().OwnerSelections![0];
        var legacy = modern with { ProjectionName = "index", ReceiptProjectionName = "rebuild" };
        var owners = new[] { modern, legacy };
        var batch = new ReadModelBatch(scope, owners.Select(owner => ReadModelBatchOperation.Write(
            "tenant-1:counter:agg-1:" + owner.ProjectionName,
            new { ProjectionVersion = owner.FreshnessVersion, ProjectionOwner = owner }, ReadModelBatchConcurrency.CreateOnly)))
            { OwnerSelections = owners };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        int writes = backend.TrySaveByteOperations.Count;
        foreach (var owner in owners)
        {
            var proof = await store.ReadOwnerProofAsync(new(scope.TenantId, scope.Domain, scope.AggregateId, owner.ProjectionName, "catalog"), new(Store, owner.ProjectionName));
            proof.Proof.ShouldNotBeNull().Selection.ShouldBe(owner);
            proof.Proof.ReceiptFingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        }
        backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("batch")]
    [InlineData("owner-identity")]
    [InlineData("owner-generation")]
    [InlineData("owner-route")]
    public async Task ContentFreeCompletion_ValidDigestCannotAuthorizeAChangedOriginalIdentity(string change)
    {
        using var backend = new RecordingDaprClient();
        var store = CreateStore(backend);
        var batch = CompletionBatch();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        string key = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        var receipt = ReadModelBatchCompletionProof.Read(backend.ByteStoreValue(key)).ShouldNotBeNull().ToReceipt();
        if (change == "scope") receipt.ScopeHash = Scope().ComputeScopeHash();
        if (change == "batch") receipt.BatchId = "other-original-batch";
        var owner = receipt.OwnerSelections![0];
        owner = change switch
        {
            "owner-identity" => owner with { OwnerIdentity = "other-owner" },
            "owner-generation" => owner with { OwnerGeneration = "other-generation" },
            "owner-route" => owner with { ProjectionName = "other-route" },
            _ => owner,
        };
        receipt.OwnerSelections = [owner];
        byte[] replaced = JsonSerializer.SerializeToUtf8Bytes(ReadModelBatchCompletionProof.From(receipt), JsonSerializerOptions.Web);
        ReadModelBatchCompletionProof.Read(replaced).ShouldNotBeNull();
        backend.SeedByteStore(key, replaced);
        int writes = backend.TrySaveByteOperations.Count;
        backend.BeforeReadByteState = read => (read == "tenant-1:counter:agg-1:detail" || read == key).ShouldBeTrue();
        (await CompletionOwnerAsync(store, batch)).Proof.ShouldBeNull();
        backend.ByteStoreValue(key).ShouldBe(replaced);
        backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ContentFreeCompletion_CompletionBudgetRefusesAReachableNearLimitControlRecordBeforeAnyEffects(ReadModelBatchStoreProfile profile)
    {
        using var backend = new RecordingDaprClient();
        var scope = Scope() with { ProjectionType = "rebuild" };
        ReadModelBatch Build(int padding)
        {
            var selected = CompletionBatch().OwnerSelections![0] with { OwnerGeneration = new string('g', padding) };
            return new(scope, [ReadModelBatchOperation.Write("x", new
                { ProjectionVersion = selected.FreshnessVersion, ProjectionOwner = selected }, ReadModelBatchConcurrency.CreateOnly)])
                { OwnerSelections = [selected] };
        }
        ReadModelBatchMarker Prepared(ReadModelBatch batch) => new()
        {
            ScopeHash = scope.ComputeScopeHash(), BatchId = scope.BatchId, Fingerprint = ReadModelBatchFingerprint.Compute(batch),
            Status = ReadModelBatchMarkerStatus.Prepared, OwnerSelections = batch.OwnerSelections,
            Operations = [new() { Ordinal = 0, Key = "x", IsDelete = false }],
            OriginalManifestKey = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()),
        };
        int baseSize = JsonSerializer.SerializeToUtf8Bytes(Prepared(Build(1)), JsonSerializerOptions.Web).Length;
        var batch = Build(ReadModelBatchMarker.MaxControlBytes - baseSize + 1);
        var prepared = Prepared(batch);
        JsonSerializer.SerializeToUtf8Bytes(prepared, JsonSerializerOptions.Web).Length.ShouldBe(ReadModelBatchMarker.MaxControlBytes);
        prepared.Status = ReadModelBatchMarkerStatus.Completed;
        prepared.Operations = null; prepared.OriginalManifestKey = null;
        prepared.TerminalTimeUtc = DateTimeOffset.MaxValue.ToString("O", CultureInfo.InvariantCulture);
        JsonSerializer.SerializeToUtf8Bytes(ReadModelBatchCompletionProof.From(prepared), JsonSerializerOptions.Web).Length
            .ShouldBeGreaterThan(ReadModelBatchMarker.MaxControlBytes);
        backend.BeforeReadByteState = _ => throw new Xunit.Sdk.XunitException("Oversized completion touched storage.");
        var error = await Should.ThrowAsync<ArgumentException>(() => CreateStore(backend, profile).ExecuteAsync(batch));
        error.Message.ShouldContain("completion evidence");
        backend.TrySaveByteOperations.ShouldBeEmpty();
        backend.TryDeleteByteOperations.ShouldBeEmpty();
        backend.ExecuteStateTransactionCallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ContentFreeCompletion_PublicationRechecksOutputsAfterEarlierVerification(ReadModelBatchStoreProfile profile)
    {
        using var backend = new RecordingDaprClient();
        var store = CreateStore(backend, profile);
        var batch = CompletionBatch();
        string completion = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        int verifiedOutputReads = 0;
        bool lost = false;
        backend.BeforeReadByteState = key =>
        {
            if (key == "rich-output" && backend.ByteStoreContains(batch.Scope.ComputeMarkerKey())
                && JsonNode.Parse(backend.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!["st"]!.GetValue<int>() == (int)ReadModelBatchMarkerStatus.Completed)
                verifiedOutputReads++;
            if (key == completion && !lost)
            {
                verifiedOutputReads.ShouldBeGreaterThan(0);
                lost = true;
                backend.SeedByteStore("rich-output", "{\"lost\":true}"u8.ToArray());
            }
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        lost.ShouldBeTrue();
        JsonNode.Parse(backend.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!["st"]!.GetValue<int>().ShouldBe((int)ReadModelBatchMarkerStatus.Completed);
        backend.ByteStoreContains(completion).ShouldBeFalse();
        backend.BeforeReadByteState = key => (key == "tenant-1:counter:agg-1:detail" || key == completion).ShouldBeTrue();
        int writes = backend.TrySaveByteOperations.Count;
        (await CompletionOwnerAsync(store, batch)).Proof.ShouldBeNull();
        backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable, false, false)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, false, true)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, true, false)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, true, true)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, false, false)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, false, true)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, true, false)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, true, true)]
    public async Task ContentFreeCompletion_PublicationTransportAndCancellationReconcileTheSameOriginalReceipt(
        ReadModelBatchStoreProfile profile, bool cancellation, bool afterPersistence)
    {
        using var backend = new RecordingDaprClient();
        using var cancelled = new CancellationTokenSource();
        var batch = CompletionBatch();
        string key = ReadModelBatchKeys.CompletionKey(batch.Scope.ComputeScopeHash());
        bool faulted = false;
        void Fail(string write, ReadOnlyMemory<byte> _)
        {
            if (write != key || afterPersistence && faulted) return;
            faulted = true;
            if (cancellation) { cancelled.Cancel(); throw new OperationCanceledException(cancelled.Token); }
            throw new Dapr.DaprException("Completion publication response lost.");
        }
        if (afterPersistence) backend.AfterTrySaveByteState = Fail; else backend.BeforeTrySaveByteState = Fail;
        var result = await CreateStore(backend, profile).ExecuteAsync(batch, cancelled.Token);
        faulted.ShouldBeTrue();
        result.IsSuccess.ShouldBe(afterPersistence);
        string originalTime = JsonNode.Parse(backend.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!["time"]!.GetValue<string>();
        byte[]? original = backend.ByteStoreValue(key)?.ToArray();
        backend.BeforeReadByteState = read => (read == "tenant-1:counter:agg-1:detail" || read == key).ShouldBeTrue();
        int writes = backend.TrySaveByteOperations.Count;
        ((await CompletionOwnerAsync(CreateStore(backend, profile), batch)).Proof is not null).ShouldBe(afterPersistence);
        backend.TrySaveByteOperations.Count.ShouldBe(writes);
        backend.BeforeReadByteState = null; backend.BeforeTrySaveByteState = null; backend.AfterTrySaveByteState = null;
        (await CreateStore(backend, profile).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        var proof = ReadModelBatchCompletionProof.Read(backend.ByteStoreValue(key)).ShouldNotBeNull();
        proof.TerminalTimeUtc.ShouldBe(originalTime);
        proof.Owners.ShouldBe(batch.OwnerSelections!);
        if (original is not null) backend.ByteStoreValue(key).ShouldBe(original);
        byte[] completed = backend.ByteStoreValue(key).ToArray();
        (await CreateStore(backend, profile).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        backend.ByteStoreValue(key).ShouldBe(completed);
    }

    [Theory]
    [InlineData(false, "live")]
    [InlineData(false, "expired")]
    [InlineData(false, "malformed")]
    [InlineData(false, "pending")]
    [InlineData(true, "live")]
    [InlineData(true, "expired")]
    [InlineData(true, "malformed")]
    [InlineData(true, "pending")]
    public async Task CompactedRead_ExpiryWrappersAndEnvelopeRefusalRemainReadOnly(bool inMemory, string state)
    {
        using var backend = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        DateTimeOffset deadline = clock.UtcNow.AddSeconds(1);
        var fake = new InMemoryReadModelStore(); fake.BatchOptions.TimeProvider = clock;
        IReadModelStore store = inMemory ? fake : new DaprReadModelStore(backend, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        string marker = Scope().ComputeMarkerKey();
        void Seed(string key, ReadOnlyMemory<byte> bytes)
        {
            if (inMemory) { using var document = JsonDocument.Parse(bytes); fake.SeedRaw(Store, key, document); }
            else backend.SeedByteStore(key, bytes);
        }
        byte[]? Raw(string key) => inMemory ? fake.SnapshotRaw(Store, key) : backend.ByteStoreValue(key)?.ToArray();
        if (state == "pending")
        {
            var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("expiry-model", new { Count = 1 }, ReadModelBatchConcurrency.CreateOnly)]);
            (await ((IReadModelBatchStagingStore)store).StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        }
        else
        {
            var wrapper = JsonNode.Parse(ReadModelBatchExpiringValue.Encode("{\"count\":1}"u8.ToArray(), deadline).Span)!.AsObject();
            if (state == "malformed") wrapper["val"] = "not-base64!";
            Seed("expiry-model", JsonSerializer.SerializeToUtf8Bytes(wrapper));
        }
        Seed(marker, "{\"mustNotRead\":true}"u8.ToArray());
        byte[] before = Raw("expiry-model")!; byte[] markerBefore = Raw(marker)!;
        int writes = backend.TrySaveByteOperations.Count, deletes = backend.TryDeleteByteOperations.Count, count = fake.Count;
        backend.BeforeReadByteState = key => key.ShouldBe("expiry-model");
        fake.ConcurrentWriteBeforeTrySave = () => throw new Xunit.Sdk.XunitException("Compacted read attempted a write.");
        fake.ConcurrentWriteBeforeTryErase = () => throw new Xunit.Sdk.XunitException("Compacted read attempted an erase.");
        if (state == "expired") clock.UtcNow = deadline;
        if (state == "malformed") await Should.ThrowAsync<JsonException>(() => store.GetCompactedAsync<JsonObject>(Store, "expiry-model"));
        else if (state == "pending") await Should.ThrowAsync<InvalidOperationException>(() => store.GetCompactedAsync<JsonObject>(Store, "expiry-model"));
        else
        {
            var read = await store.GetCompactedAsync<JsonObject>(Store, "expiry-model");
            if (state == "live") { read.Value!["count"]!.GetValue<int>().ShouldBe(1); read.ETag.ShouldNotBeNullOrWhiteSpace(); }
            else { read.Value.ShouldBeNull(); read.ETag.ShouldBeNull(); }
        }
        Raw("expiry-model").ShouldBe(before); Raw(marker).ShouldBe(markerBefore);
        backend.TrySaveByteOperations.Count.ShouldBe(writes); backend.TryDeleteByteOperations.Count.ShouldBe(deletes); fake.Count.ShouldBe(count);
    }
}
