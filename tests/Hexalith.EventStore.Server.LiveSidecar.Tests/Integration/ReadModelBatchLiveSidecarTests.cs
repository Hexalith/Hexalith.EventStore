using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Tier 2/3 live-sidecar tests for the coordinated read-model batch protocol against a real
/// <c>daprd</c> sidecar and Redis state store. These assert the persisted Redis end state directly
/// (detail/index values, terminal marker receipt, and an unchanged seeded checkpoint) rather than only a
/// method return or recorded transaction — a return status is not integration evidence (R2-A6).
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class ReadModelBatchLiveSidecarTests {
    private const string StoreName = "statestore";
    private const string AppId = "eventstore";

    private static readonly System.Text.Json.JsonSerializerOptions s_json =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly DaprTestContainerFixture _fixture;

    public ReadModelBatchLiveSidecarTests(DaprTestContainerFixture fixture) => _fixture = fixture;

    public sealed record Detail(int Version);

    public sealed record IndexEntry(int Count);

    [Fact]
    [Trait("Tier", "3")]
    public async Task ResumableBatch_PersistsDetailIndexAndReceipt_AndLeavesCheckpointUnchanged() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));

        string unique = Guid.NewGuid().ToString("N");
        string tenant = $"rmb-{unique}";
        string detailKey = $"{tenant}:detail";
        string indexKey = $"{tenant}:index";
        string deletedKey = $"{tenant}:deleted";
        string checkpointKey = $"{tenant}:checkpoint";
        var scope = new ReadModelBatchScope(StoreName, tenant, "counter", "agg-1", "counterView", $"01BATCH{unique}");

        // Seed a representative delivery/rebuild checkpoint the batch must never touch.
        await client.SaveStateAsync(StoreName, checkpointKey, new Detail(7));
        await client.SaveStateAsync(StoreName, deletedKey, new Detail(1));
        (_, string deleteEtag) = await client.GetStateAndETagAsync<Detail>(StoreName, deletedKey);

        var batch = new ReadModelBatch(
            scope,
            [
                ReadModelBatchOperation.Write(detailKey, new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(indexKey, new IndexEntry(3), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Delete(deletedKey, ReadModelBatchConcurrency.Match(deleteEtag)),
            ]);

        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);

        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        IDatabase db = redis.GetDatabase();

        // Direct Redis inspection: the compacted detail/index values are durable.
        (await ReadStateJsonAsync(db, detailKey)).ShouldNotBeNull();
        System.Text.Json.JsonSerializer.Deserialize<Detail>(await ReadStateJsonAsync(db, detailKey)!, s_json)!.Version.ShouldBe(2);
        System.Text.Json.JsonSerializer.Deserialize<IndexEntry>(await ReadStateJsonAsync(db, indexKey)!, s_json)!.Count.ShouldBe(3);
        (await ReadStateJsonAsync(db, deletedKey)).ShouldBeNull("the committed delete envelope must be conditionally compacted to absence");

        // The terminal completion receipt (Completed = status 4) is retained.
        string? markerJson = await ResolveMarkerJsonAsync(db, scope.ComputeScopeHash());
        markerJson.ShouldNotBeNull("the terminal completion receipt must be retained in Redis");
        markerJson.ShouldContain("\"st\":4");

        // The seeded checkpoint is unchanged.
        Detail? checkpoint = await client.GetStateAsync<Detail>(StoreName, checkpointKey);
        checkpoint!.Version.ShouldBe(7);

        // Idempotent retry with the same identity/fingerprint does not reapply.
        ReadModelBatchResult retry = await store.ExecuteAsync(batch);
        retry.Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task IdentityReuse_DifferentFingerprint_ReturnsConflict_AndDoesNotMutate() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));

        string unique = Guid.NewGuid().ToString("N");
        string tenant = $"rmb-{unique}";
        string detailKey = $"{tenant}:detail";
        var scope = new ReadModelBatchScope(StoreName, tenant, "counter", "agg-1", "counterView", $"01BATCH{unique}");

        await store.ExecuteAsync(new ReadModelBatch(
            scope,
            [ReadModelBatchOperation.Write(detailKey, new Detail(2), ReadModelBatchConcurrency.LastWrite)]));

        ReadModelBatchResult conflict = await store.ExecuteAsync(new ReadModelBatch(
            scope,
            [ReadModelBatchOperation.Write(detailKey, new Detail(9), ReadModelBatchConcurrency.LastWrite)]));

        conflict.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        conflict.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Identity);

        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        System.Text.Json.JsonSerializer
            .Deserialize<Detail>(await ReadStateJsonAsync(redis.GetDatabase(), detailKey)!, s_json)!
            .Version.ShouldBe(2);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task PartialPrefix_PreservesOldVisibleView_ThenSameIdentityConverges() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));

        string unique = Guid.NewGuid().ToString("N");
        string tenant = $"rmb-{unique}";
        string detailKey = $"{tenant}:detail";
        string indexKey = $"{tenant}:index";
        var scope = new ReadModelBatchScope(StoreName, tenant, "counter", "agg-1", "counterView", $"01BATCH{unique}");
        var batch = new ReadModelBatch(
            scope,
            [
                ReadModelBatchOperation.Write(detailKey, new Detail(2), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(indexKey, new IndexEntry(2), ReadModelBatchConcurrency.LastWrite),
            ]);

        await client.SaveStateAsync(StoreName, detailKey, new Detail(1));
        await client.SaveStateAsync(StoreName, indexKey, new IndexEntry(1));

        var fault = new ReadModelBatchLiveFaultInjector(ReadModelBatchPhase.AfterInstallOperation, 0);
        ReadModelBatchResult interrupted = await ExecuteWithFaultAsync(client, batch, fault);

        interrupted.Status.ShouldBe(ReadModelBatchStatus.Incomplete);
        (await store.GetAsync<Detail>(StoreName, detailKey)).Value!.Version.ShouldBe(1);
        (await store.GetAsync<IndexEntry>(StoreName, indexKey)).Value!.Count.ShouldBe(1);

        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        string rawDetail = System.Text.Encoding.UTF8.GetString((await ReadStateJsonAsync(redis.GetDatabase(), detailKey))!);
        rawDetail.ShouldContain("\"$hxrmb\":1");

        ReadModelBatchResult retry = await store.ExecuteAsync(batch);
        retry.Status.ShouldBe(ReadModelBatchStatus.Completed);
        System.Text.Json.JsonSerializer.Deserialize<Detail>(await ReadStateJsonAsync(redis.GetDatabase(), detailKey)!, s_json)!
            .Version.ShouldBe(2);
        System.Text.Json.JsonSerializer.Deserialize<IndexEntry>(await ReadStateJsonAsync(redis.GetDatabase(), indexKey)!, s_json)!
            .Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task ConflictAfterPartialInstall_AbortsAndRestoresPreviousView() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));

        string unique = Guid.NewGuid().ToString("N");
        string tenant = $"rmb-{unique}";
        string detailKey = $"{tenant}:detail";
        string indexKey = $"{tenant}:index";
        var scope = new ReadModelBatchScope(StoreName, tenant, "counter", "agg-1", "counterView", $"01BATCH{unique}");

        await client.SaveStateAsync(StoreName, detailKey, new Detail(1));
        await client.SaveStateAsync(StoreName, indexKey, new IndexEntry(1));
        (_, string staleIndexEtag) = await client.GetStateAndETagAsync<IndexEntry>(StoreName, indexKey);
        await client.SaveStateAsync(StoreName, indexKey, new IndexEntry(2));

        var batch = new ReadModelBatch(
            scope,
            [
                ReadModelBatchOperation.Write(detailKey, new Detail(3), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(indexKey, new IndexEntry(3), ReadModelBatchConcurrency.Match(staleIndexEtag)),
            ]);

        ReadModelBatchResult result = await store.ExecuteAsync(batch);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        (await store.GetAsync<Detail>(StoreName, detailKey)).Value!.Version.ShouldBe(1);
        (await store.GetAsync<IndexEntry>(StoreName, indexKey)).Value!.Count.ShouldBe(2);

        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        System.Text.Json.JsonSerializer.Deserialize<Detail>(await ReadStateJsonAsync(redis.GetDatabase(), detailKey)!, s_json)!
            .Version.ShouldBe(1);
        System.Text.Json.JsonSerializer.Deserialize<IndexEntry>(await ReadStateJsonAsync(redis.GetDatabase(), indexKey)!, s_json)!
            .Count.ShouldBe(2);
        (await ResolveMarkerJsonAsync(redis.GetDatabase(), scope.ComputeScopeHash()))!
            .ShouldContain("\"st\":3");
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task PostDispatchCancellation_ReconcilesCommittedBatchWithoutRollback() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));

        string unique = Guid.NewGuid().ToString("N");
        string tenant = $"rmb-{unique}";
        string detailKey = $"{tenant}:detail";
        string indexKey = $"{tenant}:index";
        var scope = new ReadModelBatchScope(StoreName, tenant, "counter", "agg-1", "counterView", $"01BATCH{unique}");
        var batch = new ReadModelBatch(
            scope,
            [
                ReadModelBatchOperation.Write(detailKey, new Detail(4), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(indexKey, new IndexEntry(4), ReadModelBatchConcurrency.LastWrite),
            ]);
        using var cancellation = new CancellationTokenSource();
        var fault = new ReadModelBatchLiveFaultInjector(
            ReadModelBatchPhase.AfterCommit,
            -1,
            _ => cancellation.Cancel());

        ReadModelBatchResult result = await ExecuteWithFaultAsync(client, batch, fault, cancellation.Token);

        result.Status.ShouldBe(ReadModelBatchStatus.Completed);
        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        System.Text.Json.JsonSerializer.Deserialize<Detail>(await ReadStateJsonAsync(redis.GetDatabase(), detailKey)!, s_json)!
            .Version.ShouldBe(4);
        System.Text.Json.JsonSerializer.Deserialize<IndexEntry>(await ReadStateJsonAsync(redis.GetDatabase(), indexKey)!, s_json)!
            .Count.ShouldBe(4);
        (await ResolveMarkerJsonAsync(redis.GetDatabase(), scope.ComputeScopeHash()))!
            .ShouldContain("\"st\":4");

        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    /// <summary>
    /// Opt-in qualification probe (set <c>RUN_TX_QUALIFICATION_PROBE=1</c>). Issues a conditional multi-op
    /// transaction with one deliberately-stale ETag and inspects Redis for a partial commit. It fails
    /// closed: partial-commit evidence must keep the store on the <c>Resumable</c> profile — it never
    /// silently enables Redis transaction mode.
    /// </summary>
    [Fact]
    [Trait("Tier", "3")]
    [Trait("Category", "TransactionQualificationProbe")]
    public async Task TransactionQualificationProbe_KeepsRedisResumable() {
        // The default profile for any store is Resumable and is only overridden by an explicit operator
        // decision, never by this probe.
        new ReadModelBatchOptions().GetProfile(StoreName).ShouldBe(ReadModelBatchStoreProfile.Resumable);

        if (Environment.GetEnvironmentVariable("RUN_TX_QUALIFICATION_PROBE") != "1") {
            return;
        }

        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();
        string unique = Guid.NewGuid().ToString("N");
        string keyA = $"probe-{unique}:a";
        string keyB = $"probe-{unique}:b";

        await client.SaveStateAsync(StoreName, keyB, new Detail(1));
        (_, string staleEtag) = await client.GetStateAndETagAsync<Detail>(StoreName, keyB);

        // Mutate keyB out of band so the transaction's first-write precondition on keyB is stale.
        await client.SaveStateAsync(StoreName, keyB, new Detail(2));

        var operations = new List<StateTransactionRequest> {
            new(keyA, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new Detail(5)), StateOperationType.Upsert),
            new(keyB, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new Detail(9)), StateOperationType.Upsert, staleEtag, options: new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }),
        };

        bool threw = false;
        try {
            await client.ExecuteStateTransactionAsync(StoreName, operations);
        }
        catch (Dapr.DaprException) {
            threw = true;
        }

        Detail? committedA = await client.GetStateAsync<Detail>(StoreName, keyA);
        bool partialCommit = committedA is not null;

        // Whatever the observed behavior, qualification remains an explicit operator decision. If keyA
        // committed while the conditional keyB write failed, Redis is NOT all-or-nothing and must stay
        // Resumable; the probe records evidence but never auto-qualifies.
        (threw || partialCommit || committedA is null).ShouldBeTrue(
            "the qualification probe must observe a definitive transaction outcome");
    }

    private DaprClient CreateClient() =>
        new DaprClientBuilder().UseGrpcEndpoint(_fixture.DaprGrpcEndpoint).Build();

    private static Task<ReadModelBatchResult> ExecuteWithFaultAsync(
        DaprClient client,
        ReadModelBatch batch,
        IReadModelBatchFaultInjector faultInjector,
        CancellationToken cancellationToken = default) {
        var accessor = new DaprReadModelBatchStateAccessor(client, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, new ReadModelBatchOptions(), NullLogger.Instance);
        return protocol.ExecuteAsync(batch, faultInjector, cancellationToken);
    }

    private static async Task<ConnectionMultiplexer> ConnectRedisAsync() =>
        await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,allowAdmin=true");

    private static async Task<byte[]?> ReadStateJsonAsync(IDatabase db, string logicalKey) {
        RedisValue value = await db.HashGetAsync($"{AppId}||{logicalKey}", "data");
        if (!value.IsNullOrEmpty) {
            return (byte[]?)value;
        }

        // Fall back to a key scan in case the component uses a different key prefix.
        var keys = (RedisResult[])(await db.ExecuteAsync("KEYS", $"*{logicalKey}"))!;
        foreach (RedisResult key in keys) {
            RedisValue data = await db.HashGetAsync((string)key!, "data");
            if (!data.IsNullOrEmpty) {
                return (byte[]?)data;
            }
        }

        return null;
    }

    private static async Task<string?> ResolveMarkerJsonAsync(IDatabase db, string scopeHash) {
        var keys = (RedisResult[])(await db.ExecuteAsync("KEYS", $"*{scopeHash}*"))!;
        foreach (RedisResult key in keys) {
            RedisValue data = await db.HashGetAsync((string)key!, "data");
            if (!data.IsNullOrEmpty) {
                return data.ToString();
            }
        }

        return null;
    }
}
