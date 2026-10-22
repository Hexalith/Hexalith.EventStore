using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelReviewThreeExpiryTests
{
    private static readonly DateTimeOffset s_deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static JsonObject Value(int count) => new() { ["count"] = count };
    private static ReadModelBatchScope Scope(string batch) => new("statestore", "tenant", "widget", "owner", "detail", batch);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OrdinaryCreateOnlyUsesExpiredPhysicalCasAndPreservesAReadToWriteWinner(bool fake, bool ttl)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline);
        var memory = new InMemoryReadModelStore(); memory.BatchOptions.TimeProvider = clock;
        IReadModelStore store = fake ? memory : new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        byte[] expired = ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(1)), s_deadline).ToArray();
        void Seed(byte[] bytes) { if (fake) memory.SeedRaw("statestore", "row", JsonNode.Parse(bytes)!); else client.SeedByteStore("row", bytes); }
        Seed(expired);
        (await store.GetAsync<JsonObject>("statestore", "row")).Value.ShouldBeNull();
        async Task<bool> Save() => ttl
            ? await ((IReadModelExpiringStore)store).TrySaveWithTimeToLiveAsync("statestore", "row", Value(2), "", TimeSpan.FromMinutes(1))
            : await store.TrySaveAsync("statestore", "row", Value(2), "");
        (await Save()).ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "row")).Value!["count"]!.GetValue<int>().ShouldBe(2);
        Seed(expired);
        byte[] winner = JsonSerializer.SerializeToUtf8Bytes(Value(99));
        if (fake) memory.ConcurrentWriteBeforeTrySave = () => { memory.ConcurrentWriteBeforeTrySave = null; Seed(winner); };
        else client.BeforeTrySaveByteState = (key, _) => { if (key == "row" && client.ETag?.Length > 0) { client.BeforeTrySaveByteState = null; Seed(winner); } };
        (await Save()).ShouldBeFalse();
        (fake ? memory.SnapshotRaw("statestore", "row") : client.ByteStoreValue("row")).ShouldBe(winner);
        (await store.TrySaveAsync("statestore", "row", Value(3), "stale")).ShouldBeFalse();
        Seed(expired);
        int folds = 0;
        JsonObject updated = await ReadModelWritePolicy.UpdateAsync<JsonObject>(store, "statestore", "row", previous =>
        { previous.ShouldBeNull(); folds++; return Value(3); });
        folds.ShouldBe(1); updated["count"]!.GetValue<int>().ShouldBe(3);
    }

    public static IEnumerable<object[]> ConditionalCases()
    {
        foreach (ReadModelBatchStoreProfile profile in Enum.GetValues<ReadModelBatchStoreProfile>())
            foreach (bool retained in new[] { false, true })
                foreach (bool create in new[] { false, true })
                    foreach (int ticks in new[] { -1, 0, 1 })
                        yield return [profile, retained, create, ticks];
    }

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public async Task FailedExpiredConditionalGuardNeverAdvancesCheckpointAndSettlesTerminal(
        ReadModelBatchStoreProfile profile, bool retained, bool create, int ticks)
    {
        using var client = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(ticks));
        var options = new ReadModelBatchOptions { TimeProvider = clock }; options.StoreProfiles["statestore"] = profile;
        var store = new DaprReadModelStore(client, Options.Create(options));
        byte[] winner = JsonSerializer.SerializeToUtf8Bytes(Value(99)); client.SeedByteStore("row", winner);
        var batch = new ReadModelBatch(Scope("guard"), [
            ReadModelBatchOperation.WriteExpiring("row", Value(1), create ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match("stale"), s_deadline),
            ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite)]) { RetainManifestForRecovery = retained };
        (await store.ExecuteAsync(batch)).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        client.ByteStoreValue("row").ShouldBe(winner); client.ByteStoreContains("checkpoint").ShouldBeFalse();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        ReadModelBatchMarker.ReadProof(client.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, false, ReadModelBatchStoreProfile.Resumable)]
    [InlineData(false, true, ReadModelBatchStoreProfile.Resumable)]
    [InlineData(true, false, ReadModelBatchStoreProfile.Resumable)]
    [InlineData(true, true, ReadModelBatchStoreProfile.Resumable)]
    [InlineData(false, false, ReadModelBatchStoreProfile.TransactionQualified)]
    [InlineData(false, true, ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task OmittedExpiredUnconditionalNeverHelpsOrWaitsForForeignPendingEnvelope(bool staged, bool retained, ReadModelBatchStoreProfile profile)
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline);
        var store = new InMemoryReadModelStore(); store.BatchOptions.TimeProvider = clock;
        var foreign = new ReadModelBatch(Scope("foreign"), [ReadModelBatchOperation.Write("row", Value(99), ReadModelBatchConcurrency.CreateOnly)]);
        (await store.StageAsync(foreign)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        byte[] bytes = store.SnapshotRaw("statestore", "row")!;
        byte[] marker = store.SnapshotRaw("statestore", foreign.Scope.ComputeMarkerKey())!;
        store.BatchOptions.StoreProfiles["statestore"] = profile;
        var batch = new ReadModelBatch(Scope("omitted"), [ReadModelBatchOperation.WriteExpiring("row", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline),
            ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite)]) { RetainManifestForRecovery = retained };
        if (staged)
        {
            (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
            (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        }
        else (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        store.SnapshotRaw("statestore", "row").ShouldBe(bytes);
        store.SnapshotRaw("statestore", foreign.Scope.ComputeMarkerKey()).ShouldBe(marker);
        store.Snapshot<JsonObject>("statestore", "checkpoint")!["count"]!.GetValue<int>().ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyInstalledConditionalProofSurvivesExpiryBeforeStagedCommit(bool retained)
    {
        using var client = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        var batch = new ReadModelBatch(Scope("installed"), [
            ReadModelBatchOperation.WriteExpiring("row", Value(1), ReadModelBatchConcurrency.CreateOnly, s_deadline),
            ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = retained };
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.UtcNow = s_deadline;
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        client.ByteStoreContains("row").ShouldBeFalse();
        (await store.GetAsync<JsonObject>("statestore", "checkpoint")).Value.ShouldNotBeNull();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task BulkFinalWrappersDecodeWithoutAdditionalReadsAndPreserveOrderAndEtags()
    {
        using var client = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(s_deadline);
        client.SeedByteStore("expired", ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(1)), s_deadline));
        client.SeedByteStore("live", ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(2)), s_deadline.AddTicks(1)));
        string physicalETag = (await client.GetByteStateAndETagAsync("statestore", "live", null, null, default)).Item2;
        int reads = 0; client.BeforeReadByteState = _ => reads++;
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        IReadOnlyList<ReadModelBulkEntry<JsonObject>> rows = await store.GetManyAsync<JsonObject>("statestore", ["live", "expired", "missing"], 2);
        reads.ShouldBe(0); client.BulkStateCallCount.ShouldBe(1);
        rows.Select(row => row.Key).ShouldBe(["live", "expired", "missing"]);
        rows[0].Value!["count"]!.GetValue<int>().ShouldBe(2); rows[0].ETag.ShouldBe(physicalETag);
        rows[1].Value.ShouldBeNull(); rows[1].ETag.ShouldBeNull(); rows[2].Value.ShouldBeNull();
    }

    [Fact]
    public async Task ProductionRegistrationUsesConfiguredClockForActualSelectedExpiry()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var services = new ServiceCollection(); services.AddSingleton<Dapr.Client.DaprClient>(client);
        services.AddEventStoreReadModelStore(options => options.TimeProvider = clock);
        services.AddEventStoreReadModelStore();
        await using ServiceProvider provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IReadModelBatchStore>();
        var coordinator = provider.GetRequiredService<SharedProjectionEpochCoordinator>();
        var scope = new SharedProjectionScope("statestore", "tenant", "widget", "clock", ["writer"]) { CopyOnWrite = true };
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Writes());
        await coordinator.CommitAsync(scope, "first");
        (await coordinator.ReadAsync<JsonObject>(scope, "row")).Value.ShouldNotBeNull();
        clock.UtcNow = s_deadline;
        (await coordinator.ReadAsync<JsonObject>(scope, "row")).Value.ShouldBeNull();
        await coordinator.WaitForPromotedCleanupAsync(scope);
        static async IAsyncEnumerable<ReadModelBatchOperation> Writes()
        {
            yield return ReadModelBatchOperation.WriteExpiring("row", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline);
            await Task.Yield();
        }
    }
}
