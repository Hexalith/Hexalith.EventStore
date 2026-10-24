// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 8.3, 10.4, 14-15; amendment sections 3-4.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Rewraps the same DEK through durable indexed intent and increasing claims while leaving immutable payloads unchanged.</summary>
internal sealed class PayloadKeyRotationCoordinator(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
    PayloadProtectionLeaseCoordinator leases, PayloadProtectionBackend backend, PayloadBackendExecutor executor,
    Func<AggregateIdentity, bool> allowAuditScope, string decisionActor, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly IPayloadProtectionEntropy _entropy = new CryptographicPayloadProtectionEntropy();

    /// <summary>Verifies every inventory entry and every reverse scope entry before any rewrap or old-version retirement claim.</summary>
    internal async ValueTask<KekKeyIndexSnapshot> VerifyInventoryAsync(string providerVersion, CancellationToken token = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, token);
        var indexes = new PayloadKeyIndexes(store, _clock);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(backend.BackendId), PayloadProtectionStateKeys.Digest(providerVersion), token).ConfigureAwait(false);
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (KekInventoryEntryV1 entry in inventory.Pages.SelectMany(p => p.Value.Entries))
        {
            PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(entry.ScopeDigest, entry.KeyRef, entry.DekVersion), token).ConfigureAwait(false);
            var identity = new AggregateIdentity(key.Value.TenantId, key.Value.Domain, key.Value.AggregateId);
            _ = Admit(identity);
            ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(entry.ScopeDigest, token).ConfigureAwait(false);
            PayloadKeyIndexes.VerifyMembership(key.Value, scope, inventory);
            if (!scopes.Add(entry.ScopeDigest)) { continue; }
            foreach (ScopeIndexEntryV1 reverse in scope.Pages.SelectMany(p => p.Value.Entries).Where(e => e.ProviderVersionDigest == inventory.ProviderDigest))
            {
                if (!inventory.Pages.SelectMany(p => p.Value.Entries).Contains(new KekInventoryEntryV1(entry.ScopeDigest, reverse.KeyRef, reverse.DekVersion))) { throw Mismatch(); }
            }
        }
        if (inventory.Head is not null)
        {
            PayloadProtectionStateValue<KekInventoryHeadV1> confirmed = await RequiredAsync<KekInventoryHeadV1>(PayloadProtectionStateCodec.KeyFor(inventory.Head.Value), token).ConfigureAwait(false);
            if (confirmed.ETag != inventory.Head.ETag) { throw Mismatch(); }
        }
        return inventory;
    }

    /// <summary>Starts an exact per-key operation once; a pre-existing operation is reconciled by identity rather than replaying wrap.</summary>
    internal async ValueTask<bool> RewrapAsync(AggregateIdentity identity, string keyRef, uint version, string operationId,
        string selectedProviderVersion, CancellationToken token = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, token, TimeSpan.FromSeconds(30));
        using var diagnostic = new PayloadProtectionDiagnosticScope(PayloadProtectionOperation.Rewrap);
        string root = Admit(identity);
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        using PayloadLocalOperation local = leases.LocalOperations.Begin(identity, token);
        token = local.Token;
        long started = _clock.GetTimestamp();
        uint attempts = 0;
        var catalogs = new PayloadProtectionCatalogs(store, _clock);
        var indexes = new PayloadKeyIndexes(store, _clock);
        var claims = new PayloadReconciliationClaims(store, _clock);
        PayloadProtectionStateValue<OperationV1>? existing = await PayloadOperationBudget.ReadAsync<OperationV1>(store, PayloadProtectionStateKeys.Operation(operationId), token).ConfigureAwait(false);
        if (existing is not null)
        {
            ValidateOperation(existing.Value, scope, keyRef, version);
            if (existing.Value.Phase == "Completed")
            {
                WrappedDekV1 completed = (await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, keyRef, version), token).ConfigureAwait(false)).Value;
                if (completed.ProviderKeyVersionId != selectedProviderVersion || existing.Value.Result != "Succeeded") { throw Mismatch(); }
                PayloadKeyIndexes.VerifyMembership(completed, await indexes.ReadScopeAsync(scope, token).ConfigureAwait(false), await VerifyInventoryAsync(selectedProviderVersion, token).ConfigureAwait(false));
                diagnostic.Complete();
                return true;
            }
            PayloadProtectionStateValue<ReconciliationV1>? resume = await claims.TryClaimAsync(new(operationId, scope), token).ConfigureAwait(false);
            bool reconciled = resume is not null && await ReconcileAsync(identity, resume.Value, selectedProviderVersion, token).ConfigureAwait(false);
            if (reconciled) { diagnostic.Complete(); }
            return reconciled;
        }
        PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, keyRef, version), token).ConfigureAwait(false);
        ValidateKey(identity, key.Value);
        if (key.Value.State is not ("Reserved" or "Active") || key.Value.BackendId != backend.BackendId || key.Value.ProviderKeyVersionId == selectedProviderVersion) { throw Mismatch(); }
        KekKeyIndexSnapshot inventory = await VerifyInventoryAsync(key.Value.ProviderKeyVersionId, token).ConfigureAwait(false);
        ScopeKeyIndexSnapshot scopeIndex = await indexes.ReadScopeAsync(scope, token).ConfigureAwait(false);
        PayloadKeyIndexes.VerifyMembership(key.Value, scopeIndex, inventory);
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await LifecycleAsync(identity, token).ConfigureAwait(false);
        IReadOnlyList<PayloadProtectionStateMutation> authority = await leases.AdmissionAsync(identity, lifecycle.Value, false, token).ConfigureAwait(false);
        PayloadProtectionLease lease = await leases.AcquireReadAsync(identity, keyRef, version, operationId, lifecycle, token).ConfigureAwait(false);
        await using var leaseLifetime = lease.ConfigureAwait(false);
        lifecycle = await LifecycleAsync(identity, token).ConfigureAwait(false);
        // Lease admission CAS changed lifecycle/member ETags; all intent comparisons are refreshed.
        authority = await leases.AdmissionAsync(identity, lifecycle.Value, false, token).ConfigureAwait(false);
        WorkCatalogSnapshot work = await catalogs.ReadWorkAsync(root, token).ConfigureAwait(false);
        string now = Now();
        var operation = new OperationV1(operationId, "Rewrap", scope, keyRef, version, "Started", 1, now, now, "Pending", "none");
        var reconciliation = new ReconciliationV1(operationId, scope, keyRef, version, "Rewrap", now, 0, "Pending", "none", now);
        RequireCommit(await store.CommitAsync([.. authority, PayloadProtectionStateMutation.Compare(lifecycle),
            PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(key.Value with { State = "RewrapPending", UpdatedAtUtc = now }), key.ETag),
            .. indexes.SetScopeEntry(scopeIndex, new(keyRef, version, inventory.ProviderDigest, "RewrapPending"), false),
            PayloadProtectionStateMutation.Compare(inventory.Head!), PayloadProtectionStateMutation.Put(operation), PayloadProtectionStateMutation.Put(reconciliation),
            .. catalogs.AddWork(work, new(operationId, scope))], token).ConfigureAwait(false));
        ReconciliationV1 claim = (await claims.TryClaimAsync(new(operationId, scope), token).ConfigureAwait(false))?.Value ?? throw Mismatch();
        byte[] wrapped = Base64UrlCodec.Decode(key.Value.WrappedDek);
        byte[]? dek = null;
        try
        {
            using PayloadBackendResult unwrapped = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(_entropy.CreateKeyReference(), key.Value.ProviderKeyVersionId, wrapped, ct), token, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Unwrap, attempted: _ => attempts++).ConfigureAwait(false);
            RequireBackend(unwrapped);
            if (unwrapped.ProviderKeyVersionId != key.Value.ProviderKeyVersionId) { throw Mismatch(); }
            dek = unwrapped.TakeBytes();
            if (dek.Length != 32) { throw Mismatch(); }
            using PayloadBackendResult rewrapped = await executor.ExecuteAsync(backend, ct => backend.WrapAsync(operationId, dek, ct), token, retainAbandoned: local.RetainUntil, attempted: _ => attempts++).ConfigureAwait(false);
            if (rewrapped.Failure == PayloadBackendFailure.Ambiguous) { await RecordFailureAsync(identity, claim, attempts, started, "ambiguous-outcome").ConfigureAwait(false); return false; }
            RequireBackend(rewrapped);
            if (rewrapped.ProviderKeyVersionId != selectedProviderVersion) { throw Mismatch(); }
            byte[] newWrapped = rewrapped.TakeBytes();
            try
            {
                using PayloadBackendResult verified = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(_entropy.CreateKeyReference(), selectedProviderVersion, newWrapped, ct), token, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Unwrap, attempted: _ => attempts++).ConfigureAwait(false);
                RequireBackend(verified);
                byte[] verifiedDek = verified.TakeBytes();
                try
                {
                    if (verified.ProviderKeyVersionId != selectedProviderVersion || !CryptographicOperations.FixedTimeEquals(dek, verifiedDek)) { throw Mismatch(); }
                }
                finally { CryptographicOperations.ZeroMemory(verifiedDek); }
                lease.AssertCurrent(token); local.AssertCurrent(token);
                if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(30)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
                bool committed = await CommitAsync(identity, claim, selectedProviderVersion, newWrapped, started, token, attempts).ConfigureAwait(false);
                if (committed) { diagnostic.Complete(); }
                return committed;
            }
            finally { CryptographicOperations.ZeroMemory(newWrapped); }
        }
        catch (Exception failure)
        {
            await RecordFailureAsync(identity, claim, attempts, started, Reason(failure)).ConfigureAwait(false);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapped);
            if (dek is not null) { CryptographicOperations.ZeroMemory(dek); }
        }
    }

    /// <summary>Asks only for the stable wrap operation's outcome; never repeats the original mutation after a lost response.</summary>
    internal async ValueTask<bool> ReconcileAsync(AggregateIdentity identity, ReconciliationV1 claim, string? expectedProvider = null, CancellationToken token = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, token, TimeSpan.FromSeconds(30));
        using var diagnostic = new PayloadProtectionDiagnosticScope(PayloadProtectionOperation.Reconcile);
        _ = Admit(identity);
        if (claim.Kind != "Rewrap" || claim.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(identity)) { throw Mismatch(); }
        using PayloadLocalOperation local = leases.LocalOperations.Begin(identity, token);
        token = local.Token;
        _ = await new PayloadReconciliationClaims(store, _clock).RequireAsync(claim, token).ConfigureAwait(false);
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await LifecycleAsync(identity, token).ConfigureAwait(false);
        PayloadProtectionLease lease = await leases.AcquireReadAsync(identity, claim.KeyRef, claim.DekVersion, _entropy.CreateKeyReference(), lifecycle, token).ConfigureAwait(false);
        await using var leaseLifetime = lease.ConfigureAwait(false);
        long started = _clock.GetTimestamp();
        uint attempts = 0;
        using PayloadBackendResult response = await executor.ExecuteAsync(backend, ct => backend.ReconcileAsync(claim.OperationId, ct), token, allowRetry: false, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Reconcile, attempted: _ => attempts++).ConfigureAwait(false);
        if (response.Failure is PayloadBackendFailure.Ambiguous or PayloadBackendFailure.Unavailable) { await RecordFailureAsync(identity, claim, attempts, started, "ambiguous-outcome").ConfigureAwait(false); return false; }
        RequireBackend(response);
        if (response.ProviderKeyVersionId is null || (expectedProvider is not null && response.ProviderKeyVersionId != expectedProvider)) { throw Mismatch(); }
        byte[] wrapped = response.TakeBytes();
        try
        {
            WrappedDekV1 old = (await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), token).ConfigureAwait(false)).Value;
            byte[] oldWrapped = Base64UrlCodec.Decode(old.WrappedDek);
            byte[]? oldDek = null;
            byte[]? newDek = null;
            try
            {
                using PayloadBackendResult original = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(_entropy.CreateKeyReference(), old.ProviderKeyVersionId, oldWrapped, ct), token, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Unwrap, attempted: _ => attempts++).ConfigureAwait(false);
                using PayloadBackendResult replacement = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync(_entropy.CreateKeyReference(), response.ProviderKeyVersionId, wrapped, ct), token, retainAbandoned: local.RetainUntil, operation: PayloadProtectionOperation.Unwrap, attempted: _ => attempts++).ConfigureAwait(false);
                RequireBackend(original); RequireBackend(replacement);
                oldDek = original.TakeBytes(); newDek = replacement.TakeBytes();
                if (original.ProviderKeyVersionId != old.ProviderKeyVersionId || replacement.ProviderKeyVersionId != response.ProviderKeyVersionId
                    || oldDek.Length != 32 || !CryptographicOperations.FixedTimeEquals(oldDek, newDek)) { throw Mismatch(); }
                lease.AssertCurrent(token); local.AssertCurrent(token);
                if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(30)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
                bool committed = await CommitAsync(identity, claim, response.ProviderKeyVersionId, wrapped, started, token, attempts).ConfigureAwait(false);
                if (committed) { diagnostic.Complete(); }
                return committed;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(oldWrapped);
                if (oldDek is not null) { CryptographicOperations.ZeroMemory(oldDek); }
                if (newDek is not null) { CryptographicOperations.ZeroMemory(newDek); }
            }
        }
        catch (Exception failure)
        {
            await RecordFailureAsync(identity, claim, attempts, started, Reason(failure)).ConfigureAwait(false);
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(wrapped); }
    }

    private async ValueTask<bool> CommitAsync(AggregateIdentity identity, ReconciliationV1 claim, string provider, byte[] wrapped, long started, CancellationToken token, uint attempts)
    {
        string root = Admit(identity);
        var indexes = new PayloadKeyIndexes(store, _clock);
        var catalogs = new PayloadProtectionCatalogs(store, _clock);
        PayloadProtectionStateValue<ReconciliationV1> currentClaim = await new PayloadReconciliationClaims(store, _clock).RequireAsync(claim, token).ConfigureAwait(false);
        PayloadProtectionStateValue<OperationV1> operation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(claim.OperationId), token).ConfigureAwait(false);
        ValidateOperation(operation.Value, claim.ScopeDigest, claim.KeyRef, claim.DekVersion);
        PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), token).ConfigureAwait(false);
        ValidateKey(identity, key.Value);
        if (key.Value.State != "RewrapPending" || key.Value.ProviderKeyVersionId == provider) { throw Mismatch(); }
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(claim.ScopeDigest, token).ConfigureAwait(false);
        KekKeyIndexSnapshot oldInventory = await VerifyInventoryAsync(key.Value.ProviderKeyVersionId, token).ConfigureAwait(false);
        KekKeyIndexSnapshot newInventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(backend.BackendId), PayloadProtectionStateKeys.Digest(provider), token).ConfigureAwait(false);
        PayloadKeyIndexes.VerifyMembership(key.Value, scope, oldInventory);
        PayloadProtectionStateValue<OperationV1> creating = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(key.Value.CreatingOperationId), token).ConfigureAwait(false);
        if (creating.Value.Operation != "Create" || creating.Value.ScopeDigest != claim.ScopeDigest || creating.Value.KeyRef != claim.KeyRef || creating.Value.DekVersion != claim.DekVersion) { throw Mismatch(); }
        string restoredState = creating.Value.Phase == "Completed" && creating.Value.Result == "Succeeded" ? "Active" : "Reserved";
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await LifecycleAsync(identity, token).ConfigureAwait(false);
        IReadOnlyList<PayloadProtectionStateMutation> authority = await leases.AdmissionAsync(identity, lifecycle.Value, false, token).ConfigureAwait(false);
        WorkCatalogSnapshot work = await catalogs.ReadWorkAsync(root, token).ConfigureAwait(false);
        string now = Now();
        var audit = new PayloadProtectionAuditRecord(_entropy.CreateKeyReference(), claim.OperationId, null, null, identity.TenantId, identity.Domain, null,
            "Rewrap", "RewrapPending", restoredState, lifecycle.Value.Epoch, key.Value.FenceApprovalEpoch, backend.BackendId, "v2", null, null,
            attempts, checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds))), "Succeeded", "none", decisionActor, now);
        RequireCommit(await store.CommitAsync([.. authority, PayloadProtectionStateMutation.Compare(lifecycle), PayloadProtectionStateMutation.Compare(creating),
            PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(key.Value with { State = restoredState, ProviderKeyVersionId = provider, WrappedDek = Base64UrlCodec.Encode(wrapped), UpdatedAtUtc = now }), key.ETag),
            .. indexes.SetScopeEntry(scope, new(claim.KeyRef, claim.DekVersion, newInventory.ProviderDigest, restoredState), false),
            .. indexes.SetInventoryEntry(oldInventory, new(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), false),
            .. indexes.SetInventoryEntry(newInventory, new(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), true),
            PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Completed", Result = "Succeeded", Attempt = attempts, ReasonCode = "none", UpdatedAtUtc = now }, operation.ETag),
            .. catalogs.CompleteWork(work, currentClaim, claim with { Status = "Completed", ReasonCode = "none", UpdatedAtUtc = now }), PayloadProtectionStateMutation.Put(audit)], token).ConfigureAwait(false));
        return true;
    }
    private async ValueTask RecordFailureAsync(AggregateIdentity identity, ReconciliationV1 claim, uint attempts, long started, string reason)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10), _clock);
        try
        {
            PayloadProtectionStateValue<ReconciliationV1>? current = await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(claim.OperationId), cleanup.Token).ConfigureAwait(false);
            PayloadProtectionStateValue<OperationV1>? operation = await store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(claim.OperationId), cleanup.Token).ConfigureAwait(false);
            if (current?.Value != claim || operation is null || operation.Value.Phase == "Completed") { return; }
            ValidateOperation(operation.Value, claim.ScopeDigest, claim.KeyRef, claim.DekVersion);
            PayloadProtectionStateValue<WrappedDekV1>? key = await store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), cleanup.Token).ConfigureAwait(false);
            if (key is null) { return; }
            ValidateKey(identity, key.Value);
            PayloadProtectionAuditAdmission.Require(allowAuditScope, identity);
            string now = Now();
            var audit = new PayloadProtectionAuditRecord(_entropy.CreateKeyReference(), claim.OperationId, null, null, identity.TenantId, identity.Domain, null,
                "Rewrap", key.Value.State, key.Value.State, key.Value.LifecycleEpoch, key.Value.FenceApprovalEpoch, backend.BackendId, "v2", null, null,
                attempts, checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds))), reason == "ambiguous-outcome" ? "Pending" : "Failed", reason, decisionActor, now);
            _ = await store.CommitAsync([PayloadProtectionStateMutation.Compare(key),
                PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Ambiguous", Result = "Pending", Attempt = attempts, ReasonCode = reason, UpdatedAtUtc = now }, operation.ETag),
                PayloadProtectionStateMutation.Put(claim with { ReasonCode = reason, UpdatedAtUtc = now }, current.ETag), PayloadProtectionStateMutation.Put(audit)], cleanup.Token).ConfigureAwait(false);
        }
        catch { /* Retain indexed authority if failure recording itself cannot be confirmed. */ }
    }
    private static string Reason(Exception failure) => failure is OperationCanceledException ? "cancelled"
        : failure is PayloadProtectionStateException { Reason: UnreadableProtectedDataReason.ProviderDenied } ? "provider-denied"
        : failure is PayloadProtectionStateException { Reason: UnreadableProtectedDataReason.ConsistencyMismatch } ? "consistency-mismatch" : "ambiguous-outcome";
    private async ValueTask<PayloadProtectionStateValue<LifecycleV1>> LifecycleAsync(AggregateIdentity identity, CancellationToken token)
    {
        PayloadProtectionStateValue<LifecycleV1> value = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(PayloadProtectionStateKeys.ScopeDigest(identity)), token).ConfigureAwait(false);
        if (value.Value.TenantId != identity.TenantId || value.Value.Domain != identity.Domain || value.Value.AggregateId != identity.AggregateId || value.Value.State is not ("Active" or "Pending")) { throw Mismatch(); }
        return value;
    }
    private string Admit(AggregateIdentity identity)
    {
        string root = roots.Require(identity);
        PayloadProtectionAuditAdmission.Require(allowAuditScope, identity);
        _ = CanonicalText.GetByteCount(decisionActor, 1, 256);
        return root;
    }
    private static void ValidateKey(AggregateIdentity identity, WrappedDekV1 key)
    {
        if (key.TenantId != identity.TenantId || key.Domain != identity.Domain || key.AggregateId != identity.AggregateId || key.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(identity)) { throw Mismatch(); }
    }
    private static void ValidateOperation(OperationV1 operation, string scope, string key, uint version)
    {
        if (operation.Operation != "Rewrap" || operation.ScopeDigest != scope || operation.KeyRef != key || operation.DekVersion != version) { throw Mismatch(); }
    }
    private static void RequireBackend(PayloadBackendResult response)
    {
        if (response.Failure != PayloadBackendFailure.None) { throw new PayloadProtectionStateException(response.Failure == PayloadBackendFailure.Denied ? UnreadableProtectedDataReason.ProviderDenied : response.Failure == PayloadBackendFailure.Unavailable ? UnreadableProtectedDataReason.ProviderUnavailable : UnreadableProtectedDataReason.ConsistencyMismatch); }
    }
    private static void RequireCommit(PayloadStateCommitResult result)
    {
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
