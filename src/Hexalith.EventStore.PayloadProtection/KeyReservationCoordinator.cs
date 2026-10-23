// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; amendment sections 1-6.
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Orders indexed intent, wrap, atomic reservation, output and exact issued-context persistence completion.</summary>
internal sealed class KeyReservationCoordinator
{
    private readonly IPayloadProtectionStateStore _store;
    private readonly PayloadProtectionScopeRegistry _roots;
    private readonly PayloadProtectionLeaseCoordinator _leases;
    private readonly ErasureStateRegistry _erasure;
    private readonly PayloadProtectionBackend _backend;
    private readonly PayloadBackendExecutor _executor;
    private readonly PayloadProtectionCatalogs _catalogs;
    private readonly PayloadKeyIndexes _indexes;
    private readonly PayloadProtectionCore _core;
    private readonly IPayloadProtectionEntropy _entropy;
    private readonly PayloadProtectionMaterialGenerator _generator;
    private readonly TimeProvider _clock;
    private readonly Func<AggregateIdentity, bool> _allowAuditScope;
    private readonly string _actor;
    private readonly KeyReferencePolicy _keyReferencePolicy;
    private readonly ConditionalWeakTable<PayloadProtectionCompletionContext, PayloadProtectionCompletionReceipt> _receipts = new();

