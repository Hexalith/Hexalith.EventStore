// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 12 and 15; amendment sections 3-4.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Strongly admits each read before cache lookup and retains its distributed lease through authenticated plaintext return.</summary>
internal sealed class PayloadKeyResolver(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
    ErasureStateRegistry erasure, PayloadProtectionLeaseCoordinator leases, PayloadProtectionBackend backend,
    PayloadBackendExecutor executor, PayloadKeyCache cache, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Resolves only the exact authenticated scope, DEK version, epoch and provider version.</summary>
    internal async ValueTask<PayloadKeyResolution> ResolveAsync(AggregateIdentity identity, string keyRef, uint version,
        string operationId, CancellationToken cancellationToken = default)
    {
        PayloadProtectionLease? lease = null;
        PayloadKeyResolution? copy = null;
        try
        {
            _ = roots.Require(identity);
            PayloadProtectionStateValue<LifecycleV1> lifecycle = await ObserveAsync(identity, keyRef, version, cancellationToken).ConfigureAwait(false);
            lease = await leases.AcquireReadAsync(identity, keyRef, version, operationId, lifecycle, cancellationToken).ConfigureAwait(false);
            lease.AssertCurrent(cancellationToken);
            string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
            PayloadProtectionStateValue<WrappedDekV1>? key = await store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, keyRef, version), cancellationToken).ConfigureAwait(false);
            if (key is null) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.MissingKey); }
            if (key.Value.TenantId != identity.TenantId || key.Value.Domain != identity.Domain || key.Value.AggregateId != identity.AggregateId
                || key.Value.ScopeDigest != scope || key.Value.KeyRef != keyRef || key.Value.DekVersion != version
                || key.Value.LifecycleEpoch > lifecycle.Value.Epoch || key.Value.BackendId != backend.BackendId
                || key.Value.State is not ("Reserved" or "Active" or "RewrapPending")) { throw Mismatch(); }
            var indexes = new PayloadKeyIndexes(store, _clock);
            PayloadKeyIndexes.VerifyMembership(key.Value, await indexes.ReadScopeAsync(scope, cancellationToken).ConfigureAwait(false),
                await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(backend.BackendId), PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false));
            lease.AssertCurrent(cancellationToken);
            copy = await cache.GetAsync(new(scope, keyRef, version, lifecycle.Value.Epoch, key.Value.ProviderKeyVersionId),
                token => UnwrapAsync(operationId, key.Value, token), cancellationToken).ConfigureAwait(false);
            if (copy.Reason is UnreadableProtectedDataReason failure) { return PayloadKeyResolution.Failure(failure); }
            lease.AssertCurrent(cancellationToken);
            PayloadKeyResolution ownedCopy = copy;
            PayloadProtectionLease ownedLease = lease;
            byte[] bytes = copy.TakeBytes();
            PayloadKeyResolution result = PayloadKeyResolution.Success(bytes, assertCurrent: ownedLease.AssertCurrent,
                authenticated: async token =>
                {
                    PayloadProtectionStateValue<LifecycleV1> current = await ObserveAsync(identity, keyRef, version, token).ConfigureAwait(false);
                    if (current.Value.Epoch != ownedLease.Epoch) { throw Mismatch(); }
                    if (key.Value.State == "Reserved") { await EnlistReservedReadAsync(identity, key.Value, current, token).ConfigureAwait(false); }
                }, releasedAsync: async () =>
                {
                    try { await ownedCopy.DisposeAsync().ConfigureAwait(false); }
                    finally
                    {
                        await ownedLease.DisposeAsync().ConfigureAwait(false);
                        ownedLease.AssertDeadline(cancellationToken);
                    }
                });
            copy = null;
            lease = null;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PayloadProtectionStateException exception) { return PayloadKeyResolution.Failure(exception.Reason); }
        catch { cancellationToken.ThrowIfCancellationRequested(); return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.ProviderUnavailable); }
        finally
        {
            try { if (copy is not null) { await copy.DisposeAsync().ConfigureAwait(false); } }
            finally { if (lease is not null) { await lease.DisposeAsync().ConfigureAwait(false); } }
        }
    }

    private async ValueTask<PayloadKeyResolution> UnwrapAsync(string operation, WrappedDekV1 key, CancellationToken cancellationToken)
    {
        byte[] wrapped = Base64UrlCodec.Decode(key.WrappedDek);
        try
        {
            using PayloadBackendResult response = await executor.ExecuteAsync(backend,
                token => backend.UnwrapAsync(operation, key.ProviderKeyVersionId, wrapped, token), cancellationToken).ConfigureAwait(false);
            if (response.Failure != PayloadBackendFailure.None) { return PayloadKeyResolution.Failure(response.Failure switch
            {
                PayloadBackendFailure.Denied => UnreadableProtectedDataReason.ProviderDenied,
                PayloadBackendFailure.Missing or PayloadBackendFailure.Invalidated or PayloadBackendFailure.ConsistencyMismatch => UnreadableProtectedDataReason.ConsistencyMismatch,
                _ => UnreadableProtectedDataReason.ProviderUnavailable,
            }); }
            if (response.ProviderKeyVersionId != key.ProviderKeyVersionId) { return PayloadKeyResolution.Failure(UnreadableProtectedDataReason.ConsistencyMismatch); }
            return PayloadKeyResolution.Success(response.TakeBytes());
        }
        finally { CryptographicOperations.ZeroMemory(wrapped); }
    }

    private async ValueTask<PayloadProtectionStateValue<LifecycleV1>> ObserveAsync(AggregateIdentity identity, string keyRef, uint version, CancellationToken cancellationToken)
    {
        PayloadErasureStateResult observed = await erasure.ObserveAsync(identity, keyRef, version, cancellationToken).ConfigureAwait(false);
        if (observed.State is not (PayloadErasureState.Active or PayloadErasureState.Pending))
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        }
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(PayloadProtectionStateKeys.ScopeDigest(identity)), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (lifecycle.Value.TenantId != identity.TenantId || lifecycle.Value.Domain != identity.Domain || lifecycle.Value.AggregateId != identity.AggregateId) { throw Mismatch(); }
        if (lifecycle.Value.State is "Invalidating" or "Invalidated" or "Deleted") { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
        if (lifecycle.Value.Epoch != observed.LifecycleEpoch || lifecycle.Value.State != observed.State.ToString()) { throw Mismatch(); }
        return lifecycle;
    }

    private async ValueTask EnlistReservedReadAsync(AggregateIdentity identity, WrappedDekV1 key,
        PayloadProtectionStateValue<LifecycleV1> lifecycle, CancellationToken cancellationToken)
    {
        PayloadProtectionStateValue<ReconciliationV1> reconciliation = await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(key.CreatingOperationId), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (reconciliation.Value.ScopeDigest != key.ScopeDigest || reconciliation.Value.KeyRef != key.KeyRef || reconciliation.Value.DekVersion != key.DekVersion) { throw Mismatch(); }
        if (reconciliation.Value.Status is "Pending" or "Running") { return; }
        if (reconciliation.Value.Status != "Quarantined") { throw Mismatch(); }
        var catalogs = new PayloadProtectionCatalogs(store, _clock);
        WorkCatalogSnapshot work = await catalogs.ReadWorkAsync(roots.Require(identity), cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<WrappedDekV1> current = await store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key), cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
        if (current.Value != key) { throw Mismatch(); }
        string now = PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
        PayloadStateCommitResult result = await store.CommitAsync([PayloadProtectionStateMutation.Compare(lifecycle), PayloadProtectionStateMutation.Compare(current),
            PayloadProtectionStateMutation.Put(reconciliation.Value with { Status = "Pending", ReasonCode = "none", NotBeforeUtc = now, UpdatedAtUtc = now }, reconciliation.ETag),
            .. catalogs.AddWork(work, new(key.CreatingOperationId, key.ScopeDigest))], cancellationToken).ConfigureAwait(false);
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied
            ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
