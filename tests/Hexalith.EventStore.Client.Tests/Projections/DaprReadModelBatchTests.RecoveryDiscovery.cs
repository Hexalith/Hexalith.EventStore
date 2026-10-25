using System.Text;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.ManifestPrepared)]
    [InlineData(ReadModelBatchPhase.MarkerPrepared)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation)]
    public async Task AggregateDiscoveryRetiresPrivatePreparationBeforeFirstParticipantAndFencesLateInstallers(ReadModelBatchPhase crash)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        string secret = "private-before-participant-" + new string('x', 18_000);
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write("detail", new { Secret = secret }, ReadModelBatchConcurrency.CreateOnly),
             ReadModelBatchOperation.Write("processing", new Detail(2), ReadModelBatchConcurrency.CreateOnly)])
             { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(crash), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        string manifestKey = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        client.ByteStoreContains(manifestKey).ShouldBeTrue();
        if (crash == ReadModelBatchPhase.ManifestPrepared) client.ByteStoreContains("detail").ShouldBeFalse();

        var restarted = CreateStore(client);
        (await ((IReadModelRecoveryScopeStore)restarted).RetireRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        Encoding.UTF8.GetString(client.ByteStoreValue(manifestKey)).ShouldNotContain("private-before-participant");
        Encoding.UTF8.GetString(client.ByteStoreValue(scope.ComputeMarkerKey())).ShouldNotContain("private-before-participant");
        client.ByteStoreContains("detail").ShouldBeFalse();
        client.ByteStoreContains("processing").ShouldBeFalse();
        (await restarted.ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        client.ByteStoreContains("detail").ShouldBeFalse();
        Encoding.UTF8.GetString(client.ByteStoreValue(manifestKey)).ShouldBe("{\"retiredRecoveryManifest\":true}");
    }

    [Fact]
    public async Task RecoveryScopeClosureRejectsNewAdmissionUntilCanonicalErasureThenAllowsSafeUpdates()
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var scope = Scope();
        client.SeedByteStore("detail", "{\"secret\":\"old-private-content\"}"u8.ToArray());
        var original = await store.GetAsync<Detail>(Store, "detail");
        var stale = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new Detail(2), ReadModelBatchConcurrency.Match(original.ETag!))])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        var recovery = (IReadModelRecoveryScopeStore)store;
        (await recovery.RetireRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        (await store.ExecuteAsync(stale)).IsSuccess.ShouldBeFalse();
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())).ShouldBeFalse();
        client.SeedByteStore("detail", "{\"erased\":true,\"version\":2}"u8.ToArray());
        await recovery.ReleaseRecoveryScopeAsync(scope);
        (await store.ExecuteAsync(stale)).IsSuccess.ShouldBeFalse();
        client.ByteStoreContains(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())).ShouldBeFalse();
        string etag = (await store.GetAsync<Detail>(Store, "detail")).ETag!;
        var safe = new ReadModelBatch(scope with { BatchId = "post-erasure-safe-update" },
            [ReadModelBatchOperation.Write("detail", new { Erased = true, Version = 3 }, ReadModelBatchConcurrency.Match(etag))])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        (await store.ExecuteAsync(safe)).IsSuccess.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(client.ByteStoreValue("detail")).ShouldNotContain("old-private-content");
    }

    [Fact]
    public async Task GenerationStagingAcceptsMaximumUtf8ProjectionDiscriminatorAndResumes()
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var scope = Scope() with { ProjectionType = new string('é', 256) };
        var writes = new[] { ReadModelBatchOperation.Write("bucket", new Detail(1), ReadModelBatchConcurrency.CreateOnly) };
        await ReadModelGenerationStaging.StageAsync(store, scope, writes);
        await ReadModelGenerationStaging.StageAsync(store, scope, writes);
        client.ByteStoreContains("bucket").ShouldBeTrue();
    }
}
