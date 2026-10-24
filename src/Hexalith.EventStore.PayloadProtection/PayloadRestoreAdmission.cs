// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.4, 13.5 and 15.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Compares a trusted backup's existing record schemas against live irreversible authority and records the existing restore-workflow result.</summary>
internal sealed class PayloadRestoreAdmission(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
    PayloadProtectionBackend backend, PayloadBackendExecutor executor, Func<AggregateIdentity, bool> allowAuditScope,
    Func<RestoredBackupAdmissionResult, CancellationToken, ValueTask<RestoredBackupAdmissionResult>> recordDecision,
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
            var indexes = new PayloadKeyIndexes(store, _clock);
            ScopeKeyIndexSnapshot index = await indexes.ReadScopeAsync(scope, token).ConfigureAwait(false);
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
                PayloadKeyIndexes.VerifyMembership(live.Value, index, await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(backend.BackendId), PayloadProtectionStateKeys.Digest(live.Value.ProviderKeyVersionId), token).ConfigureAwait(false));
                if (state != RestoredBackupAdmissionState.Accepted) { continue; }
                byte[] wrapped = Base64UrlCodec.Decode(live.Value.WrappedDek);
                try
                {
                    using PayloadBackendResult response = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(request.AdmissionId, live.Value.ProviderKeyVersionId, wrapped, ct), token, operation: PayloadProtectionOperation.Unwrap).ConfigureAwait(false);
                    if (response.Failure != PayloadBackendFailure.None || response.ProviderKeyVersionId != live.Value.ProviderKeyVersionId) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; continue; }
                    byte[] dek = response.TakeBytes();
                    try { if (dek.Length != 32) { state = RestoredBackupAdmissionState.OperatorDecisionRequired; } }
                    finally { CryptographicOperations.ZeroMemory(dek); }
                }
                finally { CryptographicOperations.ZeroMemory(wrapped); }
            }
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
        RestoredBackupAdmissionResult recorded = await recordDecision(result, token).ConfigureAwait(false);
        if (recorded != result) { throw Mismatch(); }
        token.ThrowIfCancellationRequested();
        return recorded;
    }
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
