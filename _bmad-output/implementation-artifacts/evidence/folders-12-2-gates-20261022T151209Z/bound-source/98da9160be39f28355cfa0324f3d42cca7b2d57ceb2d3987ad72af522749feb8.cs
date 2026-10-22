using System.Net;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Production-process OQ8 proof against the tracked PostgreSQL actor-state profile.</summary>
[Collection("Oq8Postgresql")]
[Trait("Category", "LiveSidecar")]
[Trait("Profile", "oq8-postgresql-v1")]
public sealed class IdempotencyAdmissionOq8PostgresqlTests(Oq8PostgresqlFixture fixture)
{
    /// <summary>
    /// Covers writers/failover, inclusive expiry/compaction, authority changes, and sanitized capture.
    /// Deterministic fault oracles remain separately exercised by the Server.Tests lane.
    /// </summary>
    [Fact]
    public async Task ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants()
    {
        const string Tenant = "tenant-oq8";
        const string GovernanceTenant = "tenant-oq8-governance";
        const string Payload = "{\"amount\":1}";
        const string DifferentPayload = "{\"amount\":2}";
        string writerKey = $"{Oq8PostgresqlFixture.ProtectedRawKeyPrefix}-WRITERS";
        string expiryKey = $"{Oq8PostgresqlFixture.ProtectedRawKeyPrefix}-EXPIRY";
        string rotationKey = $"{Oq8PostgresqlFixture.ProtectedRawKeyPrefix}-ROTATION";
        string governanceKey = $"{Oq8PostgresqlFixture.ProtectedRawKeyPrefix}-GOVERNANCE";
        string writerAggregate = $"oq8-writers-{Guid.NewGuid():N}";
        string crossTargetAggregate = $"oq8-cross-target-{Guid.NewGuid():N}";
        string expiryAggregate = $"oq8-expiry-{Guid.NewGuid():N}";
        string rotationAggregate = $"oq8-rotation-{Guid.NewGuid():N}";
        string governanceAggregate = $"oq8-governance-{Guid.NewGuid():N}";

        fixture.HasIndependentProcesses.ShouldBeTrue();
        Oq8PostgresqlSnapshot before = await fixture
            .CapturePostgresqlSnapshotAsync("before")
            .ConfigureAwait(true);

        fixture.SetClock(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
        int writerSampleBefore = fixture.SampleBoundaryCount;
        Oq8CommandObservation[] writers = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(index => fixture.SubmitAsync(
                index % 2,
                Tenant,
                writerAggregate,
                writerKey,
                Payload))).ConfigureAwait(true);
        writers.ShouldAllBe(static result => result.StatusCode == HttpStatusCode.Accepted);
        string[] writerIdentities = [.. writers
            .Select(static result => result.MessageIdentitySha256)
            .Distinct(StringComparer.Ordinal)
            .OfType<string>()];
        writerIdentities.Length.ShouldBe(1);
        string writerIdentity = writerIdentities.Single();
        string[] writerResults = [.. writers
            .Select(static result => result.ResultSha256)
            .Distinct(StringComparer.Ordinal)
            .OfType<string>()];
        writerResults.Length.ShouldBe(1);
        string writerResult = writerResults.Single();
        await fixture.WaitForSampleBoundaryCountAsync(writerSampleBefore + 1).ConfigureAwait(true);
        int writerSampleAfterExecution = fixture.SampleBoundaryCount;
        Oq8AdmissionSnapshot terminal = await fixture
            .InspectAdmissionAsync(Tenant, writerKey)
            .ConfigureAwait(true);
        terminal.State.ShouldBe(IdempotencyAdmissionState.Terminal);
        terminal.FencingToken.ShouldBeGreaterThan(0);
        terminal.HasIntent.ShouldBeTrue();
        terminal.HasReplay.ShouldBeTrue();
        string terminalReplay = terminal.ReplaySha256.ShouldNotBeNull();
        terminalReplay.ShouldBe(writerResult);
        bool terminalBoundaryObserved = terminal.State == IdempotencyAdmissionState.Terminal
            && terminal.FencingToken > 0
            && terminal.HasReplay
            && terminal.ReplaySha256 is not null;

        int owner = await fixture.FindAdmissionOwnerAsync().ConfigureAwait(true);
        int survivor = owner == 0 ? 1 : 0;
        await fixture.StopEventStoreNodeAsync(owner).ConfigureAwait(true);
        (Oq8CommandObservation failoverReplay, int failoverAttempts) = await fixture
            .SubmitAfterFailoverAsync(survivor, Tenant, writerAggregate, writerKey, Payload)
            .ConfigureAwait(true);
        failoverReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        failoverReplay.MessageIdentitySha256.ShouldBe(writerIdentity);
        failoverReplay.ResultSha256.ShouldNotBeNull().ShouldBe(terminalReplay);
        await fixture.WaitForSampleBoundaryCountAsync(writerSampleAfterExecution).ConfigureAwait(true);

        await fixture.RestartEventStoreNodeAsync(owner).ConfigureAwait(true);
        Oq8CommandObservation restartedReplay = await fixture
            .SubmitAsync(owner, Tenant, writerAggregate, writerKey, Payload)
            .ConfigureAwait(true);
        restartedReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        restartedReplay.MessageIdentitySha256.ShouldBe(writerIdentity);
        restartedReplay.ResultSha256.ShouldNotBeNull().ShouldBe(terminalReplay);
        Oq8CommandObservation conflict = await fixture
            .SubmitAsync(survivor, Tenant, writerAggregate, writerKey, DifferentPayload)
            .ConfigureAwait(true);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        conflict.ReasonCode.ShouldBe("idempotency_conflict");
        Oq8CommandObservation crossTargetConflict = await fixture
            .SubmitAsync(survivor, Tenant, crossTargetAggregate, writerKey, Payload)
            .ConfigureAwait(true);
        crossTargetConflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        crossTargetConflict.ReasonCode.ShouldBe("idempotency_conflict");
        await fixture.WaitForSampleBoundaryCountAsync(writerSampleAfterExecution).ConfigureAwait(true);
        int writerSampleAfterNonExecute = fixture.SampleBoundaryCount;

        fixture.SetClock(new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero));
        int expirySampleBefore = fixture.SampleBoundaryCount;
        Oq8CommandObservation expiryExecution = await fixture
            .SubmitAsync(0, Tenant, expiryAggregate, expiryKey, Payload)
            .ConfigureAwait(true);
        expiryExecution.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string expiryResult = expiryExecution.ResultSha256.ShouldNotBeNull();
        await fixture.WaitForSampleBoundaryCountAsync(expirySampleBefore + 1).ConfigureAwait(true);
        Oq8AdmissionSnapshot expiryTerminal = await fixture
            .InspectAdmissionAsync(Tenant, expiryKey)
            .ConfigureAwait(true);
        DateTimeOffset expiresAt = expiryTerminal.ReplayExpiresAt.ShouldNotBeNull();
        expiryTerminal.State.ShouldBe(IdempotencyAdmissionState.Terminal);
        string expiryReplay = expiryTerminal.ReplaySha256.ShouldNotBeNull();
        expiryReplay.ShouldBe(expiryResult);
        int expirySampleAfterExecution = fixture.SampleBoundaryCount;

