// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.3-10.8 and 15; amendment sections 1-4 and 7.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Visits only enrolled roots and one bounded work page per pass; restart reconstructs work from durable catalogs.</summary>
internal sealed class PayloadProtectionReconciler : BackgroundService
{
    private readonly IPayloadProtectionStateStore _store;
    private readonly PayloadProtectionScopeRegistry _roots;
    private readonly PayloadProtectionCatalogs _catalogs;
    private readonly PayloadReconciliationClaims _claims;
    private readonly KeyLifecycleCoordinator _lifecycle;
    private readonly TimeProvider _clock;
    private readonly Func<AggregateIdentity, bool> _allowAudit;
    private readonly string _backend;
    private readonly string _actor;
    private readonly uint[] _pages;
    private readonly SemaphoreSlim _pass = new(1, 1);
    private int _root;
    private readonly PayloadKeyRotationCoordinator? _rotation;
    private readonly PayloadPersistenceEvidenceSource? _persistence;
    private readonly PayloadProtectionBackend? _backendProvider;
    private readonly PayloadBackendExecutor _executor;

    /// <summary>Creates the sole bounded hosted worker; registration in the production host belongs to Story 8.7.</summary>
    internal PayloadProtectionReconciler(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
        KeyLifecycleCoordinator lifecycle, string backendId, Func<AggregateIdentity, bool> allowAuditScope,
        string decisionActor, TimeProvider? timeProvider = null, PayloadKeyRotationCoordinator? rotation = null,
        PayloadPersistenceEvidenceSource? persistence = null, PayloadProtectionBackend? backend = null,
        PayloadBackendExecutor? executor = null)
    {
        _store = store; _roots = roots; _lifecycle = lifecycle; _clock = timeProvider ?? TimeProvider.System;
        _catalogs = new(store, _clock); _claims = new(store, _clock); _pages = new uint[roots.Roots.Count];
        _allowAudit = allowAuditScope ?? throw new PayloadProtectionFormatException();
        _actor = decisionActor; _backend = backendId;
        _rotation = rotation;
        _persistence = persistence;
        _backendProvider = backend; _executor = executor ?? new(timeProvider);
        if (backend is not null && backend.BackendId != backendId) { throw new PayloadProtectionFormatException(); }
        _ = CanonicalText.GetByteCount(decisionActor, 1, 256);
        PersonalDataPolicyRegistry.ValidateId(backendId);
    }

