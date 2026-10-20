using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Qualifies physical retention and replaced-coordinator recovery against disposable fixture-owned Dapr/Redis state.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests(DaprTestContainerFixture fixture) : IAsyncLifetime
{
    private ConnectionMultiplexer _redis = null!;
    private HashSet<RedisKey> _before = [];

    public async ValueTask InitializeAsync()
    {
        fixture.ThrowIfHostStopped();
        _redis = await ConnectionMultiplexer.ConnectAsync(DaprTestContainerFixture.RedisEndpoint);
        _before = Keys();
        TestContext.Current.TestOutputHelper!.WriteLine($"fixture-app-id={fixture.AppId}; disposable-namespace={fixture.AppId}||*; replacement-scope=new-Dapr-clients-stores-coordinators");
    }

    public async ValueTask DisposeAsync()
    {
        HashSet<RedisKey> owned = Keys();
        owned.ExceptWith(_before);
        if (owned.Count > 0) await _redis.GetDatabase().KeyDeleteAsync(owned.ToArray());
        Keys().Except(_before).ShouldBeEmpty("all newly created fixture-owned keys must be removed after qualification");
        TestContext.Current.TestOutputHelper!.WriteLine($"fixture-app-id={fixture.AppId}; newly-owned-keys-removed={owned.Count}; newly-owned-keys-remaining=0");
        await _redis.DisposeAsync();
    }

    private HashSet<RedisKey> Keys() => [.. _redis.GetServer(_redis.GetEndPoints().Single()).Keys(pattern: fixture.AppId + "||*")];
    private RedisKey Key(string logical) => fixture.AppId + "||" + logical;
    private DaprClient Client() => new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
    private static JsonObject Value(int count) => new() { ["count"] = count };

    [Fact]
    public async Task UnqualifiedRedisTransactionRefusesSuccessForBothDeadlineAndLegacyPartialExecution()
    {
        new ReadModelBatchOptions().GetProfile("statestore").ShouldBe(ReadModelBatchStoreProfile.Resumable);
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(30);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = ReadModelBatchStoreProfile.TransactionQualified;
        foreach (bool deadlineBearing in new[] { false, true })
        {
            string tenant = "unqualified-transaction-" + Guid.NewGuid().ToString("N");
            string model = tenant + ":model";
            ReadModelBatchOperation operation = deadlineBearing
                ? ReadModelBatchOperation.WriteExpiring(model, Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: deadline)
                : ReadModelBatchOperation.Write(model, Value(1), ReadModelBatchConcurrency.LastWrite);
            var batch = new ReadModelBatch(new("statestore", tenant, "widget", "owner", "detail", "provider-probe"), [operation]);
            Exception? nativeFailure = null;
            var observed = new ObservedTransactionLiveAccessor(new DaprReadModelBatchStateAccessor(client, "statestore"),
                error => nativeFailure = error);
            ReadModelBatchResult result = await new ReadModelBatchProtocol(observed, options, NullLogger.Instance)
                .ExecuteAsync(batch, null, CancellationToken.None);
            nativeFailure.ShouldBeOfType<Dapr.DaprException>();
            nativeFailure.ToString().ShouldContain("failed to set key");
            result.Status.ShouldBe(ReadModelBatchStatus.Indeterminate);
            result.IsSuccess.ShouldBeFalse();
            IDatabase db = _redis.GetDatabase();
            ReadModelBatchMarker.ReadProof((byte[])(await db.HashGetAsync(Key(batch.Scope.ComputeMarkerKey()), "data"))!)!
                .Status.ShouldBe(ReadModelBatchMarkerStatus.Prepared);
            RedisValue persisted = await db.HashGetAsync(Key(model), "data");
            persisted.IsNull.ShouldBeFalse("the fixture provider exposes a partial mutation before rejecting the marker update");
            (await new DaprReadModelStore(client, Options.Create(options)).GetAsync<JsonObject>("statestore", model))
                .Value!["count"]!.GetValue<int>().ShouldBe(1); // A misqualified transaction can expose partial rows; refusal is completion-only.
            if (deadlineBearing)
            {
                ReadModelBatchExpiringValue.Decode((byte[])persisted!).Deadline.ShouldBe(deadline);
                TimeSpan ttl = (await db.KeyTimeToLiveAsync(Key(model))).ShouldNotBeNull();
                ttl.ShouldBeGreaterThan(TimeSpan.Zero);
                ttl.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
            }
            TestContext.Current.TestOutputHelper!.WriteLine($"deadline={deadlineBearing}; partial-model-persisted=true; marker=Prepared; SDK-success=false; native={nativeFailure.InnerException?.Message}");
        }
    }

    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable)]
    public async Task DelayedInstallationKeepsRemainingPhysicalTtlAndInclusiveDeadline(ReadModelBatchStoreProfile profile)
    {
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(30);
        string tenant = "expiry-" + Guid.NewGuid().ToString("N");
        string model = tenant + ":model", receipt = tenant + ":receipt", checkpoint = tenant + ":checkpoint";
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        options.StoreProfiles["statestore"] = profile;
        var store = new DaprReadModelStore(client, Options.Create(options));
        var batch = new ReadModelBatch(new("statestore", tenant, "widget", "owner", "detail", "expiry"),
        [ReadModelBatchOperation.WriteExpiring(model, Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: deadline),
            ReadModelBatchOperation.WriteExpiring(receipt, Value(1), ReadModelBatchConcurrency.LastWrite, expiresAtUtc: deadline),
            ReadModelBatchOperation.Write(checkpoint, Value(1), ReadModelBatchConcurrency.LastWrite)]);
        if (profile == ReadModelBatchStoreProfile.Resumable)
            (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.Advance(TimeSpan.FromSeconds(26));
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement, Options.Create(options));
        if (profile == ReadModelBatchStoreProfile.Resumable)
            (await store.CommitAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);

        IDatabase db = _redis.GetDatabase();
        TimeSpan ttl = (await db.KeyTimeToLiveAsync(Key(model))).ShouldNotBeNull();
        ttl.ShouldBeGreaterThan(TimeSpan.Zero);
        ttl.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(4));
        (await db.KeyTimeToLiveAsync(Key(checkpoint))).ShouldBeNull();
        ReadModelBatchExpiringValue.Decode((byte[])(await db.HashGetAsync(Key(model), "data"))!).Deadline.ShouldBe(deadline);
        clock.Advance(TimeSpan.FromSeconds(4).Subtract(TimeSpan.FromTicks(1)));
        (await store.GetAsync<JsonObject>("statestore", model)).Value.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromTicks(1));
        (await store.GetManyAsync<JsonObject>("statestore", [model, receipt], 1)).All(item => item.Value is null).ShouldBeTrue();
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        (await db.KeyExistsAsync(Key(model))).ShouldBeFalse();
        (await db.KeyExistsAsync(Key(receipt))).ShouldBeFalse();
        (await db.KeyExistsAsync(Key(checkpoint))).ShouldBeTrue();
        TestContext.Current.TestOutputHelper!.WriteLine($"profile={profile}; remaining-ttl-ms={ttl.TotalMilliseconds}; physical-model-and-receipt-absent=true; checkpoint-retained=true");
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.BeforeCommit)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    public async Task ReplacedStoreRecoversOriginalManifestAfterDeadlineWithoutInstallingExpiredRows(ReadModelBatchPhase phase)
    {
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(30);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        string tenant = "expiry-recovery-" + Guid.NewGuid().ToString("N");
        string model = tenant + ":model", checkpoint = tenant + ":checkpoint";
        var batch = new ReadModelBatch(new("statestore", tenant, "widget", "owner", "detail", "expiry-recovery"),
        [ReadModelBatchOperation.Write(checkpoint, Value(1), ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.WriteExpiring(model, Value(1), ReadModelBatchConcurrency.CreateOnly, expiresAtUtc: deadline)]) { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, "statestore"), options, NullLogger.Instance);
        int ordinal = phase == ReadModelBatchPhase.BeforeCommit ? -1 : 1;
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch,
            new RetainedBatchLiveFaultInjector(phase, ordinal), CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(31));
        using DaprClient replacement = Client();
        var store = new DaprReadModelStore(replacement, Options.Create(options));
        (await store.RecoverPendingAsync(batch.Scope with { BatchId = "replacement" }, model)).IsResolved.ShouldBeTrue();
        (await store.ExecuteAsync(batch)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        (await _redis.GetDatabase().KeyExistsAsync(Key(model))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(checkpoint))).ShouldBeTrue();
        (await store.GetAsync<JsonObject>("statestore", model)).Value.ShouldBeNull();
        TestContext.Current.TestOutputHelper!.WriteLine($"phase={phase}; original-deadline-enforced=true; physical-model-absent=true; checkpoint-retained=true");
    }

    [Fact]
    public async Task AbortedReplacementRestoresOnlyOriginalRemainingPhysicalTtlThenExpires()
    {
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(30);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        var store = new DaprReadModelStore(client, Options.Create(options));
        string tenant = "abort-expiry-" + Guid.NewGuid().ToString("N");
        string key = tenant + ":model";
        var original = new ReadModelBatch(new("statestore", tenant, "widget", "owner", "detail", "original"),
            [ReadModelBatchOperation.WriteExpiring(key, Value(1), ReadModelBatchConcurrency.CreateOnly, deadline)]);
        (await store.ExecuteAsync(original)).IsSuccess.ShouldBeTrue();
        ReadModelEntry<JsonObject> previous = await store.GetAsync<JsonObject>("statestore", key);
        var replacement = new ReadModelBatch(original.Scope with { BatchId = "replacement" },
            [ReadModelBatchOperation.Write(key, Value(99), ReadModelBatchConcurrency.Match(previous.ETag!))]);
        (await store.StageAsync(replacement)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.Advance(TimeSpan.FromSeconds(27));
        using DaprClient replaced = Client();
        store = new DaprReadModelStore(replaced, Options.Create(options));
        (await store.AbortAsync(replacement)).Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
        (await store.AbortAsync(replacement)).Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
        TimeSpan ttl = (await _redis.GetDatabase().KeyTimeToLiveAsync(Key(key))).ShouldNotBeNull();
        ttl.ShouldBeGreaterThan(TimeSpan.Zero); ttl.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(3));
        ReadModelBatchExpiringValue.Decode((byte[])(await _redis.GetDatabase().HashGetAsync(Key(key), "data"))!).Deadline.ShouldBe(deadline);
        (await store.GetAsync<JsonObject>("statestore", key)).Value!["count"]!.GetValue<int>().ShouldBe(1);
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        (await _redis.GetDatabase().KeyExistsAsync(Key(key))).ShouldBeFalse();
        TestContext.Current.TestOutputHelper!.WriteLine($"aborted-previous-restored=true; original-deadline={deadline:O}; remaining-ttl-ms={ttl.TotalMilliseconds}; physically-expired=true");
    }

}