        fixture.SetClock(expiresAt.AddTicks(-1));
        Oq8CommandObservation beforeExpiry = await fixture
            .SubmitAsync(1, Tenant, expiryAggregate, expiryKey, Payload)
            .ConfigureAwait(true);
        beforeExpiry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        beforeExpiry.MessageIdentitySha256.ShouldBe(expiryExecution.MessageIdentitySha256);
        beforeExpiry.ResultSha256.ShouldNotBeNull().ShouldBe(expiryReplay);
        (await fixture.InspectAdmissionAsync(Tenant, expiryKey).ConfigureAwait(true))
            .State.ShouldBe(IdempotencyAdmissionState.Terminal);
        fixture.SampleBoundaryCount.ShouldBe(expirySampleAfterExecution);

        fixture.SetClock(expiresAt);
        Oq8CommandObservation atExpiry = await fixture
            .SubmitAsync(0, Tenant, expiryAggregate, expiryKey, Payload)
            .ConfigureAwait(true);
        atExpiry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        atExpiry.ReasonCode.ShouldBe("idempotency_key_expired");
        Oq8AdmissionSnapshot tombstone = await fixture
            .InspectAdmissionAsync(Tenant, expiryKey)
            .ConfigureAwait(true);
        tombstone.State.ShouldBe(IdempotencyAdmissionState.Expired);
        tombstone.FencingToken.ShouldBe(0);
        tombstone.HasIntent.ShouldBeFalse();
        tombstone.HasReplay.ShouldBeFalse();
        tombstone.HasExecutionIdentity.ShouldBeFalse();
        tombstone.IsMinimalTombstone.ShouldBeTrue();

