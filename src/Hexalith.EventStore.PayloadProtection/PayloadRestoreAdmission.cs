// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.4, 13.5 and 15; approved review correction section 8.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Compares a trusted backup's existing record schemas against live irreversible authority and records the existing restore-workflow result.</summary>
/// <remarks>The trusted workflow callback must persist its decision atomically with all supplied unchanged-value conditions in the qualified store.
/// It must fail on conflict or uncertainty; returning an unpersisted Accepted decision violates this internal integration contract. Host wiring belongs to Story 8.7.</remarks>
internal sealed class PayloadRestoreAdmission(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
    PayloadProtectionBackend backend, PayloadBackendExecutor executor, PayloadProtectionLeaseCoordinator leases, Func<AggregateIdentity, bool> allowAuditScope,
    Func<RestoredBackupAdmissionResult, IReadOnlyList<PayloadProtectionStateMutation>, CancellationToken, ValueTask<RestoredBackupAdmissionResult>> recordDecision,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Never imports or activates keys; a denied, incomplete or resurrecting restore remains quarantined before traffic admission.</summary>
    internal async ValueTask<RestoredBackupAdmissionResult> AdmitAsync(RestoredBackupAdmissionRequest request, FenceV1 backupFence,
        LifecycleV1 backupLifecycle, IReadOnlyList<WrappedDekV1> backupKeys, CancellationToken token = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, token);
        token.ThrowIfCancellationRequested();
        if (!request.TryValidate(out _) || request.AggregateId is null || !CanonicalUlid.IsValid(request.AdmissionId)
            || !Enum.IsDefined(request.KeyReferencePolicy) || request.ProtectionMetadataVersion != 1) { throw Mismatch(); }
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        string root = roots.Require(identity);
        PayloadProtectionAuditAdmission.Require(allowAuditScope, identity);
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using PayloadLocalOperation local = leases.LocalOperations.Begin(identity, token);
        token = local.Token;
        var conditions = new Dictionary<string, PayloadProtectionStateMutation>(StringComparer.Ordinal);
        PayloadProtectionLease? lease = null;
        bool returned = false;
        try
        {
            RestoredBackupAdmissionState state = RestoredBackupAdmissionState.Accepted;
            try
            {
                _ = PayloadProtectionStateCodec.Encode(backupFence);
                _ = PayloadProtectionStateCodec.Encode(backupLifecycle);
                PayloadProtectionStateValue<FenceV1> liveFence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), token).ConfigureAwait(false);
                PayloadProtectionStateValue<LifecycleV1> liveLifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token).ConfigureAwait(false);
                if (backupFence.TenantDomainDigest != root || backupFence.TenantId != identity.TenantId || backupFence.Domain != identity.Domain
                    || liveFence.Value.TenantId != identity.TenantId || liveFence.Value.Domain != identity.Domain || liveFence.Value.TenantDomainDigest != root
                    || backupLifecycle.ScopeDigest != scope || backupLifecycle.TenantId != identity.TenantId || backupLifecycle.Domain != identity.Domain || backupLifecycle.AggregateId != identity.AggregateId
                    || liveLifecycle.Value.TenantId != identity.TenantId || liveLifecycle.Value.Domain != identity.Domain || liveLifecycle.Value.AggregateId != identity.AggregateId
                    || backupFence.ApprovalEpoch != liveFence.Value.ApprovalEpoch || backupFence.HighestWrittenEpoch != liveFence.Value.HighestWrittenEpoch
                    || backupFence.HighestWrittenFormat != liveFence.Value.HighestWrittenFormat || backupFence.ApprovedSpecDigest != liveFence.Value.ApprovedSpecDigest
                    || backupLifecycle.Epoch != liveLifecycle.Value.Epoch || backupLifecycle.State != liveLifecycle.Value.State
                    || (liveLifecycle.Value.State is "Invalidating" or "Invalidated" or "Deleted" && backupKeys.Count != 0)) { state = RestoredBackupAdmissionState.Quarantined; }
                // Read admission changes lifecycle ETags itself; retain the refreshed observation for the workflow CAS.
                lease = state == RestoredBackupAdmissionState.Accepted
                    ? await leases.AcquireReadAsync(identity, "", 0, request.AdmissionId, liveLifecycle, token).ConfigureAwait(false) : null;
                if (lease is not null)
                {
                    PayloadProtectionStateValue<LifecycleV1> admitted = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token).ConfigureAwait(false);
                    if (admitted.Value != liveLifecycle.Value) { throw Mismatch(); }
                    liveLifecycle = admitted;
                }
                Add(PayloadProtectionStateMutation.Compare(liveFence));
                Add(PayloadProtectionStateMutation.Compare(liveLifecycle));
                var indexes = new PayloadKeyIndexes(store, _clock);
                ScopeKeyIndexSnapshot index = await indexes.ReadScopeAsync(scope, token).ConfigureAwait(false);
                if (index.Head is not null) { Add(PayloadProtectionStateMutation.Compare(index.Head)); }
                else { Add(PayloadProtectionStateMutation.Put(new ScopeIndexHeadV1(scope, 0, 0, 0, PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow())))); }
                foreach (var page in index.Pages) { Add(PayloadProtectionStateMutation.Compare(page)); }
                var pairs = new HashSet<(string Key, uint Version)>();
                if (backupKeys.Count != index.Pages.Sum(p => p.Value.Entries.Length)) { state = RestoredBackupAdmissionState.Quarantined; }
                foreach (WrappedDekV1 backup in backupKeys)
                {
                    token.ThrowIfCancellationRequested();
                    _ = PayloadProtectionStateCodec.Encode(backup);
                    if (backup.ScopeDigest != scope || backup.TenantId != identity.TenantId || backup.Domain != identity.Domain || backup.AggregateId != identity.AggregateId
                        || !pairs.Add((backup.KeyRef, backup.DekVersion))) { state = RestoredBackupAdmissionState.Quarantined; continue; }
                    PayloadProtectionStateValue<WrappedDekV1>? live = await PayloadOperationBudget.ReadAsync<WrappedDekV1>(store, PayloadProtectionStateCodec.KeyFor(backup), token).ConfigureAwait(false);
                    if (live is null || live.Value.State is "Invalidating" or "Invalidated" or "Deleted" || live.Value.BackendId != backend.BackendId
                        || backup.BackendId != live.Value.BackendId || backup.ProviderKeyVersionId != live.Value.ProviderKeyVersionId
                        || backup.State != live.Value.State || backup.LifecycleEpoch != live.Value.LifecycleEpoch
                        || backup.FenceApprovalEpoch != live.Value.FenceApprovalEpoch || backup.HighestWrittenEpoch != live.Value.HighestWrittenEpoch
                        || backup.WrappedDekSha256 != live.Value.WrappedDekSha256) { state = RestoredBackupAdmissionState.Quarantined; continue; }
                    KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(backend.BackendId), PayloadProtectionStateKeys.Digest(live.Value.ProviderKeyVersionId), token).ConfigureAwait(false);
                    PayloadKeyIndexes.VerifyMembership(live.Value, index, inventory);
                    Add(PayloadProtectionStateMutation.Compare(live));
                    Add(PayloadProtectionStateMutation.Compare(inventory.Head!));
                    foreach (var page in inventory.Pages) { Add(PayloadProtectionStateMutation.Compare(page)); }
                    if (state != RestoredBackupAdmissionState.Accepted) { continue; }
                    byte[] wrapped = Base64UrlCodec.Decode(live.Value.WrappedDek);
                    try
                    {
                        using PayloadBackendResult response = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(request.AdmissionId, live.Value.ProviderKeyVersionId, wrapped, ct), token, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Unwrap).ConfigureAwait(false);
                        if (response.Failure != PayloadBackendFailure.None || response.ProviderKeyVersionId != live.Value.ProviderKeyVersionId) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; continue; }
                        byte[] dek = response.TakeBytes();
                        try { if (dek.Length != 32) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; } }
                        finally { CryptographicOperations.ZeroMemory(dek); }
                    }
                    finally { CryptographicOperations.ZeroMemory(wrapped); }
                }
                local.AssertCurrent(token); lease?.AssertCurrent(token); PayloadOperationBudget.RequireCurrent();
                // The existing workflow receives no accepted result from stale observations.
                PayloadProtectionStateValue<FenceV1> confirmedFence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), token).ConfigureAwait(false);
                PayloadProtectionStateValue<LifecycleV1> confirmedLifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token).ConfigureAwait(false);
                if (confirmedFence.ETag != liveFence.ETag || confirmedLifecycle.ETag != liveLifecycle.ETag) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (PayloadProtectionFormatException) { state = RestoredBackupAdmissionState.Quarantined; }
            catch (PayloadProtectionStateException) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; }
            string reason = RestoredBackupAdmissionResult.ReasonCodeFor(state);
            var result = new RestoredBackupAdmissionResult(request.AdmissionId, state, request.TenantId, request.Domain, request.AggregateId,
                request.FromSequence, request.ToSequence, request.BackupManifestId, request.ProtectionMetadataVersion, request.KeyReferencePolicy,
                request.KeyAliasFingerprint, state == RestoredBackupAdmissionState.Accepted ? null : "consistency-mismatch", reason,
                RestoredBackupAdmissionResult.NextActionFor(state), request.CorrelationId, null, request.OperatorActorId, _clock.GetUtcNow(), false);
            RestoredBackupAdmissionResult recorded = await PayloadOperationBudget.AwaitAsync(ct => recordDecision(result, state == RestoredBackupAdmissionState.Accepted ? conditions.Values.ToArray() : [], ct), token).ConfigureAwait(false);
            if (recorded != result) { throw Mismatch(); }
            token.ThrowIfCancellationRequested();
            local.AssertCurrent(token); PayloadOperationBudget.RequireCurrent();
            returned = true;
            return recorded;

            void Add(PayloadProtectionStateMutation condition)
            {
                if (conditions.TryGetValue(condition.Key, out var previous) && (previous.ExpectedETag != condition.ExpectedETag || !previous.Bytes!.AsSpan().SequenceEqual(condition.Bytes))) { throw Mismatch(); }
                conditions[condition.Key] = condition;
            }
        }
        finally
        {
            if (lease is not null)
            {
                using var cleanup = new PayloadOperationBudget(_clock, default, independent: true);
                try { await lease.DisposeAsync().ConfigureAwait(false); }
                catch when (!returned) { /* Preserve the original cancellation/failure; expiry never supplies clearing proof. */ }
            }
        }
    }
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
