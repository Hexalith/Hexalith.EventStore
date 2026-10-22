using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelExpiryReviewRegressionTests
{
    private static readonly DateTimeOffset s_deadline = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static ReadModelBatchScope Scope(string batch) => new("statestore", "tenant", "widget", "owner", "detail", batch);
    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified)]
    public async Task CreateOnlyReplacesLogicallyExpiredPhysicalValueUnderItsActualCas(ReadModelBatchStoreProfile profile)
    {
        using var client = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddSeconds(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = profile;
        var store = new DaprReadModelStore(client, Options.Create(options));
        var old = new ReadModelBatch(Scope("old"), [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.CreateOnly, s_deadline)]);
        (await store.ExecuteAsync(old)).IsSuccess.ShouldBeTrue();
        clock.UtcNow = s_deadline;
        client.ByteStoreContains("model").ShouldBeTrue();
        var batch = new ReadModelBatch(Scope("replacement"), [ReadModelBatchOperation.WriteExpiring("model", Value(2), ReadModelBatchConcurrency.CreateOnly, s_deadline.AddMinutes(1))]);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "model")).Value!["count"]!.GetValue<int>().ShouldBe(2);
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation)]
    [InlineData(ReadModelBatchPhase.BeforeCommit)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    public async Task RetainedCreateOnlyOverExpiredPhysicalBytesRecoversOriginalBatchRepeatedly(ReadModelBatchPhase failure)
    {
        using var client = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(s_deadline);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        client.SeedByteStore("model", ReadModelBatchExpiringValue.Encode(JsonSerializer.SerializeToUtf8Bytes(Value(1)), s_deadline));
        var batch = new ReadModelBatch(Scope("replacement"), [ReadModelBatchOperation.WriteExpiring("model", Value(2), ReadModelBatchConcurrency.CreateOnly, s_deadline.AddMinutes(1))]) { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, "statestore"), options, NullLogger.Instance);
        var fault = new DelegatingBatchFaultInjector((phase, _, _) => phase == failure ? throw new InvalidOperationException("stop") : Task.CompletedTask);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch, fault, CancellationToken.None));
        var store = new DaprReadModelStore(client, Options.Create(options));
        (await store.RecoverPendingAsync(Scope("new-process"), "model")).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(Scope("new-process-again"), "model")).IsResolved.ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", "model")).Value!["count"]!.GetValue<int>().ShouldBe(2);
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable, false)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, false)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, true)]
    public async Task FinalPersistenceUsesOneRemainingRetentionSampleAcrossInclusiveDeadline(ReadModelBatchStoreProfile profile, bool anchor)
    {
        using var client = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = profile;
        var batch = new ReadModelBatch(Scope("moving-clock"), [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline)]) { RetainManifestForRecovery = anchor };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, "statestore"), options, NullLogger.Instance);
        ReadModelBatchPhase trigger = anchor ? ReadModelBatchPhase.RecoveryManifestReclaimed
            : profile == ReadModelBatchStoreProfile.TransactionQualified ? ReadModelBatchPhase.BeforeCommit : ReadModelBatchPhase.BeforeCompaction;
        bool armed = true;
        var fault = new DelegatingBatchFaultInjector((phase, _, _) =>
        {
            if (phase == trigger && armed)
            {
                armed = false;
                int samples = 0;
                clock.Read = () => Interlocked.Increment(ref samples) == 1 ? s_deadline.AddTicks(-1) : s_deadline;
            }
            return Task.CompletedTask;
        });
        (await protocol.ExecuteAsync(batch, fault, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        var store = new DaprReadModelStore(client, Options.Create(options));
        (await store.GetAsync<JsonObject>("statestore", "model")).Value.ShouldBeNull();
        foreach (var metadata in client.TrySaveByteMetadata.Values)
            if (metadata is not null && metadata.TryGetValue("ttlInSeconds", out string? ttl)) int.Parse(ttl, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalDomainExpiryPropertyIsOrdinaryJsonWithOrWithoutDeadline(bool expiring)
    {
        using var client = new RecordingDaprClient();
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        JsonObject value = new() { ["$hxrme"] = new JsonObject { ["nested"] = 1 }, ["expires"] = "domain", ["nested"] = new JsonObject { ["$hxrme"] = 7, ["$hxrmb"] = 3 } };
        ReadModelBatchOperation operation = expiring ? ReadModelBatchOperation.WriteExpiring("model", value, ReadModelBatchConcurrency.LastWrite, s_deadline)
            : ReadModelBatchOperation.Write("model", value, ReadModelBatchConcurrency.LastWrite);
        (await store.ExecuteAsync(new ReadModelBatch(Scope("domain"), [operation]))).IsSuccess.ShouldBeTrue();
        JsonNode.DeepEquals((await store.GetAsync<JsonObject>("statestore", "model")).Value, value).ShouldBeTrue();
        (await store.GetManyAsync<JsonObject>("statestore", ["model"], 1)).Single().Value.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("\"2\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("2.5")]
    [InlineData("2147483648")]
    public void InvalidEnvelopeVersionReturnsNull(string version)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"$hxrmb\":" + version + ",\"scope\":\"x\",\"fp\":null,\"ord\":0,\"del\":false,\"prev\":null,\"cand\":\"e30=\",\"expires\":null}");
        ReadModelBatchEnvelope.FromBytes(bytes).ShouldBeNull();
    }

    [Fact]
    public void MissingDuplicateNullAndMalformedEnvelopeEvidenceReturnsNull()
    {
        string fingerprint = ReadModelBatchFingerprint.Compute(new ReadModelBatch(Scope("decode"), [ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.CreateOnly, s_deadline)]));
        var original = new ReadModelBatchEnvelope { EnvelopeVersion = 2, ScopeHash = Scope("decode").ComputeScopeHash(), Fingerprint = fingerprint, CandidateBase64 = "e30=", ExpiresAtUtc = s_deadline };
        foreach (string name in new[] { "fp", "expires", "scope", "cand" })
        {
            JsonObject root = JsonNode.Parse(original.ToBytes().Span)!.AsObject();
            root[name] = null;
            ReadModelBatchEnvelope.FromBytes(JsonSerializer.SerializeToUtf8Bytes(root)).ShouldBeNull();
            root.Remove(name);
            ReadModelBatchEnvelope.FromBytes(JsonSerializer.SerializeToUtf8Bytes(root)).ShouldBeNull();
        }
        string json = System.Text.Encoding.UTF8.GetString(original.ToBytes().Span);
        ReadModelBatchEnvelope.FromBytes(System.Text.Encoding.UTF8.GetBytes(json.Replace("\"$hxrmb\":2", "\"$hxrmb\":2,\"$hxrmb\":2", StringComparison.Ordinal))).ShouldBeNull();
        foreach (string malformed in new[] { "not-base64", Convert.ToBase64String("{"u8), Convert.ToBase64String("{\"$hxrmb\":3}"u8) })
        {
            original.CandidateBase64 = malformed;
            ReadModelBatchEnvelope.FromBytes(original.ToBytes()).ShouldBeNull();
            original.CandidateBase64 = "e30="; original.PreviousBase64 = malformed;
            ReadModelBatchEnvelope.FromBytes(original.ToBytes()).ShouldBeNull();
            original.PreviousBase64 = null;
        }
    }
    [Fact]
    public async Task LaterTransactionPreflightCrossingDeadlineOmitsEarlierCandidateBeforeDispatch()
    {
        using var client = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var clock = new ProjectionDeadlineTimeProvider(s_deadline.AddTicks(-1));
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = ReadModelBatchStoreProfile.TransactionQualified;
        client.BeforeReadByteState = key => { if (key == "later") clock.UtcNow = s_deadline; };
        var store = new DaprReadModelStore(client, Options.Create(options));
        var batch = new ReadModelBatch(Scope("multi-operation"),
            [ReadModelBatchOperation.WriteExpiring("earlier", Value(1), ReadModelBatchConcurrency.LastWrite, s_deadline),
                ReadModelBatchOperation.Write("later", Value(2), ReadModelBatchConcurrency.CreateOnly)]);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        client.ByteStoreContains("earlier").ShouldBeFalse();
        client.TransactionOperations.Any(operation => operation.Key == "earlier").ShouldBeFalse();
        (await store.GetAsync<JsonObject>("statestore", "later")).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task MalformedDuplicateAndNestedSdkExpiryEvidenceFailsClosed()
    {
        using var client = new RecordingDaprClient();
        var store = new DaprReadModelStore(client);
        byte[] valid = ReadModelBatchExpiringValue.Encode("{}"u8.ToArray(), s_deadline).ToArray();
        string text = System.Text.Encoding.UTF8.GetString(valid);
        List<byte[]> invalid = [System.Text.Encoding.UTF8.GetBytes(text.Replace("\"$hxrmb\":3", "\"$hxrmb\":3,\"$hxrmb\":3", StringComparison.Ordinal)),
            System.Text.Encoding.UTF8.GetBytes(text.Replace("\"expires\":", "\"expires\":0,\"expires\":", StringComparison.Ordinal)),
            ReadModelBatchExpiringValue.Encode(valid, s_deadline).ToArray()];
        foreach (string property in new[] { "expires", "val", "$hxrmb" })
        {
            JsonObject value = JsonNode.Parse(valid)!.AsObject(); value[property] = "invalid";
            invalid.Add(JsonSerializer.SerializeToUtf8Bytes(value));
        }
        foreach (byte[] raw in invalid)
        {
            client.SeedByteStore("invalid", raw);
            await Should.ThrowAsync<JsonException>(() => store.GetAsync<JsonObject>("statestore", "invalid"));
        }
    }

}