    /// <summary>Requires explicit audit permission and host-owned authority; no wildcard, fallback or production backend is introduced.</summary>
    internal KeyReservationCoordinator(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
        PayloadProtectionLeaseCoordinator leases, ErasureStateRegistry erasure, PayloadProtectionBackend backend,
        PayloadBackendExecutor executor, Func<AggregateIdentity, bool> allowAuditScope, string decisionActor,
        KeyReferencePolicy keyReferencePolicy = KeyReferencePolicy.NoKeyReference, TimeProvider? timeProvider = null,
        IPayloadProtectionEntropy? entropy = null, PayloadProtectionCore? core = null)
    {
        _store = store;
        _roots = roots;
        _leases = leases;
        _erasure = erasure;
        _backend = backend;
        _executor = executor;
        _clock = timeProvider ?? TimeProvider.System;
        _catalogs = new(store, _clock);
        _indexes = new(store, _clock);
        _entropy = entropy ?? new CryptographicPayloadProtectionEntropy();
        _generator = new(_entropy);
        _core = core ?? new();
        _allowAuditScope = allowAuditScope ?? throw new PayloadProtectionFormatException();
        _actor = decisionActor;
        _keyReferencePolicy = keyReferencePolicy;
        _ = CanonicalText.GetByteCount(decisionActor, 1, 256);
        if (!Enum.IsDefined(keyReferencePolicy)) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Runs full core preflight before obtaining any material; zero selection creates no durable key or intent.</summary>
    internal async ValueTask<(CoreProtectionResult Result, PayloadProtectionCompletionContext? Completion)> ProtectEventAsync(
        byte[] payload, IReadOnlyCollection<string> selectedPaths, PayloadProtectionContext context, CancellationToken cancellationToken = default)
    {
        _ = _roots.Require(context.Identity);
        PayloadProtectionCompletionContext? completion = null;
        CoreProtectionResult? result = null;
        try
        {
            result = await _core.ProtectEventAsync(payload, selectedPaths, context, async token =>
            {
                (PayloadProtectionMaterial material, PayloadProtectionCompletionContext issued) = await ReserveAsync(context, token).ConfigureAwait(false);
                completion = issued;
                return material;
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (completion is not null)
            {
                await MarkReturnedAsync(completion, cancellationToken).ConfigureAwait(false);
                _receipts.Add(completion, new());
            }
            cancellationToken.ThrowIfCancellationRequested();
            CoreProtectionResult output = result;
            result = null;
            return (output, completion);
        }
        finally { if (result is not null) { CryptographicOperations.ZeroMemory(result.PayloadBytes); } }
    }

    /// <summary>Protects one selected whole snapshot; the immutable carrier is returned only after confirmed reservation.</summary>
    internal async ValueTask<(ProtectedSnapshotPayloadV2 Result, PayloadProtectionCompletionContext Completion)> ProtectSnapshotAsync(
        byte[] payload, PayloadProtectionContext context, CancellationToken cancellationToken = default)
    {
        _ = _roots.Require(context.Identity);
        PayloadProtectionCompletionContext? completion = null;
        ProtectedSnapshotPayloadV2 result = await _core.ProtectSnapshotAsync(payload, context, async token =>
        {
            (PayloadProtectionMaterial material, PayloadProtectionCompletionContext issued) = await ReserveAsync(context, token).ConfigureAwait(false);
            completion = issued;
            return material;
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (completion is null) { throw Mismatch(); }
        await MarkReturnedAsync(completion, cancellationToken).ConfigureAwait(false);
        _receipts.Add(completion, new());
        cancellationToken.ThrowIfCancellationRequested();
        return (result, completion);
    }

    /// <summary>Acquires completion authority only for the original call-scoped context that this process issued.</summary>
    internal async ValueTask AcquireCompletionAsync(PayloadProtectionCompletionContext completion, CancellationToken cancellationToken = default)
    {
        PayloadProtectionCompletionReceipt receipt = Receipt(completion);
        await receipt.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (receipt.CompletedOutcome is not null) { return; }
            if (receipt.LeaseStarted is long previous)
            {
                if (_clock.GetElapsedTime(previous) >= TimeSpan.FromSeconds(60)) { throw Unavailable(); }
                return;
            }
            PayloadWriteAuthority authority = await AuthorityAsync(completion.Identity, cancellationToken).ConfigureAwait(false);
            if (authority.Lifecycle.Value.Epoch != completion.LifecycleEpoch) { throw Mismatch(); }
            long started = _clock.GetTimestamp();
            await _leases.AcquireCompletionAsync(completion, authority.Lifecycle, cancellationToken).ConfigureAwait(false);
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(60)) { throw Unavailable(); }
            receipt.LeaseStarted = started;
        }
        finally { receipt.Gate.Release(); }
    }

    /// <summary>Only confirmed actor persistence activates; ambiguous and absent outcomes retain indexed Reserved material.</summary>
    internal async ValueTask CompleteAsync(PayloadProtectionCompletionContext completion, PayloadProtectionPersistenceOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(outcome) || outcome == 0) { throw Mismatch(); }
        PayloadProtectionCompletionReceipt receipt = Receipt(completion);
        await receipt.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (receipt.CompletedOutcome is PayloadProtectionPersistenceOutcome completed)
            {
                if (completed != outcome) { throw Mismatch(); }
                return;
            }
            string scope = PayloadProtectionStateKeys.ScopeDigest(completion.Identity);
            PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, completion.KeyReference, completion.DekVersion), cancellationToken).ConfigureAwait(false);
            ValidateKey(key.Value, completion, allowActive: true);
            PayloadProtectionStateValue<OperationV1> operation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(completion.OperationId), cancellationToken).ConfigureAwait(false);
            ValidateOperation(operation.Value, completion);
            PayloadProtectionStateValue<ReconciliationV1> claim = await RequiredAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(completion.OperationId), cancellationToken).ConfigureAwait(false);
            if (claim.Value.ScopeDigest != scope || claim.Value.KeyRef != completion.KeyReference || claim.Value.DekVersion != completion.DekVersion || claim.Value.Kind != "Create") { throw Mismatch(); }
            WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(_roots.Require(completion.Identity), cancellationToken).ConfigureAwait(false);
            if (key.Value.State == "Active")
            {
                if (outcome != PayloadProtectionPersistenceOutcome.Persisted || operation.Value.Phase != "Completed"
                    || operation.Value.Result != "Succeeded" || claim.Value.Status != "Completed"
                    || work.Pages.SelectMany(p => p.Value.Entries).Any(e => e.OperationId == completion.OperationId)) { throw Mismatch(); }
                ScopeKeyIndexSnapshot completedScope = await _indexes.ReadScopeAsync(scope, cancellationToken).ConfigureAwait(false);
                KekKeyIndexSnapshot completedInventory = await _indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId),
                    PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false);
                PayloadKeyIndexes.VerifyMembership(key.Value, completedScope, completedInventory);
                receipt.CompletedOutcome = outcome;
                return;
            }
            string leaseKey = PayloadProtectionStateKeys.Completion(scope, completion.KeyReference, completion.DekVersion, completion.OperationId);
            PayloadProtectionStateValue<CompletionLeaseV1>? lease = await _store.ReadAsync<CompletionLeaseV1>(leaseKey, cancellationToken).ConfigureAwait(false);
            var mutations = new List<PayloadProtectionStateMutation>();
            if (outcome == PayloadProtectionPersistenceOutcome.Persisted)
            {
                if (receipt.LeaseStarted is not long started || _clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(60)
                    || lease is null || lease.Value.InstanceId != _leases.InstanceId || lease.Value.LifecycleEpoch != completion.LifecycleEpoch
                    || PayloadProtectionStateCodec.ParseTimestamp(lease.Value.ExpiresAtUtc) <= _clock.GetUtcNow()) { throw Mismatch(); }
                PayloadWriteAuthority authority = await AuthorityAsync(completion.Identity, cancellationToken).ConfigureAwait(false);
                if (authority.Lifecycle.Value.Epoch != completion.LifecycleEpoch) { throw Mismatch(); }
                ScopeKeyIndexSnapshot scopeIndex = await _indexes.ReadScopeAsync(scope, cancellationToken).ConfigureAwait(false);
                KekKeyIndexSnapshot inventory = await _indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId),
                    PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false);
                PayloadKeyIndexes.VerifyMembership(key.Value, scopeIndex, inventory);
                mutations.AddRange(authority.Conditions);
                mutations.Add(PayloadProtectionStateMutation.Compare(authority.Lifecycle));
                mutations.Add(PayloadProtectionStateMutation.Compare(authority.Fence));
                mutations.Add(PayloadProtectionStateMutation.Compare(inventory.Head!));
                mutations.Add(PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(key.Value with { State = "Active", UpdatedAtUtc = Now() }), key.ETag));
                mutations.AddRange(_indexes.SetScopeEntry(scopeIndex, new(key.Value.KeyRef, key.Value.DekVersion,
                    PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), "Active"), false));
                mutations.AddRange(_catalogs.CompleteWork(work, claim, claim.Value with { Status = "Completed", ReasonCode = "none", UpdatedAtUtc = Now() }));
                mutations.Add(PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Completed", Result = "Succeeded", ReasonCode = "none", UpdatedAtUtc = Now() }, operation.ETag));
                mutations.Add(PayloadProtectionStateMutation.Put(Audit(completion.Identity, completion.OperationId, "Activate", "Reserved", "Active",
                    completion.LifecycleEpoch, key.Value.FenceApprovalEpoch, "Succeeded", "none", key.Value)));
            }
            else
            {
                string reason = outcome == PayloadProtectionPersistenceOutcome.Unknown ? "ambiguous-outcome" : "not-persisted";
                mutations.Add(PayloadProtectionStateMutation.Compare(key));
                mutations.Add(PayloadProtectionStateMutation.Put(operation.Value with { Phase = outcome == PayloadProtectionPersistenceOutcome.Unknown ? "Ambiguous" : "Failed",
                    Result = outcome == PayloadProtectionPersistenceOutcome.Unknown ? "Pending" : "Failed", ReasonCode = reason, UpdatedAtUtc = Now() }, operation.ETag));
                mutations.Add(PayloadProtectionStateMutation.Put(claim.Value with { Status = "Pending", ReasonCode = reason,
                    NotBeforeUtc = outcome == PayloadProtectionPersistenceOutcome.Unknown ? Now()
                        : PayloadProtectionStateCodec.Timestamp(PayloadProtectionStateCodec.ParseTimestamp(key.Value.CreatedAtUtc).AddHours(24)), UpdatedAtUtc = Now() }, claim.ETag));
                mutations.Add(PayloadProtectionStateMutation.Compare(work.Head));
            }
            if (outcome != PayloadProtectionPersistenceOutcome.Unknown && lease is not null) { mutations.Add(new(leaseKey, null, lease.ETag)); }
            RequireCommit(await _store.CommitAsync(mutations, cancellationToken).ConfigureAwait(false));
            if (outcome != PayloadProtectionPersistenceOutcome.Unknown) { receipt.CompletedOutcome = outcome; }
        }
        finally { receipt.Gate.Release(); }
    }

    private async ValueTask<(PayloadProtectionMaterial Material, PayloadProtectionCompletionContext Completion)> ReserveAsync(
        PayloadProtectionContext context, CancellationToken cancellationToken)
    {
        PayloadWriteAuthority authority = await AuthorityAsync(context.Identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionMaterial? material = null;
        byte[]? wrapped = null;
        long started = _clock.GetTimestamp();
        try
        {
            string scope = PayloadProtectionStateKeys.ScopeDigest(context.Identity);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                material = _generator.Generate(_ => true, cancellationToken);
                if (await _store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, material.KeyReference, material.DekVersion), cancellationToken).ConfigureAwait(false) is null) { break; }
                CryptographicOperations.ZeroMemory(material.DataEncryptionKey);
                material = null;
            }
            if (material is null) { throw Mismatch(); }
            string operationId = _entropy.CreateKeyReference();
            string now = Now();
            var operation = new OperationV1(operationId, "Create", scope, material.KeyReference, material.DekVersion, "Started", 1, now, now, "Pending", "none");
            var reconcile = new ReconciliationV1(operationId, scope, material.KeyReference, material.DekVersion, "Create", now, 0, "Pending", "none", now);
            WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(_roots.Require(context.Identity), cancellationToken).ConfigureAwait(false);
            RequireCommit(await _store.CommitAsync([.. authority.Conditions, PayloadProtectionStateMutation.Compare(authority.Lifecycle),
                PayloadProtectionStateMutation.Compare(authority.Fence), PayloadProtectionStateMutation.Put(operation), PayloadProtectionStateMutation.Put(reconcile),
                .. _catalogs.AddWork(work, new(operationId, scope))], cancellationToken).ConfigureAwait(false));
            using PayloadBackendResult response = await _executor.ExecuteAsync(_backend,
                token => _backend.WrapAsync(operationId, material.DataEncryptionKey, token), cancellationToken).ConfigureAwait(false);
            if (response.Failure != PayloadBackendFailure.None) { throw BackendFailure(response.Failure); }
            if (response.ProviderKeyVersionId is null) { throw Mismatch(); }
            wrapped = response.TakeBytes();
            PayloadWriteAuthority refreshed = await AuthorityAsync(context.Identity, cancellationToken).ConfigureAwait(false);
            if (refreshed.Lifecycle.Value.Epoch != authority.Lifecycle.Value.Epoch
                || refreshed.Fence.Value.ApprovalEpoch != authority.Fence.Value.ApprovalEpoch
                || refreshed.Fence.Value.ApprovalId != authority.Fence.Value.ApprovalId
                || refreshed.Fence.Value.DeploymentGeneration != authority.Fence.Value.DeploymentGeneration
                || refreshed.Fence.Value.HighestWrittenEpoch < authority.Fence.Value.HighestWrittenEpoch
                || (authority.Fence.Value.HighestWrittenFormat == "V2" && refreshed.Fence.Value.HighestWrittenFormat != "V2")) { throw Mismatch(); }
            authority = refreshed;
            PayloadProtectionStateValue<OperationV1> current = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operationId), cancellationToken).ConfigureAwait(false);
            PayloadProtectionStateValue<ReconciliationV1> claim = await RequiredAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operationId), cancellationToken).ConfigureAwait(false);
            if (current.Value != operation || claim.Value != reconcile) { throw Mismatch(); }
            FenceV1 fence = authority.Fence.Value;
            ulong watermark = fence.HighestWrittenFormat == "V2" ? fence.HighestWrittenEpoch : checked(fence.HighestWrittenEpoch + 1);
            var advancedFence = fence with { HighestWrittenFormat = "V2", HighestWrittenEpoch = watermark, UpdatedAtUtc = Now() };
            WrappedDekV1 key = PayloadProtectionStateCodec.Seal(new(context.Identity.TenantId, context.Identity.Domain, context.Identity.AggregateId, scope,
                material.KeyReference, material.DekVersion, "Reserved", _backend.BackendId, response.ProviderKeyVersionId, "RSA-OAEP-256",
                Base64UrlCodec.Encode(wrapped), authority.Lifecycle.Value.Epoch, fence.ApprovalEpoch, watermark, operationId, now, Now(), new('0', 64), new('0', 64)));
            ScopeKeyIndexSnapshot scopeIndex = await _indexes.ReadScopeAsync(scope, cancellationToken).ConfigureAwait(false);
            KekKeyIndexSnapshot inventory = await _indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(_backend.BackendId),
                PayloadProtectionStateKeys.Digest(response.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false);
            RequireCommit(await _store.CommitAsync([.. authority.Conditions, PayloadProtectionStateMutation.Compare(authority.Lifecycle),
                PayloadProtectionStateMutation.Put(advancedFence, authority.Fence.ETag), PayloadProtectionStateMutation.Put(key),
                .. _indexes.SetScopeEntry(scopeIndex, new(key.KeyRef, key.DekVersion, inventory.ProviderDigest, "Reserved"), true),
                .. _indexes.SetInventoryEntry(inventory, new(scope, key.KeyRef, key.DekVersion), true),
                PayloadProtectionStateMutation.Put(operation with { Phase = "Reserved", UpdatedAtUtc = Now() }, current.ETag),
                PayloadProtectionStateMutation.Put(reconcile with { UpdatedAtUtc = Now() }, claim.ETag),
                PayloadProtectionStateMutation.Put(Audit(context.Identity, operationId, "Create", null, "Reserved", key.LifecycleEpoch,
                    fence.ApprovalEpoch, "Pending", "none", key))], cancellationToken).ConfigureAwait(false));
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw Unavailable(); }
            var completion = new PayloadProtectionCompletionContext(context.Identity, new(context.RecordSequence, context.PayloadKind, context.PayloadTypeId),
                operationId, key.KeyRef, key.DekVersion, key.LifecycleEpoch);
            PayloadProtectionMaterial output = material;
            material = null;
            return (output, completion);
        }
        finally
        {
            if (material is not null) { CryptographicOperations.ZeroMemory(material.DataEncryptionKey); }
            if (wrapped is not null) { CryptographicOperations.ZeroMemory(wrapped); }
        }
    }

    private async ValueTask MarkReturnedAsync(PayloadProtectionCompletionContext completion, CancellationToken cancellationToken)
    {
        PayloadWriteAuthority authority = await AuthorityAsync(completion.Identity, cancellationToken).ConfigureAwait(false);
        if (authority.Lifecycle.Value.Epoch != completion.LifecycleEpoch) { throw Mismatch(); }
        PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(
            PayloadProtectionStateKeys.ScopeDigest(completion.Identity), completion.KeyReference, completion.DekVersion), cancellationToken).ConfigureAwait(false);
        ValidateKey(key.Value, completion);
        if (key.Value.FenceApprovalEpoch != authority.Fence.Value.ApprovalEpoch) { throw Mismatch(); }
        PayloadProtectionStateValue<OperationV1> operation = await RequiredAsync<OperationV1>(PayloadProtectionStateKeys.Operation(completion.OperationId), cancellationToken).ConfigureAwait(false);
        ValidateOperation(operation.Value, completion);
        RequireCommit(await _store.CommitAsync([.. authority.Conditions, PayloadProtectionStateMutation.Compare(authority.Lifecycle),
            PayloadProtectionStateMutation.Compare(authority.Fence), PayloadProtectionStateMutation.Compare(key),
            PayloadProtectionStateMutation.Put(operation.Value with { Phase = "PayloadReturned", UpdatedAtUtc = Now() }, operation.ETag)], cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<PayloadWriteAuthority> AuthorityAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        string root = _roots.Require(identity);
        if (!_allowAuditScope(identity)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderDenied); }
        PayloadErasureStateResult erasure = await _erasure.ObserveAsync(identity, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (erasure.State != PayloadErasureState.Active) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
        PayloadProtectionStateValue<LifecycleV1> lifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(PayloadProtectionStateKeys.ScopeDigest(identity)), cancellationToken).ConfigureAwait(false);
        if (lifecycle.Value.Epoch != erasure.LifecycleEpoch || lifecycle.Value.State != "Active") { throw Mismatch(); }
        IReadOnlyList<PayloadProtectionStateMutation> conditions = await _leases.AdmissionAsync(identity, lifecycle.Value, true, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<FenceV1> fence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), cancellationToken).ConfigureAwait(false);
        DeploymentGenerationV1 generation = PayloadProtectionStateCodec.Decode<DeploymentGenerationV1>(conditions[0].Bytes!);
        if (fence.Value.TenantId != identity.TenantId || fence.Value.Domain != identity.Domain || fence.Value.ApprovedWriteMode != "V2"
            || fence.Value.DeploymentGeneration != generation.Generation || fence.Value.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest) { throw Mismatch(); }
        return new(lifecycle, fence, conditions);
    }
    private PayloadProtectionCompletionReceipt Receipt(PayloadProtectionCompletionContext completion)
        => _receipts.TryGetValue(completion, out PayloadProtectionCompletionReceipt? receipt) ? receipt : throw Mismatch();
    private PayloadProtectionAuditRecord Audit(AggregateIdentity identity, string operationId, string operation, string? from, string to,
        ulong lifecycleEpoch, ulong fenceEpoch, string result, string reason, WrappedDekV1 key)
    {
        if (!_allowAuditScope(identity)) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderDenied); }
        bool reference = _keyReferencePolicy != KeyReferencePolicy.NoKeyReference;
        return new(_entropy.CreateKeyReference(), operationId, null, null, identity.TenantId, identity.Domain, null, operation,
            from, to, lifecycleEpoch, fenceEpoch, _backend.BackendId, "v2", reference ? PayloadProtectionStateKeys.Digest(key.KeyRef)[..16] : null,
            reference ? PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)[..16] : null, 1, 0, result, reason, _actor, Now());
    }
    private static void ValidateOperation(OperationV1 operation, PayloadProtectionCompletionContext completion)
    {
        if (operation.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(completion.Identity) || operation.KeyRef != completion.KeyReference
            || operation.DekVersion != completion.DekVersion || operation.Operation != "Create" || operation.OperationId != completion.OperationId) { throw Mismatch(); }
    }
    private static void ValidateKey(WrappedDekV1 key, PayloadProtectionCompletionContext completion, bool allowActive = false)
    {
        if (key.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(completion.Identity) || key.TenantId != completion.Identity.TenantId
            || key.Domain != completion.Identity.Domain || key.AggregateId != completion.Identity.AggregateId || key.KeyRef != completion.KeyReference
            || key.DekVersion != completion.DekVersion || key.CreatingOperationId != completion.OperationId || key.LifecycleEpoch != completion.LifecycleEpoch
            || (key.State != "Reserved" && !(allowActive && key.State == "Active"))) { throw Mismatch(); }
    }
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => await _store.ReadAsync<T>(key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private static PayloadProtectionStateException BackendFailure(PayloadBackendFailure failure) => new(failure switch
    {
        PayloadBackendFailure.Denied => UnreadableProtectedDataReason.ProviderDenied,
        PayloadBackendFailure.Missing => UnreadableProtectedDataReason.MissingKey,
        PayloadBackendFailure.Invalidated => UnreadableProtectedDataReason.KeyInvalidatedOrDeleted,
        PayloadBackendFailure.ConsistencyMismatch => UnreadableProtectedDataReason.ConsistencyMismatch,
        _ => UnreadableProtectedDataReason.ProviderUnavailable,
    });
    private static void RequireCommit(PayloadStateCommitResult result)
    {
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied
            ? UnreadableProtectedDataReason.ProviderDenied : result == PayloadStateCommitResult.Conflict ? UnreadableProtectedDataReason.ConsistencyMismatch : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
