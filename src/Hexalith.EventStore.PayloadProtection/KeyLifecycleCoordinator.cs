// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10, 13-15; amendment sections 1-8.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Coordinates approved scope barriers, full monotonic drains and claim-fenced indexed online invalidation/deletion.</summary>
internal sealed class KeyLifecycleCoordinator
{
    private readonly IPayloadProtectionStateStore _store;
    private readonly PayloadProtectionScopeRegistry _roots;
    private readonly PayloadProtectionLeaseCoordinator _leases;
    private readonly PayloadKeyCache _cache;
    private readonly PayloadProtectionCatalogs _catalogs;
    private readonly PayloadKeyIndexes _indexes;
    private readonly PayloadReconciliationClaims _claims;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, ValueTask>? _delay;
    private readonly Func<AggregateIdentity, bool> _allowAudit;
    private readonly string _actor;
    private readonly string _backendId;

    /// <summary>Requires explicit audit-scope permission; host/workflow integration remains owned by Story 8.7.</summary>
    internal KeyLifecycleCoordinator(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
        PayloadProtectionLeaseCoordinator leases, PayloadKeyCache cache, string backendId,
        Func<AggregateIdentity, bool> allowAuditScope, string decisionActor, TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, ValueTask>? delay = null)
    {
        _store = store; _roots = roots; _leases = leases; _cache = cache;
        _clock = timeProvider ?? TimeProvider.System;
        _delay = delay;
        _catalogs = new(store, _clock); _indexes = new(store, _clock); _claims = new(store, _clock);
        _allowAudit = allowAuditScope ?? throw new PayloadProtectionFormatException();
        _actor = decisionActor; _backendId = backendId;
        _ = CanonicalText.GetByteCount(decisionActor, 1, 256);
        PersonalDataPolicyRegistry.ValidateId(backendId);
    }