    /// <summary>Processes at most 256 entries from one enrolled root, rotating both roots and retained page numbers.</summary>
    internal async ValueTask<int> RunPassAsync(CancellationToken cancellationToken = default)
    {
        await _pass.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int index = _root;
            _root = (_root + 1) % _roots.Roots.Count;
            PayloadProtectionScope enrolled = _roots.Roots[index];
            string root = PayloadProtectionStateKeys.TenantDomainDigest(enrolled.TenantId, enrolled.Domain);
            WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, cancellationToken).ConfigureAwait(false);
            if (work.Pages.Count == 0) { return 0; }
            uint number = _pages[index] % checked((uint)work.Pages.Count);
            _pages[index] = (number + 1) % checked((uint)work.Pages.Count);
            WorkEntryV1[] entries = work.Pages[(int)number].Value.Entries;
            int processed = 0;
            foreach (WorkEntryV1 entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    PayloadProtectionStateValue<LifecycleV1> lifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(entry.ScopeDigest), cancellationToken).ConfigureAwait(false);
                    var identity = new AggregateIdentity(lifecycle.Value.TenantId, lifecycle.Value.Domain, lifecycle.Value.AggregateId);
                    if (_roots.Require(identity) != root || lifecycle.Value.ScopeDigest != entry.ScopeDigest) { throw Mismatch(); }
                    PayloadProtectionAuditAdmission.Require(_allowAudit, identity);
                    PayloadProtectionStateValue<ReconciliationV1>? claim = await _claims.TryClaimAsync(entry, cancellationToken).ConfigureAwait(false);
                    if (claim is null) { continue; }
                    processed++;
                    if (claim.Value.KeyRef == "")
                    {
                        await _lifecycle.AcknowledgeAsync(identity, entry.OperationId, cancellationToken).ConfigureAwait(false);
                        _ = await _lifecycle.ResumeAsync(identity, claim.Value, cancellationToken).ConfigureAwait(false);
                    }
                    else if (claim.Value.Kind is "Create" or "Activate")
                    {
                        await ReconcileReservationAsync(identity, root, claim.Value, cancellationToken).ConfigureAwait(false);
                    }
                    else if (claim.Value.Kind == "Rewrap" && _rotation is not null)
                    {
                        _ = await _rotation.ReconcileAsync(identity, claim.Value, token: cancellationToken).ConfigureAwait(false);
                    }
                    else if (claim.Value.Kind is "Invalidate" or "Delete")
                    {
                        await _lifecycle.AcknowledgeAsync(identity, lifecycle.Value.OperationId, cancellationToken).ConfigureAwait(false);
                        _ = await _lifecycle.ResumeAsync(identity, claim.Value, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (PayloadProtectionStateException) { /* Leave discoverable work intact on denial, uncertainty or a fenced claim. */ }
            }
            return processed;
        }
        finally { _pass.Release(); }
    }

    /// <summary>Runs bounded passes; faults never cause blind mutation replay or untracked work removal.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { _ = await RunPassAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (PayloadProtectionStateException) { /* Root admission remains fail-closed until state is available and consistent. */ }
            await Task.Delay(TimeSpan.FromSeconds(1), _clock, stoppingToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ReconcileReservationAsync(AggregateIdentity identity, string root, ReconciliationV1 claim, CancellationToken token)
    {
        using var diagnostic = new PayloadProtectionDiagnosticScope(PayloadProtectionOperation.Reconcile);
        using var operationBudget = new PayloadOperationBudget(_clock, token);
        long started = _clock.GetTimestamp();
        PayloadProtectionStateValue<OperationV1> operation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(claim.OperationId), token).ConfigureAwait(false);
        if (operation.Value.ScopeDigest != claim.ScopeDigest || operation.Value.KeyRef != claim.KeyRef || operation.Value.DekVersion != claim.DekVersion
            || operation.Value.Operation != claim.Kind) { throw Mismatch(); }
        if (operation.Value.Phase == "Ambiguous" && _backendProvider is not null
            && await PayloadOperationBudget.ReadAsync<WrappedDekV1>(_store, PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), token).ConfigureAwait(false) is null)
        {
            // No payload can precede a Reserved record. Reconcile the old wrap by stable identity; never wrap again.
            using PayloadBackendResult outcome = await _executor.ExecuteAsync(_backendProvider,
                ct => _backendProvider.ReconcileAsync(claim.OperationId, ct), token, allowRetry: false, operation: PayloadProtectionOperation.Reconcile).ConfigureAwait(false);
            if (outcome.Failure is not (PayloadBackendFailure.None or PayloadBackendFailure.Missing)) { return; }
            PayloadProtectionStateValue<ReconciliationV1> held = await _claims.RequireAsync(claim, token).ConfigureAwait(false);
            string reconciledAt = Now();
            PayloadStateCommitResult reconciled = await _store.CommitAsync([
                PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Failed", Result = "Failed", ReasonCode = "not-persisted", UpdatedAtUtc = reconciledAt }, operation.ETag),
                PayloadProtectionStateMutation.Put(claim with { Status = "Pending", ReasonCode = "not-persisted", NotBeforeUtc = PayloadProtectionStateCodec.Timestamp(PayloadProtectionStateCodec.ParseTimestamp(operation.Value.CreatedAtUtc).AddHours(24)), UpdatedAtUtc = reconciledAt }, held.ETag)], token).ConfigureAwait(false);
            if (reconciled != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
            return;
        }
        PayloadProtectionPersistenceOutcome evidence;
        try
        {
            evidence = _persistence is null ? PayloadProtectionPersistenceOutcome.Unknown
                : await _persistence.ObserveAsync(identity, operation.Value, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
        token.ThrowIfCancellationRequested(); PayloadOperationBudget.RequireCurrent();
        if (!Enum.IsDefined(evidence) || evidence == 0) { throw Mismatch(); }
        if (evidence == PayloadProtectionPersistenceOutcome.Persisted)
        {
            await ActivateAsync(identity, root, claim, operation, started, token).ConfigureAwait(false);
            diagnostic.Complete();
            return;
        }
        // Expiry revokes authority; it cannot resolve an ambiguous actor save into absence.
        if ((evidence == PayloadProtectionPersistenceOutcome.Unknown && (operation.Value.Phase == "Ambiguous" || operation.Value.ReasonCode == "ambiguous-outcome"))
            || _clock.GetUtcNow() - PayloadProtectionStateCodec.ParseTimestamp(operation.Value.CreatedAtUtc) < TimeSpan.FromHours(24)) { return; }
        string leaseKey = PayloadProtectionStateKeys.Completion(claim.ScopeDigest, claim.KeyRef, claim.DekVersion, claim.OperationId);
        if (await PayloadOperationBudget.ReadAsync<CompletionLeaseV1>(_store, leaseKey, token).ConfigureAwait(false) is not null) { return; }
        PayloadProtectionStateValue<WrappedDekV1>? key = await PayloadOperationBudget.ReadAsync<WrappedDekV1>(_store, PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), token).ConfigureAwait(false);
        List<PayloadProtectionStateMutation> conditions = [];
        if (key is not null)
        {
            if (key.Value.State != "Reserved" || key.Value.CreatingOperationId != claim.OperationId || key.Value.ScopeDigest != claim.ScopeDigest
                || key.Value.TenantId != identity.TenantId || key.Value.Domain != identity.Domain || key.Value.AggregateId != identity.AggregateId) { throw Mismatch(); }
            var indexes = new PayloadKeyIndexes(_store, _clock);
            ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(claim.ScopeDigest, token).ConfigureAwait(false);
            KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId), PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), token).ConfigureAwait(false);
            PayloadKeyIndexes.VerifyMembership(key.Value, scope, inventory);
            conditions.Add(PayloadProtectionStateMutation.Compare(key));
            conditions.Add(PayloadProtectionStateMutation.Compare(scope.Head!));
            conditions.Add(PayloadProtectionStateMutation.Compare(inventory.Head!));
        }
        PayloadProtectionStateValue<ReconciliationV1> current = await _claims.RequireAsync(claim, token).ConfigureAwait(false);
        WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, token).ConfigureAwait(false);
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(claim.ScopeDigest), token).ConfigureAwait(false);
        PayloadProtectionAuditAdmission.Require(_allowAudit, identity);
        string now = Now();
        var audit = new PayloadProtectionAuditRecord(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), claim.OperationId, null, null,
            identity.TenantId, identity.Domain, null, "Reconcile", key?.Value.State, key?.Value.State, lifecycle.Value.Epoch,
            key?.Value.FenceApprovalEpoch ?? 0, _backend, "v2", null, null, claim.Attempt,
            checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds))), "Failed", "orphan-quarantined", _actor, now);
        PayloadStateCommitResult result = await _store.CommitAsync([.. conditions, PayloadProtectionStateMutation.Compare(lifecycle),
            .. _catalogs.CompleteWork(work, current, claim with { Status = "Quarantined", ReasonCode = "orphan-quarantined", UpdatedAtUtc = now }),
            PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Failed", Result = "Failed", ReasonCode = "orphan-quarantined", UpdatedAtUtc = now }, operation.ETag),
            PayloadProtectionStateMutation.Put(audit)], token).ConfigureAwait(false);
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
        diagnostic.Complete();
    }

    private async ValueTask ActivateAsync(AggregateIdentity identity, string root, ReconciliationV1 claim,
        PayloadProtectionStateValue<OperationV1> operation, long started, CancellationToken token)
    {
        PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(claim.ScopeDigest, claim.KeyRef, claim.DekVersion), token).ConfigureAwait(false);
        if (key.Value.State != "Reserved" || key.Value.CreatingOperationId != claim.OperationId
            || key.Value.TenantId != identity.TenantId || key.Value.Domain != identity.Domain || key.Value.AggregateId != identity.AggregateId) { throw Mismatch(); }
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(claim.ScopeDigest), token).ConfigureAwait(false);
        PayloadProtectionStateValue<FenceV1> fence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), token).ConfigureAwait(false);
        if (lifecycle.Value.State is not ("Active" or "Pending") || lifecycle.Value.Epoch < key.Value.LifecycleEpoch
            || fence.Value.TenantId != identity.TenantId || fence.Value.Domain != identity.Domain
            || fence.Value.ApprovalEpoch < key.Value.FenceApprovalEpoch || fence.Value.HighestWrittenEpoch < key.Value.HighestWrittenEpoch
            || fence.Value.HighestWrittenFormat != "V2" || fence.Value.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest) { throw Mismatch(); }
        var indexes = new PayloadKeyIndexes(_store, _clock);
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(claim.ScopeDigest, token).ConfigureAwait(false);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId), PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), token).ConfigureAwait(false);
        PayloadKeyIndexes.VerifyMembership(key.Value, scope, inventory);
        PayloadProtectionStateValue<ReconciliationV1> current = await _claims.RequireAsync(claim, token).ConfigureAwait(false);
        WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(root, token).ConfigureAwait(false);
        string leaseKey = PayloadProtectionStateKeys.Completion(claim.ScopeDigest, claim.KeyRef, claim.DekVersion, claim.OperationId);
        PayloadProtectionStateValue<CompletionLeaseV1>? lease = await PayloadOperationBudget.ReadAsync<CompletionLeaseV1>(_store, leaseKey, token).ConfigureAwait(false);
        string now = Now();
        var audit = new PayloadProtectionAuditRecord(new CryptographicPayloadProtectionEntropy().CreateKeyReference(), claim.OperationId, null, null,
            identity.TenantId, identity.Domain, null, "Activate", "Reserved", "Active", lifecycle.Value.Epoch, fence.Value.ApprovalEpoch,
            _backend, "v2", null, null, claim.Attempt, checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds))), "Succeeded", "none", _actor, now);
        List<PayloadProtectionStateMutation> mutations = [PayloadProtectionStateMutation.Compare(lifecycle), PayloadProtectionStateMutation.Compare(fence),
            PayloadProtectionStateMutation.Compare(inventory.Head!), PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(key.Value with { State = "Active", UpdatedAtUtc = now }), key.ETag),
            .. indexes.SetScopeEntry(scope, new(claim.KeyRef, claim.DekVersion, inventory.ProviderDigest, "Active"), false),
            PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Completed", Result = "Succeeded", ReasonCode = "none", UpdatedAtUtc = now }, operation.ETag),
            .. _catalogs.CompleteWork(work, current, claim with { Status = "Completed", ReasonCode = "none", UpdatedAtUtc = now }), PayloadProtectionStateMutation.Put(audit)];
        if (lease is not null) { mutations.Add(new(leaseKey, null, lease.ETag)); }
        PayloadStateCommitResult result = await _store.CommitAsync(mutations, token).ConfigureAwait(false);
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(_store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
