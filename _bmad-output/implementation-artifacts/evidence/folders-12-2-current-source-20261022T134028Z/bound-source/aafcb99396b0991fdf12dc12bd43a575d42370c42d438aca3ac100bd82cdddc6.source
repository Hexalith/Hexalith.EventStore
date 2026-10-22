using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelBatchDeadlineTests
{
    private static readonly DateTimeOffset s_deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static ReadModelBatchScope Scope(string batch = "deadline") => new("statestore", "tenant", "widget", "owner", "detail", batch);

    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Fact]
    public void DeadlineIsVersionedImmutableAndRoundTripsWithoutChangingHistoricalConstructors()
    {
        ReadModelBatchOperation operation = ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.CreateOnly,
            expiresAtUtc: s_deadline.ToOffset(TimeSpan.FromHours(2)));
        operation.ExpiresAtUtc.ShouldBe(s_deadline);
        operation.ExpiresAtUtc!.Value.Offset.ShouldBe(TimeSpan.Zero);
        var batch = new ReadModelBatch(Scope(), [operation]) { RetainManifestForRecovery = true };
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        string fingerprint = ReadModelBatchFingerprint.Compute(batch);
        fingerprint.ShouldStartWith("v2:");
        ReadModelBatchFingerprint.IsCanonical(fingerprint).ShouldBeTrue();
        ReadModelBatch decoded = ReadModelBatchManifestCodec.Decode(manifest, batch.Scope, null, null, fingerprint,
            retainManifestForRecovery: true);
        ReadModelBatchFingerprint.BuildCanonicalManifest(decoded).ShouldBe(manifest);
        decoded.Operations.Single().ExpiresAtUtc.ShouldBe(s_deadline);
        operation.WithConcurrency(ReadModelBatchConcurrency.LastWrite).ExpiresAtUtc.ShouldBe(s_deadline);
        SharedProjectionMutation mutation = SharedProjectionMutation.FromOperation(operation);
        SharedProjectionMutation restored = JsonSerializer.Deserialize<SharedProjectionMutation>(JsonSerializer.SerializeToUtf8Bytes(mutation))!;
        restored.ToOperation("physical").ExpiresAtUtc.ShouldBe(s_deadline);
        ReadModelBatchFingerprint.Compute(new ReadModelBatch(Scope(), [operation.WithConcurrency(ReadModelBatchConcurrency.LastWrite)]))
            .ShouldNotBe(fingerprint);
        ReadModelBatchFingerprint.Compute(new ReadModelBatch(Scope(), [ReadModelBatchOperation.WriteExpiring("model", Value(1),
            ReadModelBatchConcurrency.CreateOnly, expiresAtUtc: s_deadline.AddTicks(1))])).ShouldNotBe(fingerprint);
        ReadModelBatchFingerprint.Compute(new ReadModelBatch(Scope(), [ReadModelBatchOperation.Write("model", Value(1),
            ReadModelBatchConcurrency.CreateOnly, TimeSpan.FromSeconds(1))])).ShouldStartWith("v1:");
        typeof(ReadModelBatchOperation).GetMethods().Single(method => method.Name == "Write" && method.GetParameters().Length == 4
            && method.GetParameters()[3].ParameterType == typeof(TimeSpan?)).ShouldNotBeNull();
        typeof(SharedProjectionMutation).GetConstructor([typeof(string), typeof(ReadModelBatchOperationKind), typeof(string),
            typeof(byte[]), typeof(TimeSpan?)]).ShouldNotBeNull();
        ReadModelBatchOperation.Write("default", Value(1), ReadModelBatchConcurrency.LastWrite, default).TimeToLive.ShouldBeNull();
        ReadModelBatchOperation.Write("null", Value(1), ReadModelBatchConcurrency.LastWrite, null).TimeToLive.ShouldBeNull();
        ReadModelBatchOperation.Write("relative", Value(1), ReadModelBatchConcurrency.LastWrite, TimeSpan.FromSeconds(1)).TimeToLive.ShouldBe(TimeSpan.FromSeconds(1));
        var historical = new SharedProjectionMutation("model", ReadModelBatchOperationKind.Write, "value", [123, 125], default);
        var (key, kind, type, bytes, ttl) = historical;
        key.ShouldBe("model"); kind.ShouldBe(ReadModelBatchOperationKind.Write); type.ShouldBe("value"); bytes.ShouldBe([123, 125]); ttl.ShouldBeNull();
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task FinalValueAndReceiptBecomeAbsentAtInclusiveDeadlineWithoutRefreshingOnRetry(ReadModelBatchStoreProfile profile)
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        store.BatchOptions.StoreProfiles["statestore"] = profile;
        var batch = new ReadModelBatch(Scope(),
        [
            ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: s_deadline),
            ReadModelBatchOperation.WriteExpiring("receipt", Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: s_deadline),
            ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite),
        ]);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "model")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        (await store.GetAsync<JsonObject>("statestore", "receipt")).Value.ShouldNotBeNull();
        clock.UtcNow = s_deadline;
        (await store.GetAsync<JsonObject>("statestore", "model")).Value.ShouldBeNull();
        (await store.GetAsync<JsonObject>("statestore", "receipt")).Value.ShouldBeNull();
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        (await store.GetAsync<JsonObject>("statestore", "checkpoint")).Value.ShouldNotBeNull();
        clock.UtcNow = s_deadline.AddDays(1);
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        (await store.GetAsync<JsonObject>("statestore", "model")).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task ExpiredUnconditionalCandidateNeverDeletesOrReplacesANewerRow(ReadModelBatchStoreProfile profile)
    {
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = new ProjectionDeadlineTimeProvider(s_deadline);
        store.BatchOptions.StoreProfiles["statestore"] = profile;
        store.SeedRaw("statestore", "model", Value(2));
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.WriteExpiring("model", Value(1),
            ReadModelBatchConcurrency.LastWrite, expiresAtUtc: s_deadline)]);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "model")).Value!["count"]!.GetValue<int>().ShouldBe(2);
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Fact]
    public async Task StagedBatchCrossingDeadlineRemovesOnlyItsOwnEnvelopeAndCompletesCheckpoint()
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddDays(-1));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        var batch = new ReadModelBatch(Scope(),
        [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: s_deadline),
            ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.LastWrite)]);
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.UtcNow = s_deadline;
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        store.HasPendingEnvelope("statestore", "model").ShouldBeFalse();
        store.Snapshot<JsonObject>("statestore", "model").ShouldBeNull();
        (await store.GetAsync<JsonObject>("statestore", "checkpoint")).Value.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation)]
    [InlineData(ReadModelBatchPhase.BeforeCommit)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    public async Task OriginalPendingManifestAndRecoveryAnchorCannotResurrectExpiredCandidates(ReadModelBatchPhase failure)
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddHours(-1));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        var batch = new ReadModelBatch(Scope(),
        [ReadModelBatchOperation.Write("checkpoint", Value(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.CreateOnly, expiresAtUtc: s_deadline)])
        { RetainManifestForRecovery = true };
        store.BatchFaultHook = (phase, _, _) => phase == failure ? throw new IOException("retained failure") : Task.CompletedTask;
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        store.BatchFaultHook = null;
        clock.UtcNow = s_deadline;
        string discovery = store.HasPendingEnvelope("statestore", "model") ? "model" : "checkpoint";
        ReadModelPendingBatchRecoveryResult recovery = await store.RecoverPendingAsync(Scope("replacement-process"), discovery);
        recovery.IsResolved.ShouldBeTrue(recovery.Reason);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "model")).Value.ShouldBeNull();
        store.Snapshot<JsonObject>("statestore", "model").ShouldBeNull();
        (await store.GetAsync<JsonObject>("statestore", "checkpoint")).Value.ShouldNotBeNull();
        (await store.RecoverPendingAsync(Scope("another-process"), discovery)).IsResolved.ShouldBeTrue();
    }

    [Fact]
    public async Task CompactionRaceAndLaterRecoveryPreserveConcurrentNewerValue()
    {
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddSeconds(-10));
        var store = new InMemoryReadModelStore();
        store.BatchOptions.TimeProvider = clock;
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite,
            expiresAtUtc: s_deadline)]);
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCompaction)
            {
                clock.UtcNow = s_deadline;
                store.SeedRaw("statestore", "model", Value(2));
            }
            return Task.CompletedTask;
        };
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        store.BatchFaultHook = null;
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        (await store.GetAsync<JsonObject>("statestore", "model")).Value!["count"]!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task DaprCompactionUsesRemainingRetentionAndDeadlineAwareBulkReads()
    {
        using var client = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddSeconds(-20));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        var store = new DaprReadModelStore(client, Options.Create(options));
        var batch = new ReadModelBatch(Scope(), [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite,
            expiresAtUtc: s_deadline)]);
        (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.UtcNow = s_deadline.AddSeconds(-3);
        (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        ReadModelBatchExpiringValue.Decode(client.ByteStoreValue("model")).Deadline.ShouldBe(s_deadline);
        client.TrySaveByteMetadata["model"]["ttlInSeconds"].ShouldBe("3");
        clock.UtcNow = s_deadline;
        (await store.GetManyAsync<JsonObject>("statestore", ["model"], 1)).Single().Value.ShouldBeNull();
    }
}