    /// <summary>Commits the greater epoch, immutable member cutoff and discoverable work atomically before draining.</summary>
    internal async ValueTask<InvalidationDrainV1> BeginAsync(AggregateIdentity identity, CryptoShreddingWorkflowDecision approval,
        string operationId, ulong epoch, bool delete = false, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        string root = Admit(identity);
        _ = PayloadProtectionStateKeys.Operation(operationId);
        if (approval.State is not (CryptoShreddingWorkflowState.Approved or CryptoShreddingWorkflowState.PendingProvider)
            || !approval.Identity.TryValidate(out _) || !CanonicalUlid.IsValid(approval.Identity.WorkflowId)
            || approval.Identity.TenantId != identity.TenantId || approval.Identity.Domain != identity.Domain
            || approval.Identity.AggregateId != identity.AggregateId || approval.Identity.Scope != CryptoShreddingWorkflowScope.Aggregate) { throw Mismatch(); }
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        string kind = delete ? "Delete" : "Invalidate";
        PayloadProtectionStateValue<OperationV1>? previous = await PayloadOperationBudget.ReadAsync<OperationV1>(_store, PayloadProtectionStateKeys.Operation(operationId), cancellationToken).ConfigureAwait(false);
        if (previous is not null)
        {
            if (previous.Value.ScopeDigest != scope || previous.Value.Operation != kind || previous.Value.KeyRef != "" || previous.Value.DekVersion != 0) { throw Mismatch(); }
            InvalidationDrainV1 priorDrain = (await RequiredAsync<InvalidationDrainV1>(PayloadProtectionStateKeys.Drain(scope, operationId), cancellationToken).ConfigureAwait(false)).Value;
            if (priorDrain.LifecycleEpoch != epoch || priorDrain.TenantDomainDigest != root || priorDrain.WorkflowId != approval.Identity.WorkflowId) { throw Mismatch(); }
            return priorDrain;
        }
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<FenceV1> fence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), cancellationToken).ConfigureAwait(false);
        if (fence.Value.TenantId != identity.TenantId || fence.Value.Domain != identity.Domain) { throw Mismatch(); }
        if ((lifecycle.Value.State is not ("Active" or "Pending") && !(delete && lifecycle.Value.State == "Invalidated")) || epoch <= lifecycle.Value.Epoch) { throw Mismatch(); }
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, cancellationToken).ConfigureAwait(false);
        var drain = new InvalidationDrainV1(root, scope, operationId, epoch, members.Head.Value.EntryCount, approval.Identity.WorkflowId);
        string now = Now();
        var operation = new OperationV1(operationId, kind, scope, "", 0, "Started", 1, now, now, "Pending", "domain-invalidating");
        var reconcile = new ReconciliationV1(operationId, scope, "", 0, kind, now, 0, "Pending", "domain-invalidating", now);
        PayloadStateCommitResult result = await _store.CommitAsync([PayloadProtectionStateMutation.Put(lifecycle.Value with {
            State = lifecycle.Value.State == "Invalidated" ? "Invalidated" : "Invalidating", Epoch = epoch, OperationId = operationId, ReasonCode = lifecycle.Value.State == "Invalidated" ? "domain-invalidated" : "domain-invalidating", ObservedAtUtc = now }, lifecycle.ETag),
            PayloadProtectionStateMutation.Compare(members.Head), PayloadProtectionStateMutation.Put(drain),
            PayloadProtectionStateMutation.Compare(fence),
            PayloadProtectionStateMutation.Put(operation), PayloadProtectionStateMutation.Put(reconcile), .. _catalogs.AddWork(work, new(operationId, scope)),
            PayloadProtectionStateMutation.Put(Audit(identity, operationId, kind, lifecycle.Value.State, lifecycle.Value.State == "Invalidated" ? "Invalidated" : "Invalidating", epoch, "Pending", "domain-invalidating", 1, Elapsed(started), approval.Identity.WorkflowId, fence.Value.ApprovalEpoch))], cancellationToken).ConfigureAwait(false);
        if (result == PayloadStateCommitResult.Unknown)
        {
            // Confirm exact durable identity, then the resuming owner starts a fresh complete wait.
            PayloadProtectionStateValue<InvalidationDrainV1> confirmed = await RequiredAsync<InvalidationDrainV1>(PayloadProtectionStateKeys.Drain(scope, operationId), cancellationToken).ConfigureAwait(false);
            PayloadProtectionStateValue<LifecycleV1> barrier = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
            PayloadProtectionStateValue<OperationV1> confirmedOperation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operationId), cancellationToken).ConfigureAwait(false);
            if (confirmed.Value != drain || barrier.Value.OperationId != operationId || barrier.Value.Epoch != epoch || confirmedOperation.Value != operation) { throw Mismatch(); }
        }
        else { RequireCommit(result); }
        PayloadProtectionDiagnostics.LifecycleTransition();
        return drain;
    }

    /// <summary>Records exact clearing evidence only after this process has stopped work and cleared every matching owned buffer.</summary>
    internal async ValueTask AcknowledgeAsync(AggregateIdentity identity, string operationId, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        string root = Admit(identity);
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<InvalidationDrainV1> drain = await DrainAsync(root, lifecycle, operationId, cancellationToken).ConfigureAwait(false);
        await PayloadOperationBudget.AwaitAsync(async ct => { await _leases.LocalOperations.DrainScopeAsync(lifecycle.Value.ScopeDigest, ct).ConfigureAwait(false); return true; }, cancellationToken).ConfigureAwait(false);
        await PayloadOperationBudget.AwaitAsync(async ct => { await _cache.ClearScopeAsync(lifecycle.Value.ScopeDigest, ct).ConfigureAwait(false); return true; }, cancellationToken).ConfigureAwait(false);
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<MemberPageV1> page = members.Pages.SingleOrDefault(p => p.Value.Entries.Any(e => e.InstanceId == _leases.InstanceId)) ?? throw Mismatch();
        MemberEntryV1 member = page.Value.Entries.Single(e => e.InstanceId == _leases.InstanceId);
        if (member.Ordinal > drain.Value.MemberCutoff) { return; }
        string ackKey = PayloadProtectionStateKeys.ClearAck(lifecycle.Value.ScopeDigest, lifecycle.Value.Epoch, _leases.InstanceId);
        PayloadProtectionStateValue<CacheClearAckV1>? existing = await PayloadOperationBudget.ReadAsync<CacheClearAckV1>(_store, ackKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Value.OperationId != operationId || existing.Value.LifecycleEpoch != lifecycle.Value.Epoch || existing.Value.ScopeDigest != lifecycle.Value.ScopeDigest || existing.Value.InstanceId != member.InstanceId) { throw Mismatch(); }
            return;
        }
        var ack = new CacheClearAckV1(lifecycle.Value.ScopeDigest, operationId, lifecycle.Value.Epoch, member.InstanceId, Now());
        RequireCommit(await _store.CommitAsync([PayloadProtectionStateMutation.Compare(lifecycle), PayloadProtectionStateMutation.Compare(drain),
            PayloadProtectionStateMutation.Compare(page), PayloadProtectionStateMutation.Put(ack)], cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Waits a fresh full interval after confirmation, checks exact clearing proof, and mutates at most 256 indexed keys.</summary>
    internal async ValueTask<bool> ResumeAsync(AggregateIdentity identity, ReconciliationV1 claim, CancellationToken cancellationToken = default)
    {
        using var diagnostic = new PayloadProtectionDiagnosticScope(PayloadProtectionOperation.Invalidate);
        long started = _clock.GetTimestamp();
        string root = Admit(identity);
        if (((claim.KeyRef == "") != (claim.DekVersion == 0)) || claim.Kind is not ("Invalidate" or "Delete") || claim.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(identity)) { throw Mismatch(); }
        PayloadProtectionStateValue<LifecycleV1> lifecycle;
        using (var admissionBudget = new PayloadOperationBudget(_clock, cancellationToken))
        {
            _ = await _claims.RequireAsync(claim, cancellationToken).ConfigureAwait(false);
            lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
            _ = await DrainAsync(root, lifecycle, lifecycle.Value.OperationId, cancellationToken).ConfigureAwait(false);
        }
        await PayloadMonotonicDrain.WaitAsync(_clock, _delay, cancellationToken).ConfigureAwait(false);
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<InvalidationDrainV1> drain = await DrainAsync(root, lifecycle, lifecycle.Value.OperationId, cancellationToken).ConfigureAwait(false);
        LifecycleV1 provedLifecycle = lifecycle.Value;
        InvalidationDrainV1 provedDrain = drain.Value;
        if (claim.KeyRef == "" && provedDrain.OperationId != claim.OperationId) { throw Mismatch(); }
        bool cleared = await HasClearingProofAsync(drain.Value, cancellationToken).ConfigureAwait(false);
        if (!cleared || await HasUnresolvedPersistenceAsync(root, claim.ScopeDigest, claim.OperationId, claim.KeyRef == "", cancellationToken).ConfigureAwait(false)) { return false; }
        ScopeKeyIndexSnapshot scope = await _indexes.ReadScopeAsync(claim.ScopeDigest, cancellationToken).ConfigureAwait(false);
        ScopeIndexEntryV1[] entries = scope.Pages.SelectMany(p => p.Value.Entries).Where(e => claim.KeyRef == "" || (e.KeyRef == claim.KeyRef && e.DekVersion == claim.DekVersion)).ToArray();
        if (claim.KeyRef != "" && entries.Length == 0 && claim.Kind != "Delete") { throw Mismatch(); }
        int changed = 0;
        foreach (ScopeIndexEntryV1 entry in entries)
        {
            if (claim.Kind == "Invalidate" && entry.State == "Invalidated") { continue; }
            if (changed == 256) { return false; }
            PayloadProtectionStateValue<ReconciliationV1> currentClaim = await _claims.RequireAsync(claim, cancellationToken).ConfigureAwait(false);
            lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
            drain = await DrainAsync(root, lifecycle, lifecycle.Value.OperationId, cancellationToken).ConfigureAwait(false);
            if (lifecycle.Value != provedLifecycle || drain.Value != provedDrain) { throw Mismatch(); }
            PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, entry.KeyRef, entry.DekVersion), cancellationToken).ConfigureAwait(false);
            ValidateKey(identity, key.Value);
            scope = await _indexes.ReadScopeAsync(claim.ScopeDigest, cancellationToken).ConfigureAwait(false);
            KekKeyIndexSnapshot inventory = await _indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId), PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false);
            PayloadKeyIndexes.VerifyMembership(key.Value, scope, inventory);
            // Exact acknowledgements are immutable and Cleared tombstones are monotonic. The
            // already confirmed cutoff proof remains valid under this unchanged lifecycle/drain.
            List<PayloadProtectionStateMutation> mutations = [PayloadProtectionStateMutation.Compare(currentClaim), PayloadProtectionStateMutation.Compare(lifecycle),
                PayloadProtectionStateMutation.Compare(drain)];
            if (claim.Kind == "Delete")
            {
                mutations.Add(new(PayloadProtectionStateCodec.KeyFor(key.Value), null, key.ETag));
                mutations.AddRange(_indexes.RemoveScopeEntry(scope, entry));
                mutations.AddRange(_indexes.SetInventoryEntry(inventory, new(claim.ScopeDigest, entry.KeyRef, entry.DekVersion), false));
            }
            else
            {
                mutations.Add(PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(key.Value with { State = "Invalidated", LifecycleEpoch = lifecycle.Value.Epoch, UpdatedAtUtc = Now() }), key.ETag));
                mutations.AddRange(_indexes.SetScopeEntry(scope, entry with { State = "Invalidated" }, false));
                mutations.Add(PayloadProtectionStateMutation.Compare(inventory.Head!));
            }
            RequireCommit(await _store.CommitAsync(mutations, cancellationToken).ConfigureAwait(false));
            changed++;
        }
        // Re-enumerate both the scope and all applicable inventories before publishing an irreversible online result.
        scope = await _indexes.ReadScopeAsync(claim.ScopeDigest, cancellationToken).ConfigureAwait(false);
        var finalInventories = new Dictionary<(string Backend, string Provider), KekKeyIndexSnapshot>();
        foreach (ScopeIndexEntryV1 entry in scope.Pages.SelectMany(p => p.Value.Entries).Where(e => claim.KeyRef == "" || (e.KeyRef == claim.KeyRef && e.DekVersion == claim.DekVersion)))
        {
            if (claim.Kind == "Delete" || entry.State != "Invalidated") { throw Mismatch(); }
            WrappedDekV1 key = (await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, entry.KeyRef, entry.DekVersion), cancellationToken).ConfigureAwait(false)).Value;
            ValidateKey(identity, key);
            var address = (PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId));
            if (!finalInventories.TryGetValue(address, out KekKeyIndexSnapshot? inventory))
            {
                inventory = await _indexes.ReadKekAsync(address.Item1, address.Item2, cancellationToken).ConfigureAwait(false);
                finalInventories.Add(address, inventory);
            }
            PayloadKeyIndexes.VerifyMembership(key, scope, inventory);
        }
        PayloadProtectionStateValue<ReconciliationV1> finalClaim = await _claims.RequireAsync(claim, cancellationToken).ConfigureAwait(false);
        lifecycle = await LifecycleAsync(identity, cancellationToken).ConfigureAwait(false);
        drain = await DrainAsync(root, lifecycle, lifecycle.Value.OperationId, cancellationToken).ConfigureAwait(false);
        if (lifecycle.Value != provedLifecycle || drain.Value != provedDrain) { throw Mismatch(); }
        if (await HasUnresolvedPersistenceAsync(root, claim.ScopeDigest, claim.OperationId, claim.KeyRef == "", cancellationToken).ConfigureAwait(false)) { return false; }
        WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<OperationV1> operation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(claim.OperationId), cancellationToken).ConfigureAwait(false);
        if (operation.Value.ScopeDigest != claim.ScopeDigest || operation.Value.Operation != claim.Kind || operation.Value.KeyRef != claim.KeyRef || operation.Value.DekVersion != claim.DekVersion) { throw Mismatch(); }
        bool scopeWide = claim.KeyRef == "";
        string terminal = scopeWide ? (claim.Kind == "Delete" ? "Deleted" : "Invalidated") : lifecycle.Value.State;
        PayloadProtectionStateValue<FenceV1> fence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), cancellationToken).ConfigureAwait(false);
        if (fence.Value.TenantId != identity.TenantId || fence.Value.Domain != identity.Domain) { throw Mismatch(); }
        List<PayloadProtectionStateMutation> final = [PayloadProtectionStateMutation.Compare(drain),
            PayloadProtectionStateMutation.Compare(fence),
            scopeWide ? PayloadProtectionStateMutation.Put(lifecycle.Value with { State = terminal, Epoch = checked(lifecycle.Value.Epoch + 1), ReasonCode = claim.Kind == "Delete" ? "domain-deleted" : "domain-invalidated", ObservedAtUtc = Now() }, lifecycle.ETag) : PayloadProtectionStateMutation.Compare(lifecycle),
            PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Completed", Result = "Succeeded", ReasonCode = "none", UpdatedAtUtc = Now() }, operation.ETag),
            .. _catalogs.CompleteWork(work, finalClaim, claim with { Status = "Completed", ReasonCode = "none", UpdatedAtUtc = Now() }),
            PayloadProtectionStateMutation.Put(Audit(identity, claim.OperationId, claim.Kind, lifecycle.Value.State, terminal, scopeWide ? checked(lifecycle.Value.Epoch + 1) : lifecycle.Value.Epoch, "Succeeded", "none", claim.Attempt, Elapsed(started), drain.Value.WorkflowId, fenceEpoch: fence.Value.ApprovalEpoch))];
        if (scope.Head is not null) { final.Add(PayloadProtectionStateMutation.Compare(scope.Head)); }
        final.AddRange(finalInventories.Values.Select(inventory => PayloadProtectionStateMutation.Compare(inventory.Head!)));
        RequireCommit(await _store.CommitAsync(final, cancellationToken).ConfigureAwait(false));
        PayloadProtectionDiagnostics.LifecycleTransition();
        diagnostic.Complete();
        return true;
    }

    private async ValueTask<bool> HasClearingProofAsync(InvalidationDrainV1 drain, CancellationToken token)
    {
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(drain.TenantDomainDigest, token).ConfigureAwait(false);
        if (members.Head.Value.EntryCount < drain.MemberCutoff) { throw Mismatch(); }
        // Permanent immutable acknowledgements and monotonic tombstones need no unchanged-value
        // rewrites. Rewriting them would grow every indexed mutation and invalidate other readers.
        foreach (PayloadProtectionStateValue<MemberPageV1> page in members.Pages)
        {
            foreach (MemberEntryV1 member in page.Value.Entries.Where(e => e.Ordinal <= drain.MemberCutoff))
            {
                if (member.State == "Cleared") { continue; }
                PayloadProtectionStateValue<CacheClearAckV1>? ack = await PayloadOperationBudget.ReadAsync<CacheClearAckV1>(_store, PayloadProtectionStateKeys.ClearAck(drain.ScopeDigest, drain.LifecycleEpoch, member.InstanceId), token).ConfigureAwait(false);
                if (ack is null) { return false; }
                if (ack.Value.ScopeDigest != drain.ScopeDigest || ack.Value.OperationId != drain.OperationId || ack.Value.LifecycleEpoch != drain.LifecycleEpoch || ack.Value.InstanceId != member.InstanceId) { throw Mismatch(); }
            }
        }
        return true;
    }
    private async ValueTask<bool> HasUnresolvedPersistenceAsync(string root, string scope, string ownOperation, bool scopeWide, CancellationToken token)
    {
        WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, token).ConfigureAwait(false);
        foreach (WorkEntryV1 entry in work.Pages.SelectMany(p => p.Value.Entries).Where(e => e.ScopeDigest == scope && e.OperationId != ownOperation))
        {
            OperationV1 operation = (await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(entry.OperationId), token).ConfigureAwait(false)).Value;
            if (operation.ScopeDigest != scope) { throw Mismatch(); }
            if (operation.Result == "Pending" && (scopeWide || operation.Operation is "Create" or "Activate" or "Rewrap")) { return true; }
        }
        return false;
    }
    private async ValueTask<PayloadProtectionStateValue<LifecycleV1>> LifecycleAsync(AggregateIdentity identity, CancellationToken token)
    {
        PayloadProtectionStateValue<LifecycleV1> value = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(PayloadProtectionStateKeys.ScopeDigest(identity)), token).ConfigureAwait(false);
        if (value.Value.TenantId != identity.TenantId || value.Value.Domain != identity.Domain || value.Value.AggregateId != identity.AggregateId) { throw Mismatch(); }
        return value;
    }
    private async ValueTask<PayloadProtectionStateValue<InvalidationDrainV1>> DrainAsync(string root, PayloadProtectionStateValue<LifecycleV1> lifecycle, string operation, CancellationToken token)
    {
        PayloadProtectionStateValue<InvalidationDrainV1> drain = await RequiredAsync<InvalidationDrainV1>(PayloadProtectionStateKeys.Drain(lifecycle.Value.ScopeDigest, operation), token).ConfigureAwait(false);
        if (lifecycle.Value.State is not ("Invalidating" or "Invalidated") || lifecycle.Value.OperationId != operation || drain.Value.OperationId != operation || drain.Value.ScopeDigest != lifecycle.Value.ScopeDigest || drain.Value.LifecycleEpoch != lifecycle.Value.Epoch || drain.Value.TenantDomainDigest != root) { throw Mismatch(); }
        if (lifecycle.Value.State == "Invalidated")
        {
            OperationV1 deletion = (await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation), token).ConfigureAwait(false)).Value;
            if (deletion.Operation != "Delete" || deletion.ScopeDigest != drain.Value.ScopeDigest || deletion.KeyRef != "" || deletion.DekVersion != 0 || deletion.Result != "Pending") { throw Mismatch(); }
        }
        return drain;
    }
    private string Admit(AggregateIdentity identity)
    {
        string root = _roots.Require(identity);
        PayloadProtectionAuditAdmission.Require(_allowAudit, identity);
        return root;
    }
    private static void ValidateKey(AggregateIdentity identity, WrappedDekV1 key)
    {
        if (key.TenantId != identity.TenantId || key.Domain != identity.Domain || key.AggregateId != identity.AggregateId || key.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(identity)) { throw Mismatch(); }
    }
    private PayloadProtectionAuditRecord Audit(AggregateIdentity identity, string operation, string kind, string from, string to,
        ulong epoch, string result, string reason, uint attempts, ulong duration, string? workflow = null, ulong fenceEpoch = 0)
    {
        _ = Admit(identity);
        return new(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), operation, workflow, null, identity.TenantId, identity.Domain, null,
            kind, from, to, epoch, fenceEpoch, _backendId, "v2", null, null, attempts, duration, result, reason, _actor, Now());
    }
    private ulong Elapsed(long started) => checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds)));
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(_store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static void RequireCommit(PayloadStateCommitResult result)
    {
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : result == PayloadStateCommitResult.Conflict ? UnreadableProtectedDataReason.ConsistencyMismatch : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
