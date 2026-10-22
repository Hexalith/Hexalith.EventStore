using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Fact]
    public async Task MissingMarkerStatusCannotResumeInterruptedBatch()
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        JsonNode marker = JsonNode.Parse(client.ByteStoreValue(Scope().ComputeMarkerKey()))!;
        marker.AsObject().Remove("st").ShouldBeTrue();
        client.SeedByteStore(Scope().ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        int writes = client.TrySaveByteOperations.Count, deletes = client.TryDeleteByteOperations.Count;
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes); client.TryDeleteByteOperations.Count.ShouldBe(deletes);
    }

    [Fact]
    public async Task MatchExistingEnvelopeCannotLosePreviousValueOnNonDiscoveryKey()
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        client.SeedByteStore("index", "{}"u8.ToArray());
        string etag = (await new DaprReadModelBatchStateAccessor(client, Store).ReadAsync("index", TestContext.Current.CancellationToken)).ETag;
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("index", new IndexEntry(1), ReadModelBatchConcurrency.Match(etag))]) { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        JsonNode envelope = JsonNode.Parse(client.ByteStoreValue("index"))!; envelope["prev"] = null;
        client.SeedByteStore("index", JsonSerializer.SerializeToUtf8Bytes(envelope));
        int writes = client.TrySaveByteOperations.Count, deletes = client.TryDeleteByteOperations.Count;
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeFalse();
        client.TrySaveByteOperations.Count.ShouldBe(writes); client.TryDeleteByteOperations.Count.ShouldBe(deletes);
    }

    [Theory]
    [InlineData("operations")] [InlineData("key")] [InlineData("manifest")] [InlineData("lower")]
    public async Task RecoveryCeilingsCannotBeRaisedAndFailBeforeStateAccess(string limit)
    {
        var accessor = new RejectingBatchStateAccessor();
        var options = new ReadModelBatchOptions { MaxOperations = 200, MaxKeyByteLength = 1024, MaxCanonicalManifestBytes = 2_097_152 };
        ReadModelBatchOperation[] operations = limit == "operations"
            ? Enumerable.Range(0, 101).Select(i => ReadModelBatchOperation.Write("key" + i, new Detail(1), ReadModelBatchConcurrency.CreateOnly)).ToArray()
            : [ReadModelBatchOperation.Write(limit == "key" ? new string('k', 513) : "key", new { Value = new string('x', limit == "manifest" ? 1_048_576 : 1) }, ReadModelBatchConcurrency.CreateOnly)];
        if (limit == "lower") options.MaxKeyByteLength = 2;
        var batch = new ReadModelBatch(Scope(), operations) { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(accessor, options, NullLogger.Instance);
        await Should.ThrowAsync<ArgumentException>(() => protocol.ExecuteAsync(batch, null, TestContext.Current.CancellationToken));
        accessor.Accesses.ShouldBe(0);
    }

    [Fact]
    public async Task EscapedTopLevelDiscriminatorRemainsRecoverableWhileNestedNamesAreOrdinary()
    {
        using var client = new RecordingDaprClient(); var store = CreateStore(client);
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("detail", new Detail(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        string escaped = Encoding.UTF8.GetString(client.ByteStoreValue("detail")).Replace("$hxrmb", @"\u0024hxrmb", StringComparison.Ordinal);
        client.SeedByteStore("detail", Encoding.UTF8.GetBytes(escaped));
        (await store.GetAsync<Detail>(Store, "detail")).Value.ShouldBeNull();
        (await store.RecoverPendingAsync(Scope(), "detail")).IsResolved.ShouldBeTrue();
        (await store.GetAsync<Detail>(Store, "detail")).Value.ShouldBe(new Detail(1));
        ReadModelBatchEnvelope.IsEnvelope("{\"nested\":{\"\u0024hxrmb\":1},\"value\":\"$hxrmb\"}"u8).ShouldBeFalse();
    }
}
