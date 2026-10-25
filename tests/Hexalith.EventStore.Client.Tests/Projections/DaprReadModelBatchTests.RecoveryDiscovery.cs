using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchStoreProfile.Resumable, true)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, true)]
    [InlineData(ReadModelBatchStoreProfile.Resumable, false)]
    [InlineData(ReadModelBatchStoreProfile.TransactionQualified, false)]
    public async Task DiscoverableTerminalDecisionSurvivesFreshStoresSuccessRetryConflictAndRetirement(ReadModelBatchStoreProfile profile, bool retained)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var selection = retained ? null : new Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection(1,
            scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "owner", "generation", 2, scope.BatchId, scope.BatchId, "2");
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("detail", new { Secret = "private-value", ProjectionOwner = selection, ProjectionVersion = "2" }, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = retained, DiscoverableRecovery = true, OwnerSelection = selection };
        var store = CreateStore(client, profile);
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await CreateStore(client, profile).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        var recovery = (IReadModelRecoveryScopeStore)CreateStore(client, profile);
        (await recovery.ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(batch))).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        string manifestKey = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        Encoding.UTF8.GetString(client.ByteStoreValue(manifestKey)).ShouldBe("{\"retiredRecoveryManifest\":true}");
        Encoding.UTF8.GetString(client.ByteStoreValue(scope.ComputeMarkerKey())).ShouldNotContain("private-value");
        (await recovery.SettleRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        (await recovery.ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(batch))).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        // A different snapshot's create-only refusal has the same complete terminal contract.
        var conflictScope = scope with { BatchId = "conflict" };
        var conflictSelection = selection is null ? null : selection with { BatchId = "conflict", MessageId = "conflict" };
        var conflict = new ReadModelBatch(conflictScope, [ReadModelBatchOperation.Write("detail", new { Secret = "rejected-private", ProjectionOwner = conflictSelection, ProjectionVersion = "2" }, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = retained, DiscoverableRecovery = true, OwnerSelection = conflictSelection };
        (await store.ExecuteAsync(conflict)).IsSuccess.ShouldBeFalse();
        var raceScope = scope with { BatchId = "prepared-conflict" };
        var raceSelection = selection is null ? null : selection with { BatchId = raceScope.BatchId, MessageId = raceScope.BatchId };
        var race = new ReadModelBatch(raceScope, [ReadModelBatchOperation.Write("raced", new
            { Secret = "losing-private", ProjectionOwner = raceSelection, ProjectionVersion = "2" }, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = retained, DiscoverableRecovery = true, OwnerSelection = raceSelection };
        (await recovery.PrepareRecoveryManifestAsync(race)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        client.SeedByteStore("raced", "{\"winner\":true}"u8.ToArray());
        (await store.ExecuteAsync(race)).IsSuccess.ShouldBeFalse();
        (await ((IReadModelRecoveryScopeStore)CreateStore(client, profile)).ReadRecoveryDecisionAsync(raceScope, ReadModelBatchFingerprint.Compute(race)))
            .Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
        Encoding.UTF8.GetString(client.ByteStoreValue("raced")).ShouldBe("{\"winner\":true}");
        // Snapshot admission can refuse before preparation. Prepare a separate supported batch
        // and retire it to prove aborted terminal evidence on both profiles and retention policies.
        var retiredScope = scope with { BatchId = "retired" };
        var retiredSelection = selection is null ? null : selection with { BatchId = "retired", MessageId = "retired" };
        var retired = new ReadModelBatch(retiredScope, [ReadModelBatchOperation.Write("unused", new { Secret = "prepared-private", ProjectionOwner = retiredSelection, ProjectionVersion = "2" }, ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = retained, DiscoverableRecovery = true, OwnerSelection = retiredSelection };
        (await recovery.PrepareRecoveryManifestAsync(retired)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        // Settlement leaves guarded and ordinary Prepared work unchanged.
        byte[] prepared = client.ByteStoreValue(retiredScope.ComputeMarkerKey()).ToArray();
        (await recovery.SettleRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        client.ByteStoreValue(retiredScope.ComputeMarkerKey()).ShouldBe(prepared);
        client.ByteStoreContains("unused").ShouldBeFalse();
        (await recovery.RetireRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        (await ((IReadModelRecoveryScopeStore)CreateStore(client, profile)).ReadRecoveryDecisionAsync(retiredScope, ReadModelBatchFingerprint.Compute(retired)))
            .Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
        (await store.ExecuteAsync(retired)).IsSuccess.ShouldBeFalse();
        Encoding.UTF8.GetString(client.ByteStoreValue(ReadModelBatchKeys.ManifestKey(retiredScope.ComputeScopeHash()))).ShouldBe("{\"retiredRecoveryManifest\":true}");
        client.ByteStoreContains("unused").ShouldBeFalse();
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    [InlineData(ReadModelBatchPhase.RecoveryAnchorCompacted)]
    public async Task FreshSettlementFinishesTerminalAnchorAfterOriginalManifestHasAlreadyBeenReclaimed(ReadModelBatchPhase crash)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write("canonical", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
             ReadModelBatchOperation.Write("old-bucket", new Detail(2), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(crash), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        Encoding.UTF8.GetString(client.ByteStoreValue(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())))
            .ShouldBe("{\"retiredRecoveryManifest\":true}");
        var marker = ReadModelBatchMarker.ReadBoundedProof(client.ByteStoreValue(scope.ComputeMarkerKey()));
        marker!.RecoveryAnchor.ShouldNotBeNull();
        var fresh = CreateStore(client);
        (await ((IReadModelRecoveryScopeStore)fresh).SettleRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        (await ((IReadModelRecoveryScopeStore)CreateStore(client)).ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(batch)))
            .Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        (await CreateStore(client).ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        var next = new ReadModelBatch(scope with { BatchId = "next-delivery" },
            [ReadModelBatchOperation.Write("new-bucket", new Detail(3), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        (await fresh.ExecuteAsync(next)).IsSuccess.ShouldBeTrue();
        var (directory, _) = await ReadModelRecoveryDiscovery.ReadAsync(new DaprReadModelBatchStateAccessor(client, Store), scope, TestContext.Current.CancellationToken);
        directory.Entries.ShouldBeEmpty();
        ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue("old-bucket")).ShouldBeFalse();
    }

    [Fact]
    public async Task SettlementCannotCertifyOrReclaimAnUnverifiedPartialTransaction()
    {
        using var client = new RecordingDaprClient { TransactionOperationApplyFilter = operation => operation.Key != "missing-participant" };
        var scope = Scope();
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write("present-participant", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
             ReadModelBatchOperation.Write("missing-participant", new Detail(2), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        (await CreateStore(client, ReadModelBatchStoreProfile.TransactionQualified).ExecuteAsync(batch)).IsSuccess.ShouldBeFalse();
        var fresh = (IReadModelRecoveryScopeStore)CreateStore(client, ReadModelBatchStoreProfile.TransactionQualified);
        (await fresh.SettleRecoveryScopeAsync(scope)).IsResolved.ShouldBeFalse();
        (await fresh.ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(batch))).Status.ShouldBe(ReadModelBatchStagingStatus.Indeterminate);
        string manifest = Encoding.UTF8.GetString(client.ByteStoreValue(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())));
        manifest.ShouldNotBe("{\"retiredRecoveryManifest\":true}");
        (await fresh.RetireRecoveryScopeAsync(scope)).IsResolved.ShouldBeFalse();
        (await fresh.ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(batch))).Status.ShouldBe(ReadModelBatchStagingStatus.Indeterminate);
        Encoding.UTF8.GetString(client.ByteStoreValue(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()))).ShouldBe(manifest);
        client.ByteStoreContains("missing-participant").ShouldBeFalse();
    }

    [Fact]
    public async Task FreshSdkSettlementCompactsCommittedAnchorOutsideNewDeliveryKeysWithoutRetiringPreparedWork()
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var original = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write("canonical", new Detail(1), ReadModelBatchConcurrency.CreateOnly),
             ReadModelBatchOperation.Write("old-bucket", new Detail(2), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(original, new PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase.BeforeReceipt), TestContext.Current.CancellationToken))
            .IsSuccess.ShouldBeFalse();
        ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue("old-bucket")).ShouldBeTrue();
        var nextScope = scope with { BatchId = "new-delivery" };
        var fresh = CreateStore(client);
        (await ((IReadModelRecoveryScopeStore)fresh).SettleRecoveryScopeAsync(nextScope)).IsResolved.ShouldBeTrue();
        ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue("old-bucket")).ShouldBeFalse();
        (await ((IReadModelRecoveryScopeStore)CreateStore(client)).ReadRecoveryDecisionAsync(scope, ReadModelBatchFingerprint.Compute(original)))
            .Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        var next = new ReadModelBatch(nextScope, [ReadModelBatchOperation.Write("new-bucket", new Detail(3), ReadModelBatchConcurrency.CreateOnly)])
            { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        (await fresh.ExecuteAsync(next)).IsSuccess.ShouldBeTrue();
        (await CreateStore(client).ExecuteAsync(original)).IsSuccess.ShouldBeTrue();
        (await fresh.GetAsync<Detail>(Store, "old-bucket")).Value!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task DiscoverableOwnerBatchesAlwaysEnforceHardRecoveryLimitsBeforeAnyWrite()
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        var selection = new Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection(1, scope.TenantId, scope.Domain,
            scope.AggregateId, scope.ProjectionType, "owner", "generation", 2, scope.BatchId, scope.BatchId, "2");
        var invalid = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("unbound", new Detail(1), ReadModelBatchConcurrency.CreateOnly)])
            { DiscoverableRecovery = true };
        await Should.ThrowAsync<ArgumentException>(() => CreateStore(client).ExecuteAsync(invalid));
        client.TrySaveByteOperations.Count.ShouldBe(0);
        var options = new ReadModelBatchOptions { MaxOperations = 200, MaxKeyByteLength = 1024, MaxCanonicalManifestBytes = 2_097_152 };
        var store = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(options));
        var operations = Enumerable.Range(0, 101).Select(i => ReadModelBatchOperation.Write("owner-" + i,
            new { ProjectionOwner = selection, Count = i }, ReadModelBatchConcurrency.CreateOnly)).ToArray();
        var batch = new ReadModelBatch(scope, operations) { DiscoverableRecovery = true, OwnerSelection = selection };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(batch));
        client.TrySaveByteOperations.Count.ShouldBe(0);
        var longKey = new ReadModelBatch(scope, [ReadModelBatchOperation.Write(new string('k', 513), new { ProjectionOwner = selection, ProjectionVersion = "2" }, ReadModelBatchConcurrency.CreateOnly)])
            { DiscoverableRecovery = true, OwnerSelection = selection };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(longKey));
        var large = new ReadModelBatch(scope, [ReadModelBatchOperation.Write("large", new { ProjectionOwner = selection, Value = new string('a', 800_000) }, ReadModelBatchConcurrency.CreateOnly)])
            { DiscoverableRecovery = true, OwnerSelection = selection };
        await Should.ThrowAsync<ArgumentException>(() => store.ExecuteAsync(large));
        client.TrySaveByteOperations.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExplicitGenerationValueBudgetPreservesExistingConservativeGroupIdentitiesOnRetry()
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var writes = Enumerable.Range(0, 8).Select(i => ReadModelBatchOperation.Write("bucket-" + i,
            new string('a', 200_000), ReadModelBatchConcurrency.CreateOnly)).ToArray();
        await ReadModelGenerationStaging.StageAsync(store, Scope(), writes);
        // These values were already staged by the old conservative caller. Enlarging the
        // single-value ceiling must not regroup them under reused immutable batch IDs.
        await ReadModelGenerationStaging.StageAsync(CreateStore(client), Scope(), writes, 1_048_576);
        foreach (var write in writes) (await store.GetAsync<string>(Store, write.Key)).Value!.Length.ShouldBe(200_000);
    }

    [Fact]
    public async Task ExplicitGenerationByteBudgetRemainsFinitePreflightedAndDefaultStaysConservative()
    {
        using var client = new RecordingDaprClient();
        var store = CreateStore(client);
        var writes = new[] { ReadModelBatchOperation.Write("bucket", new string('a', 620_000), ReadModelBatchConcurrency.CreateOnly) };
        await Should.ThrowAsync<ArgumentException>(() => ReadModelGenerationStaging.StageAsync(store, Scope(), writes));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => ReadModelGenerationStaging.StageAsync(store, Scope(), writes, 1_048_577));
        client.TrySaveByteOperations.Count.ShouldBe(0);
        var limited = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(new ReadModelBatchOptions { MaxCanonicalManifestBytes = 700_000 }));
        await Should.ThrowAsync<ArgumentException>(() => ReadModelGenerationStaging.StageAsync(limited, Scope(), writes, 1_048_576));
        client.TrySaveByteOperations.Count.ShouldBe(0);
        await ReadModelGenerationStaging.StageAsync(store, Scope(), writes, 1_048_576);
        await ReadModelGenerationStaging.StageAsync(CreateStore(client), Scope(), writes, 1_048_576);
        (await store.GetAsync<string>(Store, "bucket")).Value.ShouldBe(new string('a', 620_000));
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.ManifestPrepared)]
    [InlineData(ReadModelBatchPhase.MarkerPrepared)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation)]
    public async Task AggregateDiscoveryRetiresPrivatePreparationBeforeFirstParticipantAndFencesLateInstallers(ReadModelBatchPhase crash)
    {
        using var client = new RecordingDaprClient();
        var scope = Scope();
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(9_014));
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write("detail", new { Secret = secret, }, ReadModelBatchConcurrency.CreateOnly),
             ReadModelBatchOperation.Write("processing", new Detail(2), ReadModelBatchConcurrency.CreateOnly)])
             { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, Store), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new PendingBatchRecoveryStopAtPhase(crash), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        string manifestKey = ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash());
        client.ByteStoreContains(manifestKey).ShouldBeTrue();
        if (crash == ReadModelBatchPhase.ManifestPrepared) client.ByteStoreContains("detail").ShouldBeFalse();

        var restarted = CreateStore(client);
        (await ((IReadModelRecoveryScopeStore)restarted).RetireRecoveryScopeAsync(scope)).IsResolved.ShouldBeTrue();
        Encoding.UTF8.GetString(client.ByteStoreValue(manifestKey)).ShouldNotContain(secret[..26]);
        Encoding.UTF8.GetString(client.ByteStoreValue(scope.ComputeMarkerKey())).ShouldNotContain(secret[..26]);
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