        fixture.SetClock(expiresAt.AddTicks(1));
        Oq8CommandObservation afterExpiry = await fixture
            .SubmitAsync(1, Tenant, expiryAggregate, expiryKey, DifferentPayload)
            .ConfigureAwait(true);
        afterExpiry.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        afterExpiry.ReasonCode.ShouldBe(atExpiry.ReasonCode);
        fixture.SampleBoundaryCount.ShouldBe(expirySampleAfterExecution);
        int expirySampleAfterNonExecute = fixture.SampleBoundaryCount;

        fixture.SetClock(new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.Zero));
        int authoritySampleBefore = fixture.SampleBoundaryCount;
        Oq8CommandObservation rotationExecution = await fixture
            .SubmitAsync(0, Tenant, rotationAggregate, rotationKey, Payload)
            .ConfigureAwait(true);
        rotationExecution.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string rotationResult = rotationExecution.ResultSha256.ShouldNotBeNull();
        await fixture.WaitForSampleBoundaryCountAsync(authoritySampleBefore + 1).ConfigureAwait(true);
        Oq8AdmissionSnapshot preRotationTerminal = await fixture
            .InspectAdmissionAsync(Tenant, rotationKey)
            .ConfigureAwait(true);
        string preRotationReplay = preRotationTerminal.ReplaySha256.ShouldNotBeNull();
        preRotationReplay.ShouldBe(rotationResult);
        int authoritySampleAfterExecution = fixture.SampleBoundaryCount;
        await fixture.RotateDigestAuthorityAsync().ConfigureAwait(true);
        Oq8CommandObservation rotatedReplay = await fixture
            .SubmitAsync(1, Tenant, rotationAggregate, rotationKey, Payload)
            .ConfigureAwait(true);
        rotatedReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        rotatedReplay.MessageIdentitySha256.ShouldBe(rotationExecution.MessageIdentitySha256);
        rotatedReplay.ResultSha256.ShouldNotBeNull().ShouldBe(preRotationReplay);
        Oq8RotatedAuthoritySnapshot rotatedAuthority = await fixture
            .HasCanonicalRotatedAuthorityAsync(Tenant, rotationKey)
            .ConfigureAwait(true);
        rotatedAuthority.IsCanonical.ShouldBeTrue();
        rotatedAuthority.DirectoryStable.ShouldBeTrue();
        rotatedAuthority.SourceRedirectValid.ShouldBeTrue();
        rotatedAuthority.TargetActivated.ShouldBeTrue();
        rotatedAuthority.CanonicalAuthorityCount.ShouldBe(1);
        fixture.SampleBoundaryCount.ShouldBe(authoritySampleAfterExecution);

        await fixture.RetireOldDigestReaderAsync().ConfigureAwait(true);
        Oq8CommandObservation retiredReaderReplay = await fixture
            .SubmitAsync(0, Tenant, rotationAggregate, rotationKey, Payload)
            .ConfigureAwait(true);
        retiredReaderReplay.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        retiredReaderReplay.MessageIdentitySha256.ShouldBe(rotationExecution.MessageIdentitySha256);
        retiredReaderReplay.ResultSha256.ShouldNotBeNull().ShouldBe(preRotationReplay);
        fixture.SampleBoundaryCount.ShouldBe(authoritySampleAfterExecution);

        DateTimeOffset deletionApprovedAt = new(2026, 8, 14, 9, 0, 0, TimeSpan.Zero);
        fixture.SetClock(deletionApprovedAt);
        Oq8CommandObservation governanceExecution = await fixture
            .SubmitAsync(1, GovernanceTenant, governanceAggregate, governanceKey, Payload)
            .ConfigureAwait(true);
        governanceExecution.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await fixture.WaitForSampleBoundaryCountAsync(authoritySampleAfterExecution + 1).ConfigureAwait(true);
        int authoritySampleAfterEligibleExecutions = fixture.SampleBoundaryCount;
        IdempotencyTenantLifecycleState deletionState = await fixture
            .EnterDeletionAsync(GovernanceTenant, deletionApprovedAt)
            .ConfigureAwait(true);
        deletionState.ShouldBe(IdempotencyTenantLifecycleState.Retaining);

        DateTimeOffset holdAt = deletionApprovedAt.AddDays(10);
        fixture.SetClock(holdAt);
        IdempotencyTenantLifecycleState legalHoldState = await fixture
            .PlaceLegalHoldAsync(GovernanceTenant, holdAt)
            .ConfigureAwait(true);
        legalHoldState.ShouldBe(IdempotencyTenantLifecycleState.LegalHold);
        Oq8CommandObservation heldRetry = await fixture
            .SubmitAsync(0, GovernanceTenant, governanceAggregate, governanceKey, Payload)
            .ConfigureAwait(true);
        heldRetry.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        heldRetry.ReasonCode.ShouldBe("idempotency_admission_unavailable");
        fixture.SampleBoundaryCount.ShouldBe(authoritySampleAfterEligibleExecutions);

        DateTimeOffset releaseAt = deletionApprovedAt.AddDays(20);
        fixture.SetClock(releaseAt);
        IdempotencyTenantLifecycleState releasedState = await fixture
            .ReleaseLegalHoldAsync(GovernanceTenant, releaseAt)
            .ConfigureAwait(true);
        releasedState.ShouldBe(IdempotencyTenantLifecycleState.Retaining);
        Oq8CommandObservation retainingRetry = await fixture
            .SubmitAsync(1, GovernanceTenant, governanceAggregate, governanceKey, Payload)
            .ConfigureAwait(true);
        retainingRetry.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        retainingRetry.ReasonCode.ShouldBe("idempotency_admission_unavailable");
        fixture.SampleBoundaryCount.ShouldBe(authoritySampleAfterEligibleExecutions);
        int authoritySampleAfterNonExecute = fixture.SampleBoundaryCount;

        Oq8PostgresqlSnapshot after = await fixture
            .CapturePostgresqlSnapshotAsync("after")
            .ConfigureAwait(true);
        after.ProtectedSentinelMatches.ShouldBe(0);
        after.AggregateSequenceTotal.ShouldBe(before.AggregateSequenceTotal + 4);
        after.AggregateMetadataRows.ShouldBe(before.AggregateMetadataRows + 4);
        after.AggregateEventRows.ShouldBe(before.AggregateEventRows + 4);
        after.MinimalTombstoneRows.ShouldBeGreaterThanOrEqualTo(before.MinimalTombstoneRows + 1);
        after.DirectoryRows.ShouldBeGreaterThan(before.DirectoryRows);
        after.LifecycleRows.ShouldBeGreaterThan(before.LifecycleRows);
        bool committedProjectionContainsIdentifiers = fixture.ContainsProtectedIdentifiers(new { before, after });
        committedProjectionContainsIdentifiers.ShouldBeFalse();

        fixture.RecordEvidence("writers_failover", new
        {
            concurrentRequests = writers.Length,
            canonicalExecutionIdentities = writerIdentities.Length,
            durableFencePositive = terminal.FencingToken > 0,
            sampleExecutions = writerSampleAfterExecution - writerSampleBefore,
            ownerStoppedAtTerminalBoundary = terminalBoundaryObserved,
            failoverAttempts,
            failoverReplayExact = failoverReplay.MessageIdentitySha256 == writerIdentity
                && failoverReplay.ResultSha256 == terminalReplay,
            restartedNodeReplayExact = restartedReplay.MessageIdentitySha256 == writerIdentity
                && restartedReplay.ResultSha256 == terminalReplay,
            conflictStatus = (int)conflict.StatusCode,
            crossTargetConflictStatus = (int)crossTargetConflict.StatusCode,
            nonExecuteAdditionalWork = writerSampleAfterNonExecute - writerSampleAfterExecution,
        });
        fixture.RecordEvidence("expiry_compaction", new
        {
            oneTickBefore = (int)beforeExpiry.StatusCode,
            oneTickBeforeReplayExact = beforeExpiry.ResultSha256 == expiryReplay,
            inclusiveBoundary = (int)atExpiry.StatusCode,
            oneTickAfter = (int)afterExpiry.StatusCode,
            terminalBecameMinimalTombstone = tombstone.IsMinimalTombstone,
            equivalentAndDifferentReuseShareOutcome = atExpiry.ReasonCode == afterExpiry.ReasonCode,
            nonExecuteAdditionalWork = expirySampleAfterNonExecute - expirySampleAfterExecution,
        });
        fixture.RecordEvidence("authority_change", new
        {
            rotationReplayExact = rotatedReplay.MessageIdentitySha256 == rotationExecution.MessageIdentitySha256
                && rotatedReplay.ResultSha256 == preRotationReplay,
            canonicalAuthorityCount = rotatedAuthority.CanonicalAuthorityCount,
            retiredReaderReplayExact = retiredReaderReplay.MessageIdentitySha256 == rotationExecution.MessageIdentitySha256
                && retiredReaderReplay.ResultSha256 == preRotationReplay,
            legalHoldState = legalHoldState.ToString(),
            releasedState = releasedState.ToString(),
            failClosedStatuses = new[] { (int)heldRetry.StatusCode, (int)retainingRetry.StatusCode },
            sampleExecutions = authoritySampleAfterEligibleExecutions - authoritySampleBefore,
            nonExecuteAdditionalWork = authoritySampleAfterNonExecute - authoritySampleAfterEligibleExecutions,
            deterministicSupportOracles = new[]
            {
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_PendingEquivalent_ReturnsFirstWriterTaskEvidenceWithoutDownstreamWork",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_Conflict_DeniesBeforeAggregateAndAdvisoryStores",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_AdmissionStoreUnavailableFailsClosedBeforeRoute",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcome_ReconcilesReadOnlyAndFinalizesExactAggregateResult",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcomeWithoutAuthoritativeResult_RemainsFailClosed",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_UnsafeLegacyInventoryDoesNoLifecycleAdmissionDirectoryOrMigrationWork",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_StateStoreUnavailableFailsClosedWithoutReservation",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_UnknownSchema_FailsClosedAsCorrupt",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_VerificationTagMismatchFailsClosedAsCollision",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_RestartFromEveryDurablePhaseFinishesPinnedTarget",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_UnsafeSourceEvidenceNeverPreparesTarget",
                "Hexalith.EventStore.Server.Tests.Actors.PublicationRecoveryActivationTests.OnActivate_MissingCheckpointPruned_ReleasesTheRecoverableIdempotencyRecord",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_RouteFailure_MarksUnknownOutcomeUnderSameFence",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_WriteAheadFailureAfterFenceMarksUnknownOutcome",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_PendingAcceptsOnlyExactExecuteAuthorityWithoutMutation",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_UnknownOutcomeAcceptsOnlyExactReconciliationAuthorityWithoutMutation",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_TamperedCapabilityLeavesAdmissionUnchangedBeforeBegin",
                "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_ContextThatLostDurableAuthorityPerformsZeroDownstreamWork",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionDirectoryActorTests.AdvanceAsync_PromotionOrder_KeepsSourceCanonicalUntilDirectoryFlip",
                "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_OrdinaryActivationResponseLossReprovesExactTargetBeforeAdvance",
            },
        });
        fixture.RecordEvidence("capture", new
        {
            before,
            after,
            protectedSentinelMatches = after.ProtectedSentinelMatches,
            committedProjectionContainsIdentifiers,
            closureClaimed = false,
        });
        (await fixture.ValidateEvidencePreviewAsync().ConfigureAwait(true)).Length.ShouldBe(64);
    }
}
