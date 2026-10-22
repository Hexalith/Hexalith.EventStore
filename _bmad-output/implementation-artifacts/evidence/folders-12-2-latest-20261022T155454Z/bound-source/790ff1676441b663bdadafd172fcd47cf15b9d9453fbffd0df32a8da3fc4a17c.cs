using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyLegacyInventoryActorTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InspectAsync_RequiredInventoryWithoutEntry_FailsClosedAsUninventoried()
    {
        (IdempotencyLegacyInventoryActor actor, _) = CreateActor(requireInventory: true);

        IdempotencyLegacyInventoryInspection result = await actor.InspectAsync([Alias()]);

        result.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Uninventoried);
        result.Entry.ShouldBeNull();
    }

    [Fact]
    public async Task MigrationPhases_PreserveSourceEvidenceAndExactLogicalResult()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();

        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryInspection inventoried = await actor.InspectAsync([Alias()]);
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);
        IdempotencyLegacyInventoryEntry acknowledged = await AdvanceAsync(
            actor,
            prepared,
            IdempotencyLegacyMigrationPhase.TargetPrepared);
        IdempotencyLegacyInventoryEntry redirected = await AdvanceAsync(
            actor,
            acknowledged,
            IdempotencyLegacyMigrationPhase.TargetAcknowledged,
            "redirect-digest");
        IdempotencyLegacyInventoryEntry flipped = await AdvanceAsync(
            actor,
            redirected,
            IdempotencyLegacyMigrationPhase.SourceRedirected,
            "redirect-digest");
        IdempotencyLegacyInventoryEntry migrated = await AdvanceAsync(
            actor,
            flipped,
            IdempotencyLegacyMigrationPhase.AuthorityFlipped,
            "redirect-digest");
        IdempotencyLegacyInventoryInspection final = await actor.InspectAsync([Alias()]);

        inventoried.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrate);
        prepared.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.TargetPrepared);
        acknowledged.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.TargetAcknowledged);
        redirected.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.SourceRedirected);
        flipped.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.AuthorityFlipped);
        migrated.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.Migrated);
        migrated.SourceAggregateActorId.ShouldBe(entry.SourceAggregateActorId);
        migrated.SourceEvidenceDigest.ShouldBe(entry.SourceEvidenceDigest);
        migrated.ReplayResult.ShouldBe(entry.ReplayResult);
        final.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrated);
        await stateManager.Received(7).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InspectAsync_OpenInventoryNeverClassifiesAbsenceAsNoLegacy()
    {
        (IdempotencyLegacyInventoryActor actor, _) = CreateActor(requireInventory: true);

        await actor.InventoryAsync(Entry());
        IdempotencyLegacyInventoryInspection result = await actor.InspectAsync([Alias("missing")]);

        result.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Uninventoried);
    }

    [Fact]
    public async Task InspectAsync_ClosedManifestWithUnavailableEntryFailsClosedNotNoLegacy()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        _ = await stateManager.TryRemoveStateAsync(
            $"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}",
            CancellationToken.None);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.InspectAsync([Alias()]));

        exception.Message.ShouldContain("unavailable");
    }

    [Fact]
    public async Task InspectAsync_TamperedReplayResultBreaksClosedManifestWithoutMutation()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryEntry tampered = entry with
        {
            ReplayResult = entry.ReplayResult with { ResultPayload = "tampered-result" },
        };
        await stateManager.SetStateAsync(
            $"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}",
            tampered,
            CancellationToken.None);
        stateManager.ClearReceivedCalls();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.InspectAsync([Alias()]));

        exception.Message.ShouldContain("not bound");
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = await stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InspectAsync_CrossTenantStoredEntryFailsClosedWithoutMutation()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        await stateManager.SetStateAsync(
            $"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}",
            entry with
            {
                TenantPartition = "tenant-b",
                SourceAggregateActorId = "tenant-b:folders:legacy-folder",
            },
            CancellationToken.None);
        stateManager.ClearReceivedCalls();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.InspectAsync([Alias()]));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = await stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InventoryAsync_UnknownLegacySchemaFailsClosedWithoutMutation()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.InventoryAsync(
            Entry() with { LegacySchemaVersion = 2 }));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InventoryAsync_IncoherentTimestampsFailClosedWithoutMutation(bool observedBeforeConsumed)
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        IdempotencyLegacyInventoryEntry incoherent = observedBeforeConsumed
            ? entry with { LastObservedAt = entry.FirstConsumedAt.AddTicks(-1) }
            : entry with { ReplayExpiresAt = entry.FirstConsumedAt.AddTicks(-1) };

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.InventoryAsync(incoherent));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InspectAsync_ClosedManifestDoesNotCoverUnscannedDigestKeyVersion()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        const string InventoryId = "inventory-2026-08";
        await actor.CloseAsync(
            new IdempotencyLegacyInventoryClosure(
                IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                "tenant-a",
                InventoryId,
                1,
                ["v1"],
                0,
                IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                    IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                    "tenant-a",
                    InventoryId,
                    1,
                    [],
                    ["v1"])));
        stateManager.ClearReceivedCalls();

        IdempotencyLegacyInventoryInspection covered = await actor.InspectAsync([Alias()]);
        IdempotencyLegacyInventoryInspection unscanned = await actor.InspectAsync(
            [new IdempotencyAdmissionDirectoryAlias("v2", "tenant-a:v2:key-digest", "key-digest")]);

        covered.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.NoLegacy);
        unscanned.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Uninventoried);
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackAsync_AfterRedirectBoundaryIsForbidden()
    {
        (IdempotencyLegacyInventoryActor actor, _) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);
        IdempotencyLegacyInventoryEntry rolledBack = await actor.RollbackAsync(
            new IdempotencyLegacyMigrationRollbackRequest(
                entry.InventoryId,
                entry.MigrationId,
                entry.DigestKeyVersion,
                entry.KeyDigest,
                prepared.Phase,
                "tenant-a:v1:key-digest",
                "target-import-digest"));
        IdempotencyLegacyInventoryEntry preparedAgain = await AdvanceAsync(
            actor,
            rolledBack,
            IdempotencyLegacyMigrationPhase.Inventoried);
        IdempotencyLegacyInventoryEntry acknowledged = await AdvanceAsync(
            actor,
            preparedAgain,
            IdempotencyLegacyMigrationPhase.TargetPrepared);
        IdempotencyLegacyInventoryEntry redirected = await AdvanceAsync(
            actor,
            acknowledged,
            IdempotencyLegacyMigrationPhase.TargetAcknowledged,
            "redirect-digest");

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.RollbackAsync(
            new IdempotencyLegacyMigrationRollbackRequest(
                entry.InventoryId,
                entry.MigrationId,
                entry.DigestKeyVersion,
                entry.KeyDigest,
                redirected.Phase,
                "tenant-a:v1:key-digest",
                "target-import-digest")));
    }

    [Fact]
    public async Task AdvanceAsync_DigestRotationCannotReplacePinnedMigrationTarget()
    {
        (IdempotencyLegacyInventoryActor actor, _) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.AdvanceAsync(
            new IdempotencyLegacyMigrationAdvanceRequest(
                prepared.InventoryId,
                prepared.MigrationId,
                prepared.DigestKeyVersion,
                prepared.KeyDigest,
                prepared.Phase,
                "tenant-a:v2:different-target",
                prepared.TargetImportDigest!)));
    }

    [Fact]
    public async Task AdvanceAsync_RedirectDigestIsRequiredOnlyAtBoundaryAndImmutableAfterward()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried,
            "early-redirect"));
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);
        IdempotencyLegacyInventoryEntry acknowledged = await AdvanceAsync(
            actor,
            prepared,
            IdempotencyLegacyMigrationPhase.TargetPrepared);
        IdempotencyLegacyInventoryEntry redirected = await AdvanceAsync(
            actor,
            acknowledged,
            IdempotencyLegacyMigrationPhase.TargetAcknowledged,
            "redirect-digest");
        stateManager.ClearReceivedCalls();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => AdvanceAsync(
            actor,
            redirected,
            IdempotencyLegacyMigrationPhase.SourceRedirected,
            "changed-redirect"));

        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransitionAsync_TamperingAfterInspectionCannotCrossClosedManifest(
        bool rollback)
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);
        _ = await actor.InspectAsync([Alias()]);
        await stateManager.SetStateAsync(
            $"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}",
            prepared with { ReplayResult = prepared.ReplayResult with { ResultPayload = "tampered" } },
            CancellationToken.None);
        stateManager.ClearReceivedCalls();

        _ = rollback
            ? await Should.ThrowAsync<InvalidOperationException>(() => actor.RollbackAsync(
                new IdempotencyLegacyMigrationRollbackRequest(
                    entry.InventoryId,
                    entry.MigrationId,
                    entry.DigestKeyVersion,
                    entry.KeyDigest,
                    prepared.Phase,
                    prepared.TargetAdmissionActorId!,
                    prepared.TargetImportDigest!)))
            : await Should.ThrowAsync<InvalidOperationException>(() => AdvanceAsync(
                actor,
                prepared,
                IdempotencyLegacyMigrationPhase.TargetPrepared));

        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackAsync_LostResponseIsIdempotentOnlyForExactClearedCheckpoint()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();
        await actor.InventoryAsync(entry);
        await CloseAsync(actor, entry);
        IdempotencyLegacyInventoryEntry prepared = await AdvanceAsync(
            actor,
            entry,
            IdempotencyLegacyMigrationPhase.Inventoried);
        var request = new IdempotencyLegacyMigrationRollbackRequest(
            entry.InventoryId,
            entry.MigrationId,
            entry.DigestKeyVersion,
            entry.KeyDigest,
            prepared.Phase,
            prepared.TargetAdmissionActorId!,
            prepared.TargetImportDigest!);
        _ = await actor.RollbackAsync(request);
        stateManager.ClearReceivedCalls();

        IdempotencyLegacyInventoryEntry retried = await actor.RollbackAsync(request);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.RollbackAsync(
            request with { TargetImportDigest = "different-import" }));

        retried.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.Inventoried);
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeAsync_CleansExactAliasBindingWhenEntryAlreadyAbsentAndIsIdempotent()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry first = Entry();
        IdempotencyLegacyInventoryEntry second = first with
        {
            DigestKeyVersion = "v2",
            KeyDigest = "other-key",
            VerificationTag = "other-verification",
            IntentDigest = "other-intent",
            SourceEvidenceDigest = "other-evidence",
            MigrationId = "other-migration",
        };
        await actor.InventoryAsync(first);
        await actor.InventoryAsync(second);
        string digest = IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
            IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
            first.TenantPartition,
            first.InventoryId,
            first.InventoryVersion,
            [
                IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(first),
                IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(second),
            ],
            ["v1", "v2"]);
        await actor.CloseAsync(new IdempotencyLegacyInventoryClosure(
            IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
            first.TenantPartition,
            first.InventoryId,
            first.InventoryVersion,
            ["v1", "v2"],
            2,
            digest));
        _ = await stateManager.TryRemoveStateAsync(
            $"legacy:{first.DigestKeyVersion}:{first.KeyDigest}",
            CancellationToken.None);

        await actor.PurgeAsync(Alias());
        await actor.PurgeAsync(Alias());
        IdempotencyLegacyInventoryInspection remaining = await actor.InspectAsync(
            [new IdempotencyAdmissionDirectoryAlias("v2", "tenant-a:v2:other-key", "other-key")]);
        await actor.PurgeAsync(new IdempotencyAdmissionDirectoryAlias(
            "v2",
            "tenant-a:v2:other-key",
            "other-key"));
        IdempotencyLegacyInventoryInspection final = await actor.InspectAsync([Alias()]);

        remaining.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrate);
        final.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Uninventoried);
    }

    [Fact]
    public void ManifestEvidence_BindsAllClosureMetadataIncludingEmptyInventory()
    {
        string baseline = IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
            1,
            "tenant-a",
            "inventory-a",
            1,
            [],
            ["v1"]);

        IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(2, "tenant-a", "inventory-a", 1, [], ["v1"])
            .ShouldNotBe(baseline);
        IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(1, "tenant-b", "inventory-a", 1, [], ["v1"])
            .ShouldNotBe(baseline);
        IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(1, "tenant-a", "inventory-b", 1, [], ["v1"])
            .ShouldNotBe(baseline);
        IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(1, "tenant-a", "inventory-a", 2, [], ["v1"])
            .ShouldNotBe(baseline);
    }

    [Fact]
    public async Task InspectAsync_CrossAggregateMatchesRemainUnsafeWithoutMutation()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry first = Entry();
        IdempotencyLegacyInventoryEntry second = first with
        {
            SourceAggregateActorId = "tenant-a:folders:other-folder",
            SourceEvidenceDigest = "other-source-evidence",
            DigestKeyVersion = "v2",
            KeyDigest = "other-key-digest",
            VerificationTag = "other-verification",
            IntentDigest = "other-intent",
            MigrationId = "migration-other",
        };
        await actor.InventoryAsync(first);
        await actor.InventoryAsync(second);
        await actor.CloseAsync(
            new IdempotencyLegacyInventoryClosure(
                IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                first.TenantPartition,
                first.InventoryId,
                first.InventoryVersion,
                ["v1", "v2"],
                2,
                IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                    IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                    first.TenantPartition,
                    first.InventoryId,
                    first.InventoryVersion,
                [
                    IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(first),
                    IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(second),
                ],
                ["v1", "v2"])));

        IdempotencyLegacyInventoryInspection result = await actor.InspectAsync(
        [
            Alias(),
            new IdempotencyAdmissionDirectoryAlias("v2", "tenant-a:v2:other-key-digest", "other-key-digest"),
        ]);

        result.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Unsafe);
        await stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static (IdempotencyLegacyInventoryActor Actor, IActorStateManager StateManager) CreateActor(
        bool requireInventory)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var entries = new Dictionary<string, IdempotencyLegacyInventoryEntry>(StringComparer.Ordinal);
        IdempotencyLegacyInventoryManifest? manifest = null;
        _ = stateManager.TryGetStateAsync<IdempotencyLegacyInventoryEntry>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => entries.TryGetValue(
                callInfo.ArgAt<string>(0),
                out IdempotencyLegacyInventoryEntry? entry)
                    ? new ConditionalValue<IdempotencyLegacyInventoryEntry>(true, entry)
                    : new ConditionalValue<IdempotencyLegacyInventoryEntry>(false, default!));
        _ = stateManager.SetStateAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyLegacyInventoryEntry>(entry =>
                    entries[$"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}"] = entry),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.TryRemoveStateAsync(
                IdempotencyLegacyInventoryActor.ManifestStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                bool existed = manifest is not null;
                manifest = null;
                return existed;
            });
        _ = stateManager.TryRemoveStateAsync(
                Arg.Is<string>(stateName => stateName.StartsWith("legacy:", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => entries.Remove(callInfo.ArgAt<string>(0)));
        _ = stateManager.TryGetStateAsync<IdempotencyLegacyInventoryManifest>(
                IdempotencyLegacyInventoryActor.ManifestStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => manifest is null
                ? new ConditionalValue<IdempotencyLegacyInventoryManifest>(false, default!)
                : new ConditionalValue<IdempotencyLegacyInventoryManifest>(true, manifest));
        _ = stateManager.SetStateAsync(
                IdempotencyLegacyInventoryActor.ManifestStateName,
                Arg.Do<IdempotencyLegacyInventoryManifest>(value => manifest = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ActorHost host = ActorHost.CreateForTest<IdempotencyLegacyInventoryActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a") });
        var actor = new IdempotencyLegacyInventoryActor(
            host,
            Options.Create(new IdempotencyAdmissionOptions
            {
                RequireLegacyInventory = requireInventory,
            }));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager);
    }

    private static IdempotencyAdmissionDirectoryAlias Alias(string keyDigest = "key-digest")
        => new("v1", $"tenant-a:v1:{keyDigest}", keyDigest);

    private static IdempotencyLegacyInventoryEntry Entry()
        => new(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            "tenant-a",
            "tenant-a:folders:legacy-folder",
            "source-evidence-digest",
            LegacySchemaVersion: 1,
            "v1",
            "key-digest",
            "verification-tag",
            "intent-digest",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-1),
            _now,
            _now.AddDays(1),
            new CommandProcessingResult(
                true,
                CorrelationId: "trace-original",
                EventCount: 1,
                ResultPayload: "{\"same\":true}"),
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyLegacyMigrationPhase.Inventoried,
            "inventory-2026-08",
            1,
            "migration-01J00000000000000000000000");

    private static Task CloseAsync(
        IdempotencyLegacyInventoryActor actor,
        IdempotencyLegacyInventoryEntry entry)
        => actor.CloseAsync(
            new IdempotencyLegacyInventoryClosure(
                IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                entry.TenantPartition,
                entry.InventoryId,
                entry.InventoryVersion,
                [entry.DigestKeyVersion],
                1,
                IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                    IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                    entry.TenantPartition,
                    entry.InventoryId,
                    entry.InventoryVersion,
                    [IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(entry)],
                    [entry.DigestKeyVersion])));

    private static Task<IdempotencyLegacyInventoryEntry> AdvanceAsync(
        IdempotencyLegacyInventoryActor actor,
        IdempotencyLegacyInventoryEntry entry,
        IdempotencyLegacyMigrationPhase expectedPhase,
        string? redirectDigest = null)
        => actor.AdvanceAsync(
            new IdempotencyLegacyMigrationAdvanceRequest(
                entry.InventoryId,
                entry.MigrationId,
                entry.DigestKeyVersion,
                entry.KeyDigest,
                expectedPhase,
                "tenant-a:v1:key-digest",
                "target-import-digest",
                redirectDigest));
}
