// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; amendment sections 1-6; approved review correction section 8.
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
        using PayloadLocalOperation local = _leases.LocalOperations.Begin(context.Identity, cancellationToken);
        cancellationToken = local.Token;
        PayloadProtectionCompletionContext? completion = null;
        CoreProtectionResult? result = null;
        try
        {
            result = await _core.ProtectEventAsync(payload, selectedPaths, context, async token =>
            {
                (PayloadProtectionMaterial material, PayloadProtectionCompletionContext issued) = await ReserveUniqueAsync(context, token).ConfigureAwait(false);
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
        using PayloadLocalOperation local = _leases.LocalOperations.Begin(context.Identity, cancellationToken);
        cancellationToken = local.Token;
        PayloadProtectionCompletionContext? completion = null;
        ProtectedSnapshotPayloadV2 result = await _core.ProtectSnapshotAsync(payload, context, async token =>
        {
            (PayloadProtectionMaterial material, PayloadProtectionCompletionContext issued) = await ReserveUniqueAsync(context, token).ConfigureAwait(false);
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
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
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
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long completionStarted = _clock.GetTimestamp();
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
            if (outcome == PayloadProtectionPersistenceOutcome.Persisted && operation.Value.Phase == "Persisted"
                && operation.Value.Result == "Succeeded" && claim.Value.Status == "Completed"
                && !work.Pages.SelectMany(p => p.Value.Entries).Any(e => e.OperationId == completion.OperationId))
            { receipt.CompletedOutcome = outcome; return; }
            if (claim.Value.Status != "Pending" || claim.Value.Attempt != 0) { throw Mismatch(); }
            string leaseKey = PayloadProtectionStateKeys.Completion(scope, completion.KeyReference, completion.DekVersion, completion.OperationId);
            PayloadProtectionStateValue<CompletionLeaseV1>? lease = await PayloadOperationBudget.ReadAsync<CompletionLeaseV1>(_store, leaseKey, cancellationToken).ConfigureAwait(false);
            var mutations = new List<PayloadProtectionStateMutation>();
            if (outcome == PayloadProtectionPersistenceOutcome.Persisted)
            {
                PayloadProtectionStateValue<LifecycleV1> currentLifecycle = await RequiredAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), cancellationToken).ConfigureAwait(false);
                if (currentLifecycle.Value.State == "Invalidating")
                {
                    if (receipt.LeaseStarted is null || currentLifecycle.Value.Epoch <= completion.LifecycleEpoch) { throw Mismatch(); }
                    PayloadProtectionStateValue<FenceV1> currentFence = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(_roots.Require(completion.Identity)), cancellationToken).ConfigureAwait(false);
                    if (currentFence.Value.ApprovalEpoch < key.Value.FenceApprovalEpoch || currentFence.Value.HighestWrittenEpoch < key.Value.HighestWrittenEpoch
                        || currentFence.Value.HighestWrittenFormat != "V2") { throw Mismatch(); }
                    ScopeKeyIndexSnapshot retainedScope = await _indexes.ReadScopeAsync(scope, cancellationToken).ConfigureAwait(false);
                    KekKeyIndexSnapshot retainedInventory = await _indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.Value.BackendId), PayloadProtectionStateKeys.Digest(key.Value.ProviderKeyVersionId), cancellationToken).ConfigureAwait(false);
                    PayloadKeyIndexes.VerifyMembership(key.Value, retainedScope, retainedInventory);
                    List<PayloadProtectionStateMutation> resolved = [PayloadProtectionStateMutation.Compare(currentLifecycle), PayloadProtectionStateMutation.Compare(currentFence),
                        PayloadProtectionStateMutation.Compare(key), PayloadProtectionStateMutation.Compare(retainedScope.Head!), .. retainedScope.Pages.Select(PayloadProtectionStateMutation.Compare),
                        PayloadProtectionStateMutation.Compare(retainedInventory.Head!), .. retainedInventory.Pages.Select(PayloadProtectionStateMutation.Compare),
                        PayloadProtectionStateMutation.Put(operation.Value with { Phase = "Persisted", Result = "Succeeded", ReasonCode = "none", UpdatedAtUtc = Now() }, operation.ETag),
                        .. _catalogs.CompleteWork(work, claim, claim.Value with { Status = "Completed", ReasonCode = "none", UpdatedAtUtc = Now() }),
                        PayloadProtectionStateMutation.Put(Audit(completion.Identity, completion.OperationId, "Activate", "Reserved", "Reserved", currentLifecycle.Value.Epoch, currentFence.Value.ApprovalEpoch, "Succeeded", "none", key.Value, 1, Elapsed(completionStarted)))];
                    if (lease is not null) { resolved.Add(new(leaseKey, null, lease.ETag)); }
                    RequireCommit(await _store.CommitAsync(resolved, cancellationToken).ConfigureAwait(false));
                    receipt.CompletedOutcome = outcome;
                    return;
                }
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
                    completion.LifecycleEpoch, key.Value.FenceApprovalEpoch, "Succeeded", "none", key.Value, 1, Elapsed(completionStarted))));
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
                mutations.Add(PayloadProtectionStateMutation.Put(Audit(completion.Identity, completion.OperationId, "Activate", "Reserved", "Reserved",
                    completion.LifecycleEpoch, key.Value.FenceApprovalEpoch, outcome == PayloadProtectionPersistenceOutcome.Unknown ? "Pending" : "Failed", reason, key.Value, 1, Elapsed(completionStarted))));
            }
            if (outcome != PayloadProtectionPersistenceOutcome.Unknown && lease is not null) { mutations.Add(new(leaseKey, null, lease.ETag)); }
            RequireCommit(await _store.CommitAsync(mutations, cancellationToken).ConfigureAwait(false));
            if (outcome != PayloadProtectionPersistenceOutcome.Unknown) { receipt.CompletedOutcome = outcome; }
        }
        finally { receipt.Gate.Release(); }
    }

    private async ValueTask<(PayloadProtectionMaterial Material, PayloadProtectionCompletionContext Completion)> ReserveUniqueAsync(
        PayloadProtectionContext context, CancellationToken cancellationToken)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < 16 && _clock.GetElapsedTime(started) < TimeSpan.FromSeconds(10); attempt++)
        {
            try { return await ReserveAsync(context, cancellationToken).ConfigureAwait(false); }
            catch (PayloadKeyCollisionException) { cancellationToken.ThrowIfCancellationRequested(); }
        }
        throw Mismatch();
    }

    private async ValueTask<(PayloadProtectionMaterial Material, PayloadProtectionCompletionContext Completion)> ReserveAsync(
        PayloadProtectionContext context, CancellationToken cancellationToken)
    {
        PayloadWriteAuthority authority = await AuthorityAsync(context.Identity, cancellationToken).ConfigureAwait(false);
        using PayloadLocalOperation local = _leases.LocalOperations.Begin(context.Identity, cancellationToken);
        cancellationToken = local.Token;
        PayloadProtectionMaterial? material = null;
        byte[]? wrapped = null;
        OperationV1? intent = null;
        uint backendAttempts = 0;
        Task<PayloadBackendResult>? backendWork = null;
        long started = _clock.GetTimestamp();
        try
        {
            string scope = PayloadProtectionStateKeys.ScopeDigest(context.Identity);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                material = _generator.Generate(_ => true, cancellationToken);
                if (await PayloadOperationBudget.ReadAsync<WrappedDekV1>(_store, PayloadProtectionStateKeys.Key(scope, material.KeyReference, material.DekVersion), cancellationToken).ConfigureAwait(false) is null) { break; }
                CryptographicOperations.ZeroMemory(material.DataEncryptionKey);
                material = null;
            }
            if (material is null) { throw Mismatch(); }
            string operationId = _entropy.CreateKeyReference();
            string now = Now();
            var operation = new OperationV1(operationId, "Create", scope, material.KeyReference, material.DekVersion, "Started", 1, now, now, "Pending", "none");
            var reconcile = new ReconciliationV1(operationId, scope, material.KeyReference, material.DekVersion, "Create", PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow().AddSeconds(90)), 0, "Pending", "none", now);
            WorkCatalogSnapshot work = await _catalogs.ReadWorkAsync(_roots.Require(context.Identity), cancellationToken).ConfigureAwait(false);
            RequireCommit(await _store.CommitAsync([.. authority.Conditions, PayloadProtectionStateMutation.Compare(authority.Lifecycle),
                PayloadProtectionStateMutation.Compare(authority.Fence), PayloadProtectionStateMutation.Put(operation), PayloadProtectionStateMutation.Put(reconcile),
                .. _catalogs.AddWork(work, new(operationId, scope))], cancellationToken).ConfigureAwait(false));
            intent = operation;
            using PayloadBackendResult response = await _executor.ExecuteAsync(_backend,
                token => new ValueTask<PayloadBackendResult>(backendWork = _backend.WrapAsync(operationId, material.DataEncryptionKey, token).AsTask()), cancellationToken,
                retainAbandoned: local.RetainUntil, attempted: attempt => backendAttempts = checked((uint)attempt)).ConfigureAwait(false);
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
            PayloadStateCommitResult reservationResult = await _store.CommitAsync([.. authority.Conditions, PayloadProtectionStateMutation.Compare(authority.Lifecycle),
                PayloadProtectionStateMutation.Put(advancedFence, authority.Fence.ETag), PayloadProtectionStateMutation.Put(key),
                .. _indexes.SetScopeEntry(scopeIndex, new(key.KeyRef, key.DekVersion, inventory.ProviderDigest, "Reserved"), true),
                .. _indexes.SetInventoryEntry(inventory, new(scope, key.KeyRef, key.DekVersion), true),
                PayloadProtectionStateMutation.Put(operation with { Phase = "Reserved", Attempt = backendAttempts, UpdatedAtUtc = Now() }, current.ETag),
                PayloadProtectionStateMutation.Put(reconcile with { UpdatedAtUtc = Now() }, claim.ETag),
                PayloadProtectionStateMutation.Put(Audit(context.Identity, operationId, "Create", null, "Reserved", key.LifecycleEpoch,
                    fence.ApprovalEpoch, "Pending", "none", key, backendAttempts, Elapsed(started)))], cancellationToken).ConfigureAwait(false);
            if (reservationResult == PayloadStateCommitResult.Conflict)
            {
                PayloadProtectionStateValue<WrappedDekV1>? collision = await PayloadOperationBudget.ReadAsync<WrappedDekV1>(_store, PayloadProtectionStateCodec.KeyFor(key), cancellationToken).ConfigureAwait(false);
                if (collision is not null && collision.Value.CreatingOperationId != operationId)
                {
                    WorkCatalogSnapshot collidedWork = await _catalogs.ReadWorkAsync(_roots.Require(context.Identity), cancellationToken).ConfigureAwait(false);
                    RequireCommit(await _store.CommitAsync([PayloadProtectionStateMutation.Put(operation with { Phase = "Failed", Result = "Failed", ReasonCode = "consistency-mismatch", UpdatedAtUtc = Now() }, current.ETag),
                        .. _catalogs.CompleteWork(collidedWork, claim, claim.Value with { Status = "Completed", ReasonCode = "consistency-mismatch", UpdatedAtUtc = Now() })], cancellationToken).ConfigureAwait(false));
                    throw new PayloadKeyCollisionException();
                }
            }
            RequireCommit(reservationResult);
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw Unavailable(); }
            var completion = new PayloadProtectionCompletionContext(context.Identity, new(context.RecordSequence, context.PayloadKind, context.PayloadTypeId),
                operationId, key.KeyRef, key.DekVersion, key.LifecycleEpoch);
            PayloadProtectionMaterial output = material;
            material = null;
            return (output, completion);
        }
        catch (Exception failure) when (failure is not PayloadKeyCollisionException)
        {
            if (intent is not null) { await RecordFailureAsync(context.Identity, intent, backendAttempts, started, failure, backendWork?.IsCompleted != false).ConfigureAwait(false); }
            throw;
        }
        finally
        {
            if (material is not null) { CryptographicOperations.ZeroMemory(material.DataEncryptionKey); }
            if (wrapped is not null) { CryptographicOperations.ZeroMemory(wrapped); }
        }
    }

    private async ValueTask MarkReturnedAsync(PayloadProtectionCompletionContext completion, CancellationToken cancellationToken)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
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
        PayloadProtectionAuditAdmission.Require(_allowAuditScope, identity);
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
    private PayloadProtectionAuditRecord Audit(AggregateIdentity identity, string operationId, string operation, string? from, string? to,
        ulong lifecycleEpoch, ulong fenceEpoch, string result, string reason, WrappedDekV1? key, uint attempts = 1, ulong duration = 0)
    {
        PayloadProtectionAuditAdmission.Require(_allowAuditScope, identity);
        bool reference = key is not null && _keyReferencePolicy != KeyReferencePolicy.NoKeyReference;
        return new(_entropy.CreateKeyReference(), operationId, null, null, identity.TenantId, identity.Domain, null, operation,
            from, to, lifecycleEpoch, fenceEpoch, _backend.BackendId, "v2", reference ? PayloadProtectionStateKeys.Digest(key!.KeyRef)[..16] : null,
            reference ? PayloadProtectionStateKeys.Digest(key!.ProviderKeyVersionId)[..16] : null, attempts, duration, result, reason, _actor, Now());
    }
    private async ValueTask RecordFailureAsync(AggregateIdentity identity, OperationV1 intent, uint attempts, long started, Exception failure, bool foregroundStopped)
    {
        // Cleanup has its own small bound: caller cancellation cannot erase discoverable mutation ambiguity.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10), _clock);
        try
        {
            PayloadProtectionStateValue<OperationV1>? current = await _store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(intent.OperationId), cleanup.Token).ConfigureAwait(false);
            PayloadProtectionStateValue<ReconciliationV1>? claim = await _store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(intent.OperationId), cleanup.Token).ConfigureAwait(false);
            if (current is null || claim is null || current.Value.OperationId != intent.OperationId || current.Value.ScopeDigest != intent.ScopeDigest
                || claim.Value.Status != "Pending" || claim.Value.Attempt != 0
                || current.Value.KeyRef != intent.KeyRef || current.Value.DekVersion != intent.DekVersion || current.Value.Phase is "Completed" or "Failed") { return; }
            bool definite = failure is PayloadProtectionStateException state && state.Reason is UnreadableProtectedDataReason.ProviderDenied or UnreadableProtectedDataReason.ConsistencyMismatch;
            string reason = definite ? (failure is PayloadProtectionStateException { Reason: UnreadableProtectedDataReason.ProviderDenied } ? "provider-denied" : "consistency-mismatch") : "ambiguous-outcome";
            PayloadProtectionStateValue<LifecycleV1> lifecycle = await _store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(intent.ScopeDigest), cleanup.Token).ConfigureAwait(false) ?? throw Mismatch();
            PayloadProtectionStateValue<FenceV1> fence = await _store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(_roots.Require(identity)), cleanup.Token).ConfigureAwait(false) ?? throw Mismatch();
            _ = await _store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { Phase = definite ? "Failed" : "Ambiguous", Result = definite ? "Failed" : "Pending", Attempt = attempts, ReasonCode = reason, UpdatedAtUtc = Now() }, current.ETag),
                PayloadProtectionStateMutation.Put(claim.Value with { ReasonCode = reason, Status = "Pending", NotBeforeUtc = foregroundStopped ? Now() : claim.Value.NotBeforeUtc, UpdatedAtUtc = Now() }, claim.ETag),
                PayloadProtectionStateMutation.Put(Audit(identity, intent.OperationId, "Create", null, null, lifecycle.Value.Epoch, fence.Value.ApprovalEpoch, definite ? "Failed" : "Pending", reason, null, attempts, Elapsed(started)))], cleanup.Token).ConfigureAwait(false);
        }
        catch { /* A failed bounded recording attempt leaves the original indexed intent authoritative; never hide the original failure. */ }
    }
    private ulong Elapsed(long started) => checked((ulong)Math.Max(0, Math.Floor(_clock.GetElapsedTime(started).TotalMilliseconds)));
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
        => await PayloadOperationBudget.ReadAsync<T>(_store, key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
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
