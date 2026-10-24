// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 8; PF-02.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Applies restore authority comparisons in the real qualified decision transaction and inspects persisted outcomes.</summary>
public sealed class PayloadProtectionReviewLiveTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Theory]
    [InlineData("lifecycle:")]
    [InlineData("fence:")]
    [InlineData("key:")]
    [InlineData("index-head:")]
    [InlineData("index:")]
    [InlineData("kek-index-head:")]
    [InlineData("kek-index:")]
    public async Task DecisionBoundaryRaceRejectsAcceptedWithEveryExactAuthorityConditionAppliedAtomically(string changed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40)); CancellationToken token = timeout.Token;
        ArgumentNullException.ThrowIfNull(changed);
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-race-" + changed.TrimEnd(':'), token);
        var backup = await BackupAsync(env, token);
        string? auditKey = null;
        var admission = new PayloadRestoreAdmission(fixture.Store, env.Roots, env.Backend, new(), env.Leases, _ => true,
            async (decision, conditions, ct) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.Accepted); conditions.Count.ShouldBe(7);
                var changedValue = conditions.Single(c => c.Key.StartsWith("eventstore:pp:v2:" + changed, StringComparison.Ordinal));
                // Persist a decision-boundary change to this exact supporting observation, after all verification reads.
                (await fixture.Store.CommitAsync([changedValue], ct)).ShouldBe(PayloadStateCommitResult.Committed);
                PayloadProtectionAuditRecord audit = Audit(env, decision, "Succeeded"); auditKey = PayloadProtectionStateCodec.KeyFor(audit);
                var before = new List<PayloadProtectionStateMutation>();
                foreach (var condition in conditions) { before.Add(await CurrentAsync(condition.Key, ct)); }
                // Dapr's selected gRPC transaction surface conservatively reports stale ETags as Unknown.
                (await fixture.Store.CommitAsync([.. conditions, PayloadProtectionStateMutation.Put(audit)], ct)).ShouldBe(PayloadStateCommitResult.Unknown);
                foreach (var condition in before)
                {
                    var after = await CurrentAsync(condition.Key, ct);
                    after.ExpectedETag.ShouldBe(condition.ExpectedETag); after.Bytes.ShouldBe(condition.Bytes);
                }
                throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
            });
        try { await admission.AdmitAsync(backup.Request, backup.Fence, backup.Lifecycle, [backup.Key], token); throw new InvalidOperationException("Unexpected accepted decision"); }
        catch (PayloadProtectionStateException) { }
        auditKey.ShouldNotBeNull(); (await fixture.Store.ReadAsync<PayloadProtectionAuditRecord>(auditKey!, token)).ShouldBeNull();
        (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(env.Scope, backup.Request.AdmissionId), token)).ShouldBeNull();
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V2_AuthorityChangeDuringActualUnwrapPersistsOnlyTheOperatorDecision(bool fenceChange)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40)); CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-unwrap-" + (fenceChange ? "fence" : "lifecycle"), token);
        var backup = await BackupAsync(env, token);
        env.Backend.Unwrapping = async (operation, ct) =>
        {
            (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(env.Scope, operation), ct)).ShouldNotBeNull();
            if (fenceChange)
            {
                var current = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), ct))!;
                (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { ApprovalEpoch = current.Value.ApprovalEpoch + 1 }, current.ETag)], ct)).ShouldBe(PayloadStateCommitResult.Committed);
            }
            else
            {
                var current = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), ct))!;
                (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { State = "Invalidating", Epoch = 2, ReasonCode = "domain-invalidating" }, current.ETag)], ct)).ShouldBe(PayloadStateCommitResult.Committed);
            }
        };
        string? auditKey = null;
        var admission = new PayloadRestoreAdmission(fixture.Store, env.Roots, env.Backend, new(), env.Leases, _ => true,
            async (decision, conditions, ct) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.OperatorDecisionRequired); conditions.ShouldBeEmpty();
                var audit = Audit(env, decision, "Failed"); auditKey = PayloadProtectionStateCodec.KeyFor(audit);
                (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(audit)], ct)).ShouldBe(PayloadStateCommitResult.Committed);
                return decision;
            });
        (await admission.AdmitAsync(backup.Request, backup.Fence, backup.Lifecycle, [backup.Key], token)).State.ShouldBe(RestoredBackupAdmissionState.OperatorDecisionRequired);
        (await fixture.Store.ReadAsync<PayloadProtectionAuditRecord>(auditKey!, token))!.Value.Result.ShouldBe("Failed");
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup.Key), token))!.Value.ShouldBe(backup.Key);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Fact]
    public async Task IgnoredRestoreCancellationBlocksClearAcknowledgementUntilTheActualLateDEKIsZeroed()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40)); CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-late-restore", token);
        var backup = await BackupAsync(env, token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        env.Backend.Unwrapping = async (_, _) => { entered.SetResult(); await release.Task; };
        int decisions = 0;
        var admission = new PayloadRestoreAdmission(fixture.Store, env.Roots, env.Backend, new(), env.Leases, _ => true,
            (_, _, _) => { decisions++; throw new InvalidOperationException(); });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<RestoredBackupAdmissionResult> pending = admission.AdmitAsync(backup.Request, backup.Fence, backup.Lifecycle, [backup.Key], cancel.Token).AsTask();
        await entered.Task.WaitAsync(token); cancel.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        const string operation = "01J00000000000000000000901";
        var approval = new CryptoShreddingWorkflowDecision(new("01J00000000000000000000902", env.Identity.TenantId, env.Identity.Domain,
            CryptoShreddingWorkflowScope.Aggregate, env.Identity.AggregateId, null, null, KeyReferencePolicy.NoKeyReference, null),
            CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None, null, null, "qualification-actor", DateTimeOffset.UtcNow, false, false);
        KeyLifecycleCoordinator lifecycle = env.Lifecycle;
        await lifecycle.BeginAsync(env.Identity, approval, operation, 2, cancellationToken: token);
        Task clearing = lifecycle.AcknowledgeAsync(env.Identity, operation, token).AsTask();
        clearing.IsCompleted.ShouldBeFalse();
        string ack = PayloadProtectionStateKeys.ClearAck(env.Scope, 2, env.Leases.InstanceId);
        (await fixture.Store.ReadAsync<CacheClearAckV1>(ack, token)).ShouldBeNull();
        release.SetResult(); await clearing.WaitAsync(token);
        (await fixture.Store.ReadAsync<CacheClearAckV1>(ack, token))!.Value.OperationId.ShouldBe(operation);
        decisions.ShouldBe(0); env.Backend.Outputs.Count.ShouldBeGreaterThan(1);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Fact]
    public async Task ConfirmedPersistenceAfterTheBarrierRetainsReservedMaterialAndBothIndexesWhileResolvingItsWork()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40)); CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-persisted-barrier", token);
        var write = await env.Reservations.ProtectEventAsync("{\"secret\":\"barrier-persistence-canary\"}"u8.ToArray(), ["/secret"],
            new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 1), token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown, token);
        const string operation = "01J00000000000000000000903";
        var approval = new CryptoShreddingWorkflowDecision(new("01J00000000000000000000904", env.Identity.TenantId, env.Identity.Domain,
            CryptoShreddingWorkflowScope.Aggregate, env.Identity.AggregateId, null, null, KeyReferencePolicy.NoKeyReference, null),
            CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None, null, null, "qualification-actor", DateTimeOffset.UtcNow, false, false);
        await env.Lifecycle.BeginAsync(env.Identity, approval, operation, 2, true, token);
        // Another owner holds the drain while this worker resolves the independent create evidence.
        (await new PayloadReconciliationClaims(fixture.Store).TryClaimAsync(new(operation, env.Scope), token)).ShouldNotBeNull();
        using var worker = new PayloadProtectionReconciler(fixture.Store, env.Roots, env.Lifecycle, env.Backend.BackendId, _ => true, "qualification-actor",
            persistence: new LifecycleQualificationEvidence(write.Completion!.OperationId));
        (await worker.RunPassAsync(token)).ShouldBe(1);
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(env.Scope, write.Completion.KeyReference, 1), token))!.Value;
        key.State.ShouldBe("Reserved");
        var indexes = new PayloadKeyIndexes(fixture.Store);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(env.Scope, token), await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId), token));
        OperationV1 persisted = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion.OperationId), token))!.Value;
        persisted.Phase.ShouldBe("Persisted"); persisted.Result.ShouldBe("Succeeded");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId), token))!.Value.Status.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Pages.SelectMany(p => p.Value.Entries).Select(e => e.OperationId).ShouldBe([operation]);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value.State.ShouldBe("Invalidating");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedRewrapIntentPreventsWrongTargetRecoveryAndRestoresOnlyConfirmedMissingMutation(bool wrongTarget)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40)); CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-retained-target-" + (wrongTarget ? "wrong" : "missing"), token);
        var backup = await BackupAsync(env, token);
        string operation = new CryptographicPayloadProtectionEntropy().CreateKeyReference();
        if (!wrongTarget) { env.Backend.Unwrapping = (_, _) => ValueTask.FromException(new InvalidOperationException("fixture-unwrap-unavailable")); }
        if (wrongTarget) { env.Backend.WrapVersionOverride = env.Backend.OldVersion; }
        // Deliberately fault the exact-version adapter response for the wrong-target case.
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, env.Roots, env.Leases, env.Backend, new(), _ => true, "qualification-actor");
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(env.Identity, backup.Key.KeyRef, 1, operation, env.Backend.NewVersion, token).AsTask());
        env.Backend.Unwrapping = null;
        var held = (await new PayloadReconciliationClaims(fixture.Store).TryClaimAsync(new(operation, env.Scope), token))!;
        held.Value.TargetProviderVersionDigest.ShouldBe(PayloadProtectionStateKeys.Digest(env.Backend.NewVersion));
        if (wrongTarget) { await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.ReconcileAsync(env.Identity, held.Value, token: token).AsTask()); }
        else { (await rotation.ReconcileAsync(env.Identity, held.Value, token: token)).ShouldBeFalse(); }
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup.Key), token))!.Value;
        key.State.ShouldBe(wrongTarget ? "RewrapPending" : "Active"); key.WrappedDek.ShouldBe(backup.Key.WrappedDek); key.ProviderKeyVersionId.ShouldBe(backup.Key.ProviderKeyVersionId);
        var indexes = new PayloadKeyIndexes(fixture.Store);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(env.Scope, token), await rotation.VerifyInventoryAsync(env.Backend.OldVersion, token));
        (await rotation.VerifyInventoryAsync(env.Backend.NewVersion, token)).Head.ShouldBeNull();
        if (!wrongTarget)
        {
            (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation), token))!.Value.ReasonCode.ShouldBe("not-persisted");
            (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(0u);
        }
        env.Backend.WrapCount.ShouldBe(wrongTarget ? 2 : 1);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcknowledgedBarrierCanResolveAnIndexedRewrapWithoutPlaintextAndFinishPersistedDeletion(bool missing)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-barrier-rewrap-" + (missing ? "missing" : "completed"), token);
        var backup = await BackupAsync(env, token);
        string rewrap = new CryptographicPayloadProtectionEntropy().CreateKeyReference();
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, env.Roots, env.Leases, env.Backend, new(), _ => true, "qualification-actor");
        if (missing)
        {
            env.Backend.Unwrapping = (_, _) => ValueTask.FromException(new InvalidOperationException("fixture-unwrap-unavailable"));
            await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(env.Identity, backup.Key.KeyRef, 1, rewrap, env.Backend.NewVersion, token).AsTask());
        }
        else
        {
            env.Backend.CurrentVersion = env.Backend.NewVersion; env.Backend.AmbiguousNextWrap = true;
            (await rotation.RewrapAsync(env.Identity, backup.Key.KeyRef, 1, rewrap, env.Backend.NewVersion, token)).ShouldBeFalse();
        }
        env.Backend.Unwrapping = null;
        string operation = new CryptographicPayloadProtectionEntropy().CreateKeyReference();
        string workflow = new CryptographicPayloadProtectionEntropy().CreateKeyReference();
        var approval = new CryptoShreddingWorkflowDecision(new(workflow, env.Identity.TenantId, env.Identity.Domain,
            CryptoShreddingWorkflowScope.Aggregate, env.Identity.AggregateId, null, null, KeyReferencePolicy.NoKeyReference, null),
            CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None, null, null, "qualification-actor", DateTimeOffset.UtcNow, false, false);
        KeyLifecycleCoordinator lifecycle = env.Lifecycle;
        await lifecycle.BeginAsync(env.Identity, approval, operation, 2, true, token);
        await lifecycle.AcknowledgeAsync(env.Identity, operation, token);
        Should.Throw<PayloadProtectionStateException>(() => env.Leases.LocalOperations.Begin(env.Identity));
        (await fixture.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(env.Scope, 2, env.Leases.InstanceId), token))!.Value.OperationId.ShouldBe(operation);
        int unwraps = 0;
        env.Backend.Unwrapping = (_, _) => { unwraps++; throw new InvalidOperationException("No unwrap is permitted beyond the barrier."); };
        var claims = new PayloadReconciliationClaims(fixture.Store);
        ReconciliationV1 drainClaim = (await claims.TryClaimAsync(new(operation, env.Scope), token))!.Value;
        using var worker = new PayloadProtectionReconciler(fixture.Store, env.Roots, lifecycle, env.Backend.BackendId, _ => true, "qualification-actor", rotation: rotation);
        (await worker.RunPassAsync(token)).ShouldBe(1);
        WrappedDekV1 retained = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup.Key), token))!.Value;
        retained.State.ShouldBe(missing ? "Active" : "RewrapPending"); retained.WrappedDek.ShouldBe(backup.Key.WrappedDek);
        retained.ProviderKeyVersionId.ShouldBe(backup.Key.ProviderKeyVersionId); unwraps.ShouldBe(0);
        var indexes = new PayloadKeyIndexes(fixture.Store);
        PayloadKeyIndexes.VerifyMembership(retained, await indexes.ReadScopeAsync(env.Scope, token), await rotation.VerifyInventoryAsync(env.Backend.OldVersion, token));
        (await rotation.VerifyInventoryAsync(env.Backend.NewVersion, token)).Head.ShouldBeNull();
        OperationV1 resolved = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(rewrap), token))!.Value;
        resolved.Result.ShouldBe(missing ? "Failed" : "Succeeded"); resolved.ReasonCode.ShouldBe(missing ? "not-persisted" : "none");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(rewrap), token))!.Value.Status.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Pages.SelectMany(p => p.Value.Entries).Select(e => e.OperationId).ShouldBe([operation]);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        (await lifecycle.ResumeAsync(env.Identity, drainClaim, token)).ShouldBeTrue();
        System.Diagnostics.Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup.Key), token)).ShouldBeNull();
        (await indexes.ReadScopeAsync(env.Scope, token)).Head!.Value.EntryCount.ShouldBe(0u);
        (await rotation.VerifyInventoryAsync(env.Backend.OldVersion, token)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(0u);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value.State.ShouldBe("Deleted");
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation), token))!.Value.Result.ShouldBe("Succeeded");
        unwraps.ShouldBe(0); env.Backend.WrapCount.ShouldBe(missing ? 1 : 2);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    private ValueTask<PayloadProtectionStateMutation> CurrentAsync(string key, CancellationToken token)
    {
        string kind = key["eventstore:pp:v2:".Length..].Split(':')[0];
        return kind switch
        {
            "lifecycle" => ReadAsync<LifecycleV1>(key, token), "fence" => ReadAsync<FenceV1>(key, token), "key" => ReadAsync<WrappedDekV1>(key, token),
            "index-head" => ReadAsync<ScopeIndexHeadV1>(key, token), "index" => ReadAsync<ScopeIndexPageV1>(key, token),
            "kek-index-head" => ReadAsync<KekInventoryHeadV1>(key, token), "kek-index" => ReadAsync<KekInventoryPageV1>(key, token),
            _ => throw new InvalidOperationException(),
        };
    }
    private async ValueTask<PayloadProtectionStateMutation> ReadAsync<T>(string key, CancellationToken token) where T : class
        => PayloadProtectionStateMutation.Compare((await fixture.Store.ReadAsync<T>(key, token))!);

    private static async ValueTask<(WrappedDekV1 Key, FenceV1 Fence, LifecycleV1 Lifecycle, RestoredBackupAdmissionRequest Request)> BackupAsync(LifecycleQualificationEnvironment env, CancellationToken token)
    {
        var write = await env.Reservations.ProtectEventAsync("{\"secret\":\"restore-qualification-canary\"}"u8.ToArray(), ["/secret"],
            new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 1), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        WrappedDekV1 key = (await env.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(env.Scope, write.Completion!.KeyReference, 1), token))!.Value;
        FenceV1 fence = (await env.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        LifecycleV1 lifecycle = (await env.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value;
        var entropy = new CryptographicPayloadProtectionEntropy();
        var request = new RestoredBackupAdmissionRequest(entropy.CreateKeyReference(), env.Identity.TenantId, env.Identity.Domain, env.Identity.AggregateId, null, null,
            entropy.CreateKeyReference(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, KeyReferencePolicy.NoKeyReference, null, null, null, "qualification-actor");
        return (key, fence, lifecycle, request);
    }
    private static PayloadProtectionAuditRecord Audit(LifecycleQualificationEnvironment env, RestoredBackupAdmissionResult decision, string result) => new(
        new CryptographicPayloadProtectionEntropy().CreateKeyReference(), decision.AdmissionId, decision.AdmissionId, null, env.Identity.TenantId, env.Identity.Domain, null,
        "Reconcile", null, null, 1, 1, env.Backend.BackendId, "v2", null, null, 1, 0, result, result == "Succeeded" ? "none" : "consistency-mismatch", "qualification-actor", PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow));
}
