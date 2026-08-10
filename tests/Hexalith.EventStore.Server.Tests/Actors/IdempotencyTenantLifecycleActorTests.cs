using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyTenantLifecycleActorTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnterDeletionAsync_UsesExactFourHundredDayBoundary()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();

        IdempotencyTenantLifecycleRecord record = await actor.EnterDeletionAsync(_now);

        record.State.ShouldBe(IdempotencyTenantLifecycleState.Retaining);
        record.DeletionApprovedAt.ShouldBe(_now);
        record.DeleteAfter.ShouldBe(_now.AddDays(400));
        record.RemainingRetention.ShouldBe(TimeSpan.FromDays(400));
    }

    [Fact]
    public async Task EnterDeletionAsync_LateObservationDoesNotRestartApprovedRetentionInterval()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        DateTimeOffset approvedAt = _now.AddDays(-401);

        IdempotencyTenantLifecycleRecord record = await actor.EnterDeletionAsync(approvedAt);

        record.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        record.DeletionApprovedAt.ShouldBe(approvedAt);
        record.DeleteAfter.ShouldBe(approvedAt.AddDays(400));
        record.RemainingRetention.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task RegisterAsync_RejectsActorIdThatIsNotBoundToProtectedIdentity()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.RegisterAsync(
            [
                new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v2", "key-b"),
            ]));

        exception.Message.ShouldContain("references are invalid");
    }

    [Fact]
    public async Task AdmitAsync_ActiveRegisteredReferenceRoutesInsideLifecycleTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                new ActorId("tenant-a:v1:key-a"),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        IdempotencyAdmissionResult expected = new(IdempotencyAdmissionDecision.Execute, 3);
        _ = admission.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>()).Returns(expected);
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(
            new IdempotencyTenantLifecycleAdmissionRequest(reference, AdmissionRequest()));

        result.ShouldBe(expected);
        _ = await admission.Received(1).AdmitAsync(AdmissionRequest());
    }

    [Fact]
    public async Task AdmitAsync_DeletionAfterRegistrationCannotCreateAdmissionState()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);
        _ = await actor.EnterDeletionAsync(_now);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.AdmitAsync(
                new IdempotencyTenantLifecycleAdmissionRequest(reference, AdmissionRequest())));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task MigrateLegacyAsync_ExactSourcePersistsEveryProofBeforeActivationAndReprovesCompletion()
    {
        var calls = new List<string>();
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IIdempotencyLegacySourceActor source = Substitute.For<IIdempotencyLegacySourceActor>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyAdmissionDirectoryInspectionActor directoryInspection
            = Substitute.For<IIdempotencyAdmissionDirectoryInspectionActor>();
        var targetReference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        var alias = new IdempotencyAdmissionDirectoryAlias("v1", targetReference.ActorId, "key-a");
        IdempotencyLegacyInventoryEntry entry = LegacyEntry();
        bool redirected = false;
        bool activated = false;
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = factory.CreateActorProxy<IIdempotencyLegacySourceActor>(
                Arg.Any<ActorId>(),
                nameof(AggregateActor))
            .Returns(source);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(target);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directoryInspection);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(_ => new IdempotencyLegacyInventoryInspection(
                entry.Phase == IdempotencyLegacyMigrationPhase.Migrated
                    ? IdempotencyLegacyInventoryDecision.Migrated
                    : IdempotencyLegacyInventoryDecision.Migrate,
                entry));
        _ = inventory.AdvanceAsync(Arg.Any<IdempotencyLegacyMigrationAdvanceRequest>())
            .Returns(callInfo =>
            {
                IdempotencyLegacyMigrationAdvanceRequest request = callInfo
                    .ArgAt<IdempotencyLegacyMigrationAdvanceRequest>(0);
                calls.Add($"checkpoint:{request.ExpectedPhase}");
                IdempotencyLegacyMigrationPhase next = request.ExpectedPhase switch
                {
                    IdempotencyLegacyMigrationPhase.Inventoried => IdempotencyLegacyMigrationPhase.TargetPrepared,
                    IdempotencyLegacyMigrationPhase.TargetPrepared => IdempotencyLegacyMigrationPhase.TargetAcknowledged,
                    IdempotencyLegacyMigrationPhase.TargetAcknowledged => IdempotencyLegacyMigrationPhase.SourceRedirected,
                    IdempotencyLegacyMigrationPhase.SourceRedirected => IdempotencyLegacyMigrationPhase.AuthorityFlipped,
                    IdempotencyLegacyMigrationPhase.AuthorityFlipped => IdempotencyLegacyMigrationPhase.Migrated,
                    _ => throw new InvalidOperationException(),
                };
                entry = entry with
                {
                    Phase = next,
                    TargetAdmissionActorId = request.TargetAdmissionActorId,
                    TargetImportDigest = request.TargetImportDigest,
                    SourceRedirectDigest = request.SourceRedirectDigest ?? entry.SourceRedirectDigest,
                };
                return entry;
            });
        _ = source.InspectLegacySourceAsync(Arg.Any<IdempotencyLegacySourceRequest>())
            .Returns(_ =>
            {
                calls.Add("inspect-source");
                return redirected
                    ? new IdempotencyLegacySourceInspection(
                        IdempotencyLegacySourceDecision.Redirected,
                        "redirect-digest")
                    : new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Exact);
            });
        _ = source.SetLegacySourceRedirectAsync(Arg.Any<IdempotencyLegacySourceRedirectRequest>())
            .Returns(_ =>
            {
                calls.Add("redirect-source");
                redirected = true;
                return new IdempotencyLegacySourceInspection(
                    IdempotencyLegacySourceDecision.Redirected,
                    "redirect-digest");
            });
        _ = target.PreparePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionImportRequest>())
            .Returns(_ =>
            {
                calls.Add("prepare-target");
                return Task.CompletedTask;
            });
        _ = target.AcknowledgePromotionAsync(
                Arg.Any<IdempotencyAdmissionPromotionAcknowledgementRequest>())
            .Returns(callInfo =>
            {
                calls.Add(activated ? "ack-active" : "ack-prepared");
                IdempotencyAdmissionPromotionAcknowledgementRequest request = callInfo
                    .ArgAt<IdempotencyAdmissionPromotionAcknowledgementRequest>(0);
                return new IdempotencyAdmissionPromotionAcknowledgement(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    request.SourceActorId,
                    request.MigrationId,
                    request.SourceEvidenceDigest,
                    request.ImportDigest,
                    activated);
            });
        _ = target.ActivatePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionActivationRequest>())
            .Returns(_ =>
            {
                calls.Add("activate-target");
                activated = true;
                return Task.CompletedTask;
            });
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(_ =>
            {
                calls.Add("prove-directory");
                return new IdempotencyAdmissionDirectoryResult(
                    targetReference.ActorId,
                    IdempotencyAdmissionPromotionPhase.Stable);
            });
        _ = target.InspectAsync().Returns(_ => new IdempotencyAdmissionInspection(
            true,
            Promotion: new IdempotencyAdmissionPromotionAcknowledgement(
                IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                entry.SourceAggregateActorId,
                entry.MigrationId,
                entry.SourceEvidenceDigest,
                entry.TargetImportDigest!,
                activated)));
        _ = directoryInspection.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(_ => new IdempotencyAdmissionDirectoryResult(
                targetReference.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        await actor.RegisterAsync([targetReference]);
        var request = new IdempotencyLegacyMigrationRequest(
            [alias],
            targetReference,
            "target-verification",
            "target-intent",
            IdempotencyReplayRetentionTier.Mutation,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);
        IdempotencyLegacyMigrationResult reproved = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        reproved.DeniedDecision.ShouldBeNull();
        result.TargetAdmissionActorId.ShouldBe(targetReference.ActorId);
        entry.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.Migrated);
        calls.IndexOf("prepare-target").ShouldBeLessThan(calls.IndexOf("checkpoint:Inventoried"));
        calls.IndexOf("redirect-source").ShouldBeLessThan(calls.IndexOf("checkpoint:TargetAcknowledged"));
        calls.IndexOf("prove-directory").ShouldBeLessThan(calls.IndexOf("checkpoint:SourceRedirected"));
        calls.IndexOf("activate-target").ShouldBeGreaterThan(calls.IndexOf("checkpoint:SourceRedirected"));
        calls.IndexOf("activate-target").ShouldBeLessThan(calls.IndexOf("checkpoint:AuthorityFlipped"));
    }

    [Fact]
    public async Task MigrateLegacyAsync_DeletionWinsBeforeAnySourceOrTargetWork()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        var target = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([target]);
        _ = await actor.EnterDeletionAsync(_now);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            ((IIdempotencyTenantLifecycleMigrationActor)actor).MigrateLegacyAsync(
                new IdempotencyLegacyMigrationRequest(
                    [new IdempotencyAdmissionDirectoryAlias("v1", target.ActorId, "key-a")],
                    target,
                    "target-verification",
                    "target-intent",
                    IdempotencyReplayRetentionTier.Mutation,
                    "source-verification",
                    "source-intent",
                    IdempotencyReplayRetentionTier.Mutation)));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyLegacySourceActor>(
            default!,
            default!);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Theory]
    [InlineData(IdempotencyLegacyMigrationPhase.Inventoried)]
    [InlineData(IdempotencyLegacyMigrationPhase.TargetPrepared)]
    [InlineData(IdempotencyLegacyMigrationPhase.TargetAcknowledged)]
    [InlineData(IdempotencyLegacyMigrationPhase.SourceRedirected)]
    [InlineData(IdempotencyLegacyMigrationPhase.AuthorityFlipped)]
    [InlineData(IdempotencyLegacyMigrationPhase.Migrated)]
    public async Task MigrateLegacyAsync_RestartFromEveryDurablePhaseFinishesPinnedTarget(
        IdempotencyLegacyMigrationPhase initialPhase)
    {
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, Func<IdempotencyLegacyMigrationPhase> phase)
            = await CreateResumableMigrationAsync(initialPhase);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        result.TargetAdmissionActorId.ShouldBe(request.Target.ActorId);
        phase().ShouldBe(IdempotencyLegacyMigrationPhase.Migrated);
    }

    [Fact]
    public async Task MigrateLegacyAsync_MigratedMarkerWithMismatchedRedirectRefusesReplayAuthority()
    {
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.Migrated,
                invalidCompletedRedirect: true);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBe(IdempotencyAdmissionDecision.UnsafeLegacy);
    }

    [Fact]
    public async Task MigrateLegacyAsync_ExpiredExactSourcePreparesOnlyFenceFreeTombstone()
    {
        IdempotencyAdmissionPromotionImportRequest? prepared = null;
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.Inventoried,
                expired: true,
                onPrepare: value => prepared = value);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        prepared.ShouldNotBeNull().Record.ShouldBeNull();
        prepared.Tombstone.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Expired);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(120)]
    public async Task MigrateLegacyAsync_RestartUsesPersistedTargetDigestAcrossClockAndExpiry(
        int minutesAdvanced)
    {
        string? acknowledgedDigest = null;
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.TargetPrepared,
                clockAdvance: TimeSpan.FromMinutes(minutesAdvanced),
                onAcknowledge: value => acknowledgedDigest = value.ImportDigest);
        IdempotencyLegacyInventoryEntry entry = LegacyEntry();
        var imported = new IdempotencyAdmissionRecord(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            IdempotencyAdmissionState.Terminal,
            entry.TenantPartition,
            request.Target.DigestKeyVersion,
            request.Target.KeyDigest,
            request.TargetVerificationTag,
            request.TargetIntentDigest,
            entry.RetentionTier,
            entry.FirstConsumedAt,
            entry.LastObservedAt,
            entry.ReplayExpiresAt,
            1,
            entry.ReplayResult,
            entry.ExecutionMessageId,
            entry.ExecutionCorrelationId);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        acknowledgedDigest.ShouldBe(IdempotencyAdmissionPromotionEvidence.Compute(imported, null));
    }

    [Fact]
    public async Task MigrateLegacyAsync_LostActivationResponseSkipsDuplicateActivationAndAdvances()
    {
        int activationCalls = 0;
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, Func<IdempotencyLegacyMigrationPhase> phase)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.AuthorityFlipped,
                alreadyActivated: true,
                onActivate: () => activationCalls++);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        phase().ShouldBe(IdempotencyLegacyMigrationPhase.Migrated);
        activationCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MigrateLegacyAsync_MigratedReproofFollowsOnlyProvedForwardRotationChain(int hops)
    {
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, string expected)
            = await CreateMigratedRotationAsync(hops, invalidChain: false);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBeNull();
        result.TargetAdmissionActorId.ShouldBe(expected);
    }

    [Fact]
    public async Task MigrateLegacyAsync_InvalidForwardRotationChainDeniesWithoutMovingAuthorityBackward()
    {
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateMigratedRotationAsync(2, invalidChain: true);

        IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
            .MigrateLegacyAsync(request);

        result.DeniedDecision.ShouldBe(IdempotencyAdmissionDecision.UnsafeLegacy);
    }

    [Theory]
    [InlineData((int)IdempotencyLegacySourceDecision.Missing)]
    [InlineData((int)IdempotencyLegacySourceDecision.Unsupported)]
    [InlineData((int)IdempotencyLegacySourceDecision.Unavailable)]
    [InlineData((int)IdempotencyLegacySourceDecision.Conflict)]
    public async Task MigrateLegacyAsync_UnsafeSourceEvidenceNeverPreparesTarget(
        int sourceDecisionValue)
    {
        var sourceDecision = (IdempotencyLegacySourceDecision)sourceDecisionValue;
        bool prepared = false;
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.Inventoried,
                onPrepare: _ => prepared = true,
                sourceDecision: sourceDecision);

        if (sourceDecision == IdempotencyLegacySourceDecision.Unavailable)
        {
            InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                ((IIdempotencyTenantLifecycleMigrationActor)actor).MigrateLegacyAsync(request));
            exception.Message.ShouldContain("temporarily unavailable");
        }
        else
        {
            IdempotencyLegacyMigrationResult result = await ((IIdempotencyTenantLifecycleMigrationActor)actor)
                .MigrateLegacyAsync(request);
            result.DeniedDecision.ShouldBe(
                sourceDecision == IdempotencyLegacySourceDecision.Conflict
                    ? IdempotencyAdmissionDecision.Conflict
                    : IdempotencyAdmissionDecision.UnsafeLegacy);
        }

        prepared.ShouldBeFalse();
    }

    [Fact]
    public async Task MigrateLegacyAsync_UnavailableSourceNeverPreparesTarget()
    {
        bool prepared = false;
        (IdempotencyTenantLifecycleActor actor, IdempotencyLegacyMigrationRequest request, _)
            = await CreateResumableMigrationAsync(
                IdempotencyLegacyMigrationPhase.Inventoried,
                onPrepare: _ => prepared = true,
                sourceFailure: new InvalidOperationException("bounded source unavailable"));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ((IIdempotencyTenantLifecycleMigrationActor)actor).MigrateLegacyAsync(request));

        exception.Message.ShouldBe("Legacy source evidence is unavailable or inconsistent.");
        prepared.ShouldBeFalse();
    }

    [Fact]
    public async Task RollbackLegacyAsync_PreRedirectRollsBackTargetBeforeInventoryCheckpoint()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        var targetReference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        IdempotencyLegacyInventoryEntry source = LegacyEntry() with
        {
            Phase = IdempotencyLegacyMigrationPhase.TargetPrepared,
            TargetAdmissionActorId = targetReference.ActorId,
            TargetImportDigest = "target-import-digest",
        };
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                new ActorId(targetReference.ActorId),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(target);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                new ActorId("tenant-a"),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = target.RollbackPromotionAsync(Arg.Any<IdempotencyAdmissionPromotionRollbackRequest>())
            .Returns(Task.CompletedTask);
        _ = inventory.RollbackAsync(Arg.Any<IdempotencyLegacyMigrationRollbackRequest>())
            .Returns(source with
            {
                Phase = IdempotencyLegacyMigrationPhase.Inventoried,
                TargetAdmissionActorId = null,
                TargetImportDigest = null,
            });
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        await actor.RegisterAsync([targetReference]);

        await ((IIdempotencyTenantLifecycleMigrationActor)actor).RollbackLegacyAsync(
            new IdempotencyLegacyLifecycleRollbackRequest(
                targetReference,
                source.SourceAggregateActorId,
                source.SourceEvidenceDigest,
                source.InventoryId,
                source.MigrationId,
                source.DigestKeyVersion,
                source.KeyDigest,
                source.Phase,
                source.TargetImportDigest!));

        Received.InOrder(() =>
        {
            _ = target.RollbackPromotionAsync(Arg.Any<IdempotencyAdmissionPromotionRollbackRequest>());
            _ = inventory.RollbackAsync(Arg.Any<IdempotencyLegacyMigrationRollbackRequest>());
        });
    }

    [Fact]
    public async Task RegisterAsync_ExistingReferenceAfterDeletionIsStillDenied()
    {
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor();
        var reference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        await actor.RegisterAsync([reference]);
        _ = await actor.EnterDeletionAsync(_now);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.RegisterAsync([reference]));

        exception.Message.ShouldContain("forbids idempotency admission");
    }

    [Fact]
    public async Task LegalHold_PausesAndResumesRemainingIntervalAtInclusiveBoundary()
    {
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor();
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(100));

        IdempotencyTenantLifecycleRecord held = await actor.PlaceLegalHoldAsync(time.GetUtcNow());
        time.Advance(TimeSpan.FromDays(50));
        IdempotencyTenantLifecycleRecord stillHeld = await actor.GetAsync();
        IdempotencyTenantLifecycleRecord resumed = await actor.ReleaseLegalHoldAsync(time.GetUtcNow());
        time.Advance(TimeSpan.FromDays(300));
        IdempotencyTenantLifecycleRecord eligible = await actor.GetAsync();

        held.RemainingRetention.ShouldBe(TimeSpan.FromDays(300));
        stillHeld.State.ShouldBe(IdempotencyTenantLifecycleState.LegalHold);
        stillHeld.RemainingRetention.ShouldBe(TimeSpan.FromDays(300));
        resumed.DeleteAfter.ShouldBe(_now.AddDays(450));
        eligible.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
    }

    [Fact]
    public async Task PurgeAsync_BoundedBatchesMarkPurgedOnlyAfterEveryProtectedReference()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(true);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-b", "v1", "key-b"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord oneRemaining = await actor.PurgeAsync(1);
        IdempotencyTenantLifecycleRecord purged = await actor.PurgeAsync(1);

        oneRemaining.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        oneRemaining.References.ShouldHaveSingleItem();
        purged.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        purged.References.ShouldBeEmpty();
        await admission.Received(2).PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>());
        await directory.Received(2).PurgeAliasAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias>());
        await inventory.Received(1).PurgeAsync(
            new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-a", "key-a"));
        await inventory.Received(1).PurgeAsync(
            new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-b", "key-b"));
    }

    [Fact]
    public async Task PurgeAsync_EligibleStateDeletesAndAcknowledgesInsideLifecycleTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(true);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord result = await actor.PurgeAsync(1);

        result.State.ShouldBe(IdempotencyTenantLifecycleState.Purged);
        result.References.ShouldBeEmpty();
        await admission.Received(1).PurgeTombstoneAsync(
            new IdempotencyAdmissionPurgeRequest("tenant-a", "v1", "key-a"));
        await directory.Received(1).PurgeAliasAsync(
            new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-a", "key-a"));
        await inventory.Received(1).PurgeAsync(
            new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-a", "key-a"));
    }

    [Fact]
    public async Task PurgeAsync_LegalHoldWinsSerializedEligibilityAndDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));
        _ = await actor.PlaceLegalHoldAsync(time.GetUtcNow());

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_LiveAdmissionRetainsGovernedReferenceAndDirectoryAlias()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = admission.PurgeTombstoneAsync(Arg.Any<IdempotencyAdmissionPurgeRequest>()).Returns(false);
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync(
        [
            new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a"),
        ]);
        _ = await actor.EnterDeletionAsync(_now);
        time.Advance(TimeSpan.FromDays(400));

        IdempotencyTenantLifecycleRecord result = await actor.PurgeAsync(1);

        result.State.ShouldBe(IdempotencyTenantLifecycleState.PurgeEligible);
        result.References.ShouldHaveSingleItem();
        await directory.DidNotReceive().PurgeAliasAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias>());
    }

    [Fact]
    public async Task PurgeAsync_CorruptLifecycleDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
        var corrupt = new IdempotencyTenantLifecycleRecord(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            "tenant-a",
            IdempotencyTenantLifecycleState.Active,
            _now,
            _now,
            null,
            null,
            null,
            []);
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_ContradictoryEligibleRemainderOrFutureDeadlineDeletesNothing()
    {
        IdempotencyTenantLifecycleRecord valid = PurgeEligibleRecord();
        IdempotencyTenantLifecycleRecord[] corruptVariants =
        [
            valid with { RemainingRetention = TimeSpan.FromDays(1) },
            valid with { DeleteAfter = _now.AddDays(1) },
        ];

        foreach (IdempotencyTenantLifecycleRecord corrupt in corruptVariants)
        {
            IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
            (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
            _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                    IdempotencyTenantLifecycleActor.StateName,
                    Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

            _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

            _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
                default!,
                default!);
            _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                default!,
                default!);
        }
    }

    [Fact]
    public async Task PurgeAsync_UnboundReferenceDeletesNothing()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor(factory);
        IdempotencyTenantLifecycleRecord corrupt = PurgeEligibleRecord() with
        {
            References =
            [
                new IdempotencyTenantLifecycleReference("tenant-b:v1:key-a", "v1", "key-a"),
            ],
        };
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, corrupt));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.PurgeAsync(1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_OversizedActorTurnIsRejectedBeforeDeletion()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => actor.PurgeAsync(IdempotencyTenantLifecycleActor.MaximumReferencesPerPurgeTurn + 1));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task AcknowledgePurgeAsync_DirectCallerCannotBypassSerializedDeletion()
    {
        (IdempotencyTenantLifecycleActor actor, IActorStateManager stateManager, _) = CreateActor();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.AcknowledgePurgeAsync("tenant-a:v1:key-a"));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeTombstoneAsync_RemovesOnlyExactCompactedStateAndPromotionMetadata()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a:v1:key-a") });
        var actor = new IdempotencyAdmissionActor(
            host,
            NullLogger<IdempotencyAdmissionActor>.Instance,
            new FakeTimeProvider(_now));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        var tombstone = new IdempotencyAdmissionTombstone(
            IdempotencyAdmissionTombstone.CurrentSchemaVersion,
            IdempotencyAdmissionState.Expired,
            "tenant-a",
            "key-a",
            "tag-a",
            "v1",
            Hexalith.EventStore.Contracts.Commands.IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-2),
            _now.AddDays(-1),
            _now);
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionTombstone>(true, tombstone));

        bool purged = await actor.PurgeTombstoneAsync(
            new IdempotencyAdmissionPurgeRequest("tenant-a", "v1", "key-a"));

        purged.ShouldBeTrue();
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.RedirectStateName,
            Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.PromotionStateName,
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    private static (IdempotencyTenantLifecycleActor Actor, IActorStateManager StateManager, FakeTimeProvider Time) CreateActor(
        IActorProxyFactory? actorProxyFactory = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var time = new FakeTimeProvider(_now);
        IdempotencyTenantLifecycleRecord? stored = null;
        _ = stateManager.TryGetStateAsync<IdempotencyTenantLifecycleRecord>(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => stored is null
                ? new ConditionalValue<IdempotencyTenantLifecycleRecord>(false, default!)
                : new ConditionalValue<IdempotencyTenantLifecycleRecord>(true, stored));
        _ = stateManager.SetStateAsync(
                IdempotencyTenantLifecycleActor.StateName,
                Arg.Do<IdempotencyTenantLifecycleRecord>(record => stored = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ActorHost host = ActorHost.CreateForTest<IdempotencyTenantLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a") });
        var actor = new IdempotencyTenantLifecycleActor(
            host,
            NullLogger<IdempotencyTenantLifecycleActor>.Instance,
            time,
            actorProxyFactory);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, time);
    }

    private static IdempotencyAdmissionRequest AdmissionRequest()
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-a",
            "verification-tag",
            "intent-digest",
            IdempotencyReplayRetentionTier.Mutation,
            "01J00000000000000000000000",
            "trace-a");

    private static IdempotencyTenantLifecycleRecord PurgeEligibleRecord()
        => new(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            "tenant-a",
            IdempotencyTenantLifecycleState.PurgeEligible,
            _now,
            _now.AddDays(-401),
            _now.AddDays(-1),
            TimeSpan.Zero,
            null,
            [new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a")]);

    private static IdempotencyLegacyInventoryEntry LegacyEntry()
        => new(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            "tenant-a",
            "tenant-a:folders:legacy-folder",
            "source-evidence",
            1,
            "v1",
            "key-a",
            "source-verification",
            "source-intent",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddHours(-1),
            _now,
            _now.AddHours(1),
            new CommandProcessingResult(true, CorrelationId: "trace-original", ResultPayload: "same"),
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyLegacyMigrationPhase.Inventoried,
            "inventory-2026-08",
            1,
            "migration-01J00000000000000000000000");

    private static async Task<(
        IdempotencyTenantLifecycleActor Actor,
        IdempotencyLegacyMigrationRequest Request,
        Func<IdempotencyLegacyMigrationPhase> Phase)> CreateResumableMigrationAsync(
        IdempotencyLegacyMigrationPhase initialPhase,
        bool invalidCompletedRedirect = false,
        bool expired = false,
        Action<IdempotencyAdmissionPromotionImportRequest>? onPrepare = null,
        IdempotencyLegacySourceDecision? sourceDecision = null,
        Exception? sourceFailure = null,
        TimeSpan? clockAdvance = null,
        Action<IdempotencyAdmissionPromotionAcknowledgementRequest>? onAcknowledge = null,
        bool alreadyActivated = false,
        Action? onActivate = null)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IIdempotencyLegacySourceActor source = Substitute.For<IIdempotencyLegacySourceActor>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyAdmissionDirectoryInspectionActor directoryInspection
            = Substitute.For<IIdempotencyAdmissionDirectoryInspectionActor>();
        var targetReference = new IdempotencyTenantLifecycleReference("tenant-a:v1:key-a", "v1", "key-a");
        var alias = new IdempotencyAdmissionDirectoryAlias("v1", targetReference.ActorId, "key-a");
        var request = new IdempotencyLegacyMigrationRequest(
            [alias],
            targetReference,
            "target-verification",
            "target-intent",
            IdempotencyReplayRetentionTier.Mutation,
            "source-verification",
            "source-intent",
            IdempotencyReplayRetentionTier.Mutation);
        IdempotencyLegacyInventoryEntry entry = expired
            ? LegacyEntry() with
            {
                ReplayExpiresAt = _now.AddMinutes(-1),
                LastObservedAt = _now,
            }
            : LegacyEntry();
        IdempotencyAdmissionRecord? imported = expired
            ? null
            : new IdempotencyAdmissionRecord(
                IdempotencyAdmissionRecord.CurrentSchemaVersion,
                IdempotencyAdmissionState.Terminal,
                entry.TenantPartition,
                request.Target.DigestKeyVersion,
                request.Target.KeyDigest,
                request.TargetVerificationTag,
                request.TargetIntentDigest,
                entry.RetentionTier,
                entry.FirstConsumedAt,
                entry.LastObservedAt,
                entry.ReplayExpiresAt,
                1,
                entry.ReplayResult,
                entry.ExecutionMessageId,
                entry.ExecutionCorrelationId);
        IdempotencyAdmissionTombstone? importedTombstone = expired
            ? new IdempotencyAdmissionTombstone(
                IdempotencyAdmissionTombstone.CurrentSchemaVersion,
                IdempotencyAdmissionState.Expired,
                entry.TenantPartition,
                request.Target.KeyDigest,
                request.TargetVerificationTag,
                request.Target.DigestKeyVersion,
                entry.RetentionTier,
                entry.FirstConsumedAt,
                entry.ReplayExpiresAt,
                entry.LastObservedAt)
            : null;
        string importDigest = IdempotencyAdmissionPromotionEvidence.Compute(imported, importedTombstone);
        bool hasTarget = initialPhase != IdempotencyLegacyMigrationPhase.Inventoried;
        bool hasRedirect = initialPhase is IdempotencyLegacyMigrationPhase.SourceRedirected
            or IdempotencyLegacyMigrationPhase.AuthorityFlipped
            or IdempotencyLegacyMigrationPhase.Migrated;
        bool activated = alreadyActivated || initialPhase == IdempotencyLegacyMigrationPhase.Migrated;
        entry = entry with
        {
            Phase = initialPhase,
            TargetAdmissionActorId = hasTarget ? targetReference.ActorId : null,
            TargetImportDigest = hasTarget ? importDigest : null,
            SourceRedirectDigest = hasRedirect ? "redirect-digest" : null,
        };
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = factory.CreateActorProxy<IIdempotencyLegacySourceActor>(
                Arg.Any<ActorId>(),
                nameof(AggregateActor))
            .Returns(source);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(target);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directoryInspection);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(_ => new IdempotencyLegacyInventoryInspection(
                entry.Phase == IdempotencyLegacyMigrationPhase.Migrated
                    ? IdempotencyLegacyInventoryDecision.Migrated
                    : IdempotencyLegacyInventoryDecision.Migrate,
                entry));
        _ = inventory.AdvanceAsync(Arg.Any<IdempotencyLegacyMigrationAdvanceRequest>())
            .Returns(callInfo =>
            {
                IdempotencyLegacyMigrationAdvanceRequest advance = callInfo
                    .ArgAt<IdempotencyLegacyMigrationAdvanceRequest>(0);
                IdempotencyLegacyMigrationPhase next = advance.ExpectedPhase switch
                {
                    IdempotencyLegacyMigrationPhase.Inventoried => IdempotencyLegacyMigrationPhase.TargetPrepared,
                    IdempotencyLegacyMigrationPhase.TargetPrepared => IdempotencyLegacyMigrationPhase.TargetAcknowledged,
                    IdempotencyLegacyMigrationPhase.TargetAcknowledged => IdempotencyLegacyMigrationPhase.SourceRedirected,
                    IdempotencyLegacyMigrationPhase.SourceRedirected => IdempotencyLegacyMigrationPhase.AuthorityFlipped,
                    IdempotencyLegacyMigrationPhase.AuthorityFlipped => IdempotencyLegacyMigrationPhase.Migrated,
                    _ => throw new InvalidOperationException(),
                };
                entry = entry with
                {
                    Phase = next,
                    TargetAdmissionActorId = advance.TargetAdmissionActorId,
                    TargetImportDigest = advance.TargetImportDigest,
                    SourceRedirectDigest = advance.SourceRedirectDigest ?? entry.SourceRedirectDigest,
                };
                return entry;
            });
        _ = source.InspectLegacySourceAsync(Arg.Any<IdempotencyLegacySourceRequest>())
            .Returns(_ => sourceFailure is not null
                ? throw sourceFailure
                : hasRedirect
                    ? new IdempotencyLegacySourceInspection(
                        IdempotencyLegacySourceDecision.Redirected,
                        invalidCompletedRedirect ? "different-redirect-digest" : "redirect-digest")
                    : new IdempotencyLegacySourceInspection(
                        sourceDecision
                            ?? (expired
                                ? IdempotencyLegacySourceDecision.Expired
                                : IdempotencyLegacySourceDecision.Exact)));
        _ = source.SetLegacySourceRedirectAsync(Arg.Any<IdempotencyLegacySourceRedirectRequest>())
            .Returns(_ =>
            {
                hasRedirect = true;
                return new IdempotencyLegacySourceInspection(
                    IdempotencyLegacySourceDecision.Redirected,
                    "redirect-digest");
            });
        _ = target.PreparePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionImportRequest>())
            .Returns(callInfo =>
            {
                onPrepare?.Invoke(callInfo.ArgAt<IdempotencyAdmissionPromotionImportRequest>(0));
                return Task.CompletedTask;
            });
        _ = target.AcknowledgePromotionAsync(
                Arg.Any<IdempotencyAdmissionPromotionAcknowledgementRequest>())
            .Returns(callInfo =>
            {
                IdempotencyAdmissionPromotionAcknowledgementRequest acknowledgement = callInfo
                    .ArgAt<IdempotencyAdmissionPromotionAcknowledgementRequest>(0);
                onAcknowledge?.Invoke(acknowledgement);
                return new IdempotencyAdmissionPromotionAcknowledgement(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    acknowledgement.SourceActorId,
                    acknowledgement.MigrationId,
                    acknowledgement.SourceEvidenceDigest,
                    acknowledgement.ImportDigest,
                    activated);
            });
        _ = target.ActivatePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionActivationRequest>())
            .Returns(_ =>
            {
                onActivate?.Invoke();
                activated = true;
                return Task.CompletedTask;
            });
        _ = target.InspectAsync().Returns(_ => new IdempotencyAdmissionInspection(
            true,
            Promotion: new IdempotencyAdmissionPromotionAcknowledgement(
                IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                entry.SourceAggregateActorId,
                entry.MigrationId,
                entry.SourceEvidenceDigest,
                importDigest,
                activated)));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                targetReference.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        _ = directoryInspection.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(_ => new IdempotencyAdmissionDirectoryResult(
                targetReference.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        (IdempotencyTenantLifecycleActor actor, _, FakeTimeProvider time) = CreateActor(factory);
        await actor.RegisterAsync([targetReference]);
        if (clockAdvance is not null)
        {
            time.Advance(clockAdvance.Value);
        }

        return (actor, request, () => entry.Phase);
    }

    private static async Task<(
        IdempotencyTenantLifecycleActor Actor,
        IdempotencyLegacyMigrationRequest Request,
        string ExpectedAuthority)> CreateMigratedRotationAsync(int hops, bool invalidChain)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IIdempotencyLegacySourceActor source = Substitute.For<IIdempotencyLegacySourceActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyAdmissionDirectoryInspectionActor directoryInspection
            = Substitute.For<IIdempotencyAdmissionDirectoryInspectionActor>();
        IdempotencyLegacyInventoryEntry entry = LegacyEntry() with
        {
            Phase = IdempotencyLegacyMigrationPhase.Migrated,
            TargetAdmissionActorId = "tenant-a:v1:key-a",
            TargetImportDigest = "target-import-digest",
            SourceRedirectDigest = "source-redirect-digest",
        };
        IdempotencyAdmissionDirectoryAlias[] aliases = Enumerable.Range(1, hops + 1)
            .Select(version => new IdempotencyAdmissionDirectoryAlias(
                $"v{version}",
                $"tenant-a:v{version}:key-{version}",
                $"key-{version}"))
            .ToArray();
        aliases[0] = new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:key-a", "key-a");
        IdempotencyTenantLifecycleReference[] references = aliases
            .Select(alias => new IdempotencyTenantLifecycleReference(
                alias.ActorId,
                alias.DigestKeyVersion,
                alias.KeyDigest))
            .ToArray();
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = factory.CreateActorProxy<IIdempotencyLegacySourceActor>(
                Arg.Any<ActorId>(),
                nameof(AggregateActor))
            .Returns(source);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directoryInspection);
        var admissions = aliases.ToDictionary(
            static alias => alias.ActorId,
            static _ => Substitute.For<IIdempotencyAdmissionActor>(),
            StringComparer.Ordinal);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(callInfo => admissions[callInfo.ArgAt<ActorId>(0).GetId()]);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(
                IdempotencyLegacyInventoryDecision.Migrated,
                entry));
        _ = source.InspectLegacySourceAsync(Arg.Any<IdempotencyLegacySourceRequest>())
            .Returns(new IdempotencyLegacySourceInspection(
                IdempotencyLegacySourceDecision.Redirected,
                entry.SourceRedirectDigest));
        _ = admissions[aliases[0].ActorId].AcknowledgePromotionAsync(
                Arg.Any<IdempotencyAdmissionPromotionAcknowledgementRequest>())
            .Returns(callInfo =>
            {
                IdempotencyAdmissionPromotionAcknowledgementRequest proof = callInfo
                    .ArgAt<IdempotencyAdmissionPromotionAcknowledgementRequest>(0);
                return new IdempotencyAdmissionPromotionAcknowledgement(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    proof.SourceActorId,
                    proof.MigrationId,
                    proof.SourceEvidenceDigest,
                    proof.ImportDigest,
                    Activated: true);
            });
        for (int index = 0; index < aliases.Length; index++)
        {
            int captured = index;
            string? redirect = captured + 1 < aliases.Length ? aliases[captured + 1].ActorId : null;
            IdempotencyAdmissionPromotionAcknowledgement? promotion = captured == 0
                ? null
                : new IdempotencyAdmissionPromotionAcknowledgement(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    invalidChain && captured == aliases.Length - 1
                        ? "tenant-a:v9:wrong-source"
                        : aliases[captured - 1].ActorId,
                    IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
                        aliases[captured - 1].ActorId,
                        aliases[captured].ActorId),
                    "ordinary-import",
                    "ordinary-import",
                    Activated: true);
            _ = admissions[aliases[captured].ActorId].InspectAsync()
                .Returns(new IdempotencyAdmissionInspection(
                    true,
                    RedirectActorId: redirect,
                    Promotion: promotion));
        }

        _ = directoryInspection.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                aliases[^1].ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        (IdempotencyTenantLifecycleActor actor, _, _) = CreateActor(factory);
        await actor.RegisterAsync(references);
        var request = new IdempotencyLegacyMigrationRequest(
            aliases,
            references[0],
            "target-verification",
            "target-intent",
            IdempotencyReplayRetentionTier.Mutation,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier);
        return (actor, request, aliases[^1].ActorId);
    }
}
