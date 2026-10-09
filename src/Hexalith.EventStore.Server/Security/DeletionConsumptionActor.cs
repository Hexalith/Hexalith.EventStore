using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual DAPR durable same-tenant block/reserve/activation owner. Registration and independent authority/backend remain disabled by default.</summary>
/// <remarks>Production must use non-reentrant actor turns and a qualified durable conditional state store. Provider absence is not destruction evidence.</remarks>
/// <param name="host">Exact private tenant actor.</param>
/// <param name="authority">Independently authenticated signature/online guard and registrar verifier.</param>
/// <param name="provider">Qualified physical all-or-none exact reservation backend.</param>
/// <param name="clock">Whole physical recovery deadline clock; omission uses the system operational clock.</param>
public sealed class DeletionConsumptionActor(ActorHost host, IDeletionConsumptionAuthority? authority = null,
    IAtomicDeletionManifestProvider? provider = null, TimeProvider? clock = null) : Actor(host), IDeletionConsumptionActor
{
    private const string StateKey = "deletion-consumption-v23";
    private readonly AsyncLocal<AuthoritativeStreamReadDeadline?> _entryBudget = new();
    private readonly AsyncLocal<EntryOutcomeHolder?> _entryReservation = new();
    private Task? _unfinishedStateIo;
    private AuthoritativeStreamReadDeadline Budget => _entryBudget.Value ?? throw new InvalidOperationException("Deletion consumption entry budget is absent.");
    private async Task<T> RunEntryAsync<T>(Func<Task<T>> operation, T unavailable)
    {
        var operationClock = clock ?? TimeProvider.System;
        using var budget = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), operationClock, CancellationToken.None, operationClock.GetTimestamp());
        var previous = _entryBudget.Value; var previousReservation = _entryReservation.Value;
        _entryBudget.Value = budget; _entryReservation.Value = new EntryOutcomeHolder();
        try
        {
            budget.ThrowIfCancellationRequested(); CheckStateIoReady();
            T result = await operation().ConfigureAwait(false);
            budget.ThrowIfCancellationRequested(); return result;
        }
        catch (TimeoutException) { return OriginalReservationOrUnavailable(unavailable); }
        catch (OperationCanceledException) when (budget.IsExpired) { return OriginalReservationOrUnavailable(unavailable); }
        finally { _entryBudget.Value = previous; _entryReservation.Value = previousReservation; }
    }
    private T OriginalReservationOrUnavailable<T>(T unavailable)
    {
        // A previously authenticated durable reservation is safe to report after its physical
        // recovery times out. Never return another batch's covering reservation to this caller.
        if (unavailable is DeletionConsumptionOutcome missing && _entryReservation.Value?.Reservation is { } original
            && original.TenantId == missing.TenantId && original.BatchId == missing.BatchId)
        { return (T)(object)original; }
        return unavailable;
    }
    private void CheckStateIoReady()
    {
        if (_unfinishedStateIo is { IsCompleted: false }) { throw new TimeoutException("Previous deletion consumption state I/O is unfinished."); }
        if (_unfinishedStateIo is not null) { _ = _unfinishedStateIo.Exception; _unfinishedStateIo = null; }
    }
    /// <summary>Excludes the Dapr post-method save from an unfinished state-manager call in the timed-out actor turn.</summary>
    protected override async Task OnPostActorMethodAsync(ActorMethodContext actorMethodContext)
    {
        if (_unfinishedStateIo is { } pending) { await pending.ConfigureAwait(false); }
        await base.OnPostActorMethodAsync(actorMethodContext).ConfigureAwait(false);
    }

    /// <summary>Excludes Dapr failure cleanup from an unfinished state-manager call in the timed-out actor turn.</summary>
    protected override async Task OnActorMethodFailedAsync(ActorMethodContext actorMethodContext, Exception exception)
    {
        if (_unfinishedStateIo is { } pending)
        {
            try { await pending.ConfigureAwait(false); }
            catch (Exception) { /* The original failure remains authoritative after state I/O quiesces. */ }
        }
        await base.OnActorMethodFailedAsync(actorMethodContext, exception).ConfigureAwait(false);
    }
    private async Task<T> StateIoAsync<T>(Func<Task<T>> operation)
    {
        Budget.ThrowIfCancellationRequested(); CheckStateIoReady();
        Task<T> pending = operation(); _unfinishedStateIo = pending;
        return await Budget.WaitAsync(pending).ConfigureAwait(false);
    }
    private async Task StateIoAsync(Func<Task> operation)
    {
        Budget.ThrowIfCancellationRequested(); CheckStateIoReady();
        Task pending = operation(); _unfinishedStateIo = pending;
        await Budget.WaitAsync(pending).ConfigureAwait(false);
    }
    private Task<T> CaptureAsync<T>(Func<T> capture) => Budget.ReadAsync(_ => Task.FromResult(capture()));
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request)
    { ArgumentNullException.ThrowIfNull(request); return RunEntryAsync(() => RegisterEntryAsync(request), Unavailable(request.Capability.TenantId, request.Capability.BatchId)); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request)
    { ArgumentNullException.ThrowIfNull(request); return RunEntryAsync(() => ReserveAndConsumeEntryAsync(request), Unavailable(request.Capability.TenantId, request.Capability.BatchId)); }
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> BlockAsync(DeletionBatchBlockRequest request)
    { ArgumentNullException.ThrowIfNull(request); return RunEntryAsync(() => BlockEntryAsync(request), Unavailable(request.TenantId, request.BatchId)); }
    /// <inheritdoc/>
    public Task<DeletionCapabilityRevocationReceipt?> RegisterRevocationAsync(DeletionCapabilityRevocationEnvelope envelope)
        => RunEntryAsync(() => RegisterRevocationEntryAsync(envelope), (DeletionCapabilityRevocationReceipt?)null);
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation)
    { ArgumentNullException.ThrowIfNull(activation); return RunEntryAsync(() => ActivateEntryAsync(activation), Unavailable(activation.Replacement.Capability.TenantId, activation.Replacement.Capability.BatchId)); }
    /// <inheritdoc/>
    public Task<DeletionActivationComparison?> ReadActivationComparisonAsync(string tenantId, string batchId, string replacementKeyVersion)
        => RunEntryAsync(() => ReadActivationComparisonEntryAsync(tenantId, batchId, replacementKeyVersion), (DeletionActivationComparison?)null);
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ReconcileBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request)
    { ArgumentNullException.ThrowIfNull(request); return RunEntryAsync(() => ReconcileBlockedReplacementEntryAsync(request), Unavailable(request.Capability.TenantId, request.Capability.BatchId)); }
    /// <inheritdoc/>
    public Task<DeletionBlockedReplacementResult?> ReadBlockedReplacementAsync(DeletionBatchCapabilityV1 capability)
        => RunEntryAsync(() => ReadBlockedReplacementEntryAsync(capability), (DeletionBlockedReplacementResult?)null);
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> LookupAsync(string tenantId, string batchId)
        => RunEntryAsync(() => LookupEntryAsync(tenantId, batchId), Unavailable(tenantId, batchId));
    /// <inheritdoc/>
    public Task<DeletionCapabilityRevocationReceipt?> LookupRevocationAsync(DeletionCapabilityRevocationEnvelope envelope)
        => RunEntryAsync(() => LookupRevocationEntryAsync(envelope), (DeletionCapabilityRevocationReceipt?)null);

    /// <summary>Gets the exact private actor registration name.</summary>
    public const string ActorTypeName = "DeletionConsumptionActor";
    /// <summary>Gets the exact same-tenant actor shared by reserve, block, revocation and activation.</summary>
    public static string GetActorId(string tenantId) => new AggregateIdentity(tenantId, "protection", "deletion-consumption-v23").ActorId;
    private async Task<DeletionConsumptionOutcome> RegisterEntryAsync(DeletionBatchConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.Capability.TenantId);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        var result = await RegisterAsyncCoreAsync(request).ConfigureAwait(false);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> RegisterAsyncCoreAsync(DeletionBatchConsumptionRequest request)
    {
        var owned = await CaptureAsync(() => DeletionConsumptionIdentity.Capture(request)).ConfigureAwait(false); string tenant = owned.Capability.TenantId; Check(tenant);
        if (!await AdmitAsync(tenant, owned.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, owned.Capability.BatchId); }
        var state = await ReadAsync(tenant, true).ConfigureAwait(false); var batch = Find(state, owned.Capability.BatchId);
        if (batch is not null) { return DeletionConsumptionIdentity.Same(batch.Current, owned) ? batch.Outcome : Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (authority is null || provider is null || !await Budget.ReadAsync(token => authority.VerifyDispatchAsync(owned, token)).ConfigureAwait(false))
        { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        var reservation = FindCoveredReservation(state, owned);
        if (reservation is not null)
        {
            var original = await RecoverAsync(state, reservation, false).ConfigureAwait(false);
            return original.Status == DeletionConsumptionStatus.Consumed
                ? await RetainCoveredAsync(await ReadAsync(tenant).ConfigureAwait(false), owned,
                    Array.AsReadOnly(original.TargetReceipts.Where(r => owned.Targets.Contains(r.Target)).ToArray())).ConfigureAwait(false)
                : Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable);
        }
        var prior = FindDestroyed(state, owned);
        if (prior is not null) { return await RetainCoveredAsync(state, owned, prior).ConfigureAwait(false); }
        if (OverlapsCoveredOrReserved(state, owned)) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (state.Batches.Count >= 1000) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        var compromised = KeyBlock(state, owned.Capability.CapabilityKeyVersion);
        var next = state with { Revision = checked(state.Revision + 1) };
        var outcome = Result(next, owned.Capability.BatchId, compromised is null ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ConsumptionBlocked)
            with { ReceiptId = Receipt("register", owned, next.Revision), BlockReason = compromised is null ? null : DeletionConsumptionBlockReason.CapabilityKeyCompromise,
                BlockedKeyVersion = compromised?.Envelope.KeyVersion, RevocationRevision = compromised?.Envelope.RevocationRevision };
        next = next with { Batches = state.Batches.Append(new DeletionConsumptionBatch(owned, owned, outcome)).ToArray() };
        await SaveAsync(next).ConfigureAwait(false); return outcome;
    }
    private async Task<DeletionConsumptionOutcome> ReserveAndConsumeEntryAsync(DeletionBatchConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.Capability.TenantId);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        var result = await ReserveAndConsumeAsyncCoreAsync(request).ConfigureAwait(false);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> ReserveAndConsumeAsyncCoreAsync(DeletionBatchConsumptionRequest request)
    {
        var owned = await CaptureAsync(() => DeletionConsumptionIdentity.Capture(request)).ConfigureAwait(false); string tenant = owned.Capability.TenantId; Check(tenant);
        if (!await AdmitAsync(tenant, owned.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, owned.Capability.BatchId); }
        var state = await ReadAsync(tenant, true).ConfigureAwait(false); var batch = Find(state, owned.Capability.BatchId);
        if (batch is null) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        if (!DeletionConsumptionIdentity.Same(batch.Current, owned)) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (batch.Outcome.Status == DeletionConsumptionStatus.ConsumptionReserved) { return await RecoverAsync(state, batch, false).ConfigureAwait(false); }
        if (batch.Outcome.Status != DeletionConsumptionStatus.Unconsumed) { return batch.Outcome; }
        if (authority is null || provider is null || !await Budget.ReadAsync(token => authority.VerifyDispatchAsync(owned, token)).ConfigureAwait(false))
        { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        var keyBlock = KeyBlock(state, owned.Capability.CapabilityKeyVersion);
        if (keyBlock is not null) { throw new InvalidOperationException("Registered unconsumed batch bypassed durable key block."); }
        var covered = FindDestroyed(state, owned);
        if (covered is not null) { return await RetainCoveredAsync(state, owned, covered).ConfigureAwait(false); }
        if (OverlapsCoveredOrReserved(state, owned)) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var reserved = batch with { Outcome = Result(next, owned.Capability.BatchId, DeletionConsumptionStatus.ConsumptionReserved)
            with { ReceiptId = Receipt("reserve", owned, next.Revision) } };
        next = Replace(next, reserved); await SaveAsync(next).ConfigureAwait(false);
        return await RecoverAsync(next, reserved, true).ConfigureAwait(false);
    }
    private async Task<DeletionConsumptionOutcome> BlockEntryAsync(DeletionBatchBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.TenantId);
        if (!(await AdmitAsync(request.TenantId, request.BatchId, "BlockDeletionBatchConsumption").ConfigureAwait(false))) { return Unavailable(request.TenantId, request.BatchId); }
        var result = await BlockAsyncCoreAsync(request).ConfigureAwait(false);
        if (!(await AdmitAsync(request.TenantId, request.BatchId, "BlockDeletionBatchConsumption").ConfigureAwait(false))) { return Unavailable(request.TenantId, request.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> BlockAsyncCoreAsync(DeletionBatchBlockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.TenantId); DeletionConsumptionIdentity.Text(request.BatchId);
        DeletionConsumptionIdentity.Text(request.OperationId); DeletionConsumptionIdentity.Text(request.AdmissionEvidenceId);
        if (!await AdmitAsync(request.TenantId, request.BatchId, "BlockDeletionBatchConsumption").ConfigureAwait(false)) { return Unavailable(request.TenantId, request.BatchId); }
        var state = await ReadAsync(request.TenantId, true).ConfigureAwait(false); string digest = DeletionConsumptionIdentity.Digest(request);
        var prior = state.Operations.SingleOrDefault(o => o.OperationId == request.OperationId);
        if (prior is not null) { return prior.RequestDigest == digest ? prior.Outcome : Result(state, request.BatchId, DeletionConsumptionStatus.Conflict); }
        if (state.Operations.Count >= 10000) { return Result(state, request.BatchId, DeletionConsumptionStatus.Unavailable); }
        var batch = Find(state, request.BatchId);
        if (batch is null || authority is null || !await Budget.ReadAsync(token => authority.VerifyAdmissionBlockAsync(request, token)).ConfigureAwait(false))
        { return Result(state, request.BatchId, DeletionConsumptionStatus.Unavailable); }
        var next = state with { Revision = checked(state.Revision + 1) }; var outcome = batch.Outcome;
        if (outcome.Status == DeletionConsumptionStatus.Unconsumed
            || outcome.Status == DeletionConsumptionStatus.ConsumptionBlocked && outcome.BlockReason == DeletionConsumptionBlockReason.CapabilityKeyCompromise)
        {
            outcome = Result(next, request.BatchId, DeletionConsumptionStatus.ConsumptionBlocked) with {
                ReceiptId = DeletionConsumptionIdentity.Digest(new[] { request.OperationId, digest, "admission-block" }), BlockReason = DeletionConsumptionBlockReason.AdmissionIntegrity };
            next = Replace(next, batch with { Outcome = outcome });
        }
        next = next with { Operations = state.Operations.Append(new DeletionConsumptionOperation(request.OperationId, digest, outcome)).ToArray() };
        await SaveAsync(next).ConfigureAwait(false); return outcome;
    }
    private async Task<DeletionCapabilityRevocationReceipt?> RegisterRevocationEntryAsync(DeletionCapabilityRevocationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope); Check(envelope.TenantId);
        if (!(await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "RegisterDeletionCapabilityRevocation").ConfigureAwait(false))) { return null; }
        var result = await RegisterRevocationAsyncCoreAsync(envelope).ConfigureAwait(false);
        if (!(await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "RegisterDeletionCapabilityRevocation").ConfigureAwait(false))) { return null; }
        return result;
    }
    private async Task<DeletionCapabilityRevocationReceipt?> RegisterRevocationAsyncCoreAsync(DeletionCapabilityRevocationEnvelope envelope)
    {
        DeletionConsumptionIdentity.Revocation(envelope); Check(envelope.TenantId);
        if (!await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "RegisterDeletionCapabilityRevocation").ConfigureAwait(false)) { return null; }
        var state = await ReadAsync(envelope.TenantId, true).ConfigureAwait(false);
        var prior = state.Revocations.SingleOrDefault(r => r.Envelope.EventIdentity == envelope.EventIdentity);
        if (prior is not null) { if (prior.Envelope != envelope) { throw new ArgumentException("Changed revocation event identity.", nameof(envelope)); } return prior; }
        if (state.Revocations.Count >= 10000) { return null; }
        if (authority is null || !await Budget.ReadAsync(token => authority.VerifyRevocationAsync(envelope, token)).ConfigureAwait(false)) { return null; }
        var old = KeyBlock(state, envelope.KeyVersion);
        if (old is not null && envelope.RevocationRevision <= old.Envelope.RevocationRevision) { return null; }
        var affected = state.Batches.Where(b => b.Current.Capability.CapabilityKeyVersion == envelope.KeyVersion
            && b.Outcome.Status == DeletionConsumptionStatus.Unconsumed).Select(b => b.Current.Capability.BatchId).Order(StringComparer.Ordinal).ToArray();
        var next = state with { Revision = checked(state.Revision + 1), KeyBlockSetRevision = checked(state.KeyBlockSetRevision + 1) };
        string receipt = DeletionConsumptionIdentity.Digest(envelope);
        var result = new DeletionCapabilityRevocationReceipt(envelope, next.Revision, next.KeyBlockSetRevision, receipt, Array.AsReadOnly(affected));
        next = next with { Revocations = state.Revocations.Append(result).ToArray(), Batches = state.Batches.Select(b => affected.Contains(b.Current.Capability.BatchId)
            ? b with { Outcome = Result(next, b.Current.Capability.BatchId, DeletionConsumptionStatus.ConsumptionBlocked) with {
                ReceiptId = DeletionConsumptionIdentity.Digest(new[] { receipt, b.Current.Capability.BatchId }), BlockReason = DeletionConsumptionBlockReason.CapabilityKeyCompromise,
                BlockedKeyVersion = envelope.KeyVersion, RevocationRevision = envelope.RevocationRevision } } : b).ToArray() };
        await SaveAsync(next).ConfigureAwait(false); return result;
    }
    private async Task<DeletionConsumptionOutcome> ActivateEntryAsync(DeletionReattestationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation); var c = activation.Replacement.Capability; Check(c.TenantId);
        if (!(await AdmitAsync(c.TenantId, c.BatchId, "ActivateReattestedDeletionBatch").ConfigureAwait(false))) { return Unavailable(c.TenantId, c.BatchId); }
        var result = await ActivateAsyncCoreAsync(activation).ConfigureAwait(false);
        if (!(await AdmitAsync(c.TenantId, c.BatchId, "ActivateReattestedDeletionBatch").ConfigureAwait(false))) { return Unavailable(c.TenantId, c.BatchId); }
        return result;
    }
    private async Task<DeletionActivationComparison?> ReadActivationComparisonEntryAsync(string tenantId, string batchId, string replacementKeyVersion)
    {
        Check(tenantId); DeletionConsumptionIdentity.Text(batchId); DeletionConsumptionIdentity.Text(replacementKeyVersion);
        if (!await AdmitAsync(tenantId, batchId, "ReadDeletionActivationComparison").ConfigureAwait(false)) { return null; }
        var state = await ReadAsync(tenantId).ConfigureAwait(false); var batch = Find(state, batchId);
        if (batch?.Outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked || batch.Outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise
            || string.IsNullOrWhiteSpace(batch.Outcome.ReceiptId)) { return null; }
        // An already completed original blocked activation may still await its guard mirror.
        // The exact original activation retains the preceding guard block; unrelated successors use the current block.
        var priorActivation = state.Operations.SingleOrDefault(o => o.ActivationOriginal?.Replacement.Capability == batch.Current.Capability
            && o.ActivationOriginal.Replacement.Capability.CapabilityKeyVersion == replacementKeyVersion
            && o.Outcome.Status == DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise && o.Outcome.ReceiptId == batch.Outcome.ReceiptId);
        var comparison = new DeletionActivationComparison(tenantId, batchId, priorActivation?.ActivationOriginal?.CompromiseBlockReceiptId ?? batch.Outcome.ReceiptId, state.KeyBlockSetRevision,
            replacementKeyVersion, KeyBlock(state, replacementKeyVersion) is not null, state.Revision)
            { ReplacementKeyRevocation = KeyBlock(state, replacementKeyVersion) };
        var final = await ReadAsync(tenantId).ConfigureAwait(false);
        return DeletionConsumptionIdentity.Digest(final) == DeletionConsumptionIdentity.Digest(state)
            && await AdmitAsync(tenantId, batchId, "ReadDeletionActivationComparison").ConfigureAwait(false) ? comparison : null;
    }
    private async Task<DeletionConsumptionOutcome> ReconcileBlockedReplacementEntryAsync(DeletionBlockedReplacementReconciliation request)
    {
        var owned = await CaptureAsync(() => DeletionConsumptionIdentity.Capture(request)).ConfigureAwait(false); string tenant = owned.Capability.TenantId; string id = owned.Capability.BatchId; Check(tenant);
        if (!await AdmitAsync(tenant, id, "ReconcileBlockedDeletionReplacement").ConfigureAwait(false)) { return Unavailable(tenant, id); }
        var state = await ReadAsync(tenant, true).ConfigureAwait(false); string digest = DeletionConsumptionIdentity.Digest(owned);
        var prior = state.Operations.SingleOrDefault(o => o.OperationId == owned.OperationId);
        if (prior is not null)
        { return prior.RequestDigest == digest && await AdmitAsync(tenant, id, "ReconcileBlockedDeletionReplacement").ConfigureAwait(false) ? prior.Outcome : Result(state, id, DeletionConsumptionStatus.Conflict); }
        var batch = Find(state, id); var keyBlock = KeyBlock(state, owned.Capability.CapabilityKeyVersion);
        bool originalBlockedActivation = batch is not null && batch.Current.Capability == owned.Capability && state.Operations.Any(o =>
            o.ActivationOriginal is { } activation && activation.Replacement.Capability == owned.Capability
            && activation.CompromiseBlockReceiptId == owned.CompromiseBlockReceiptId && activation.GuardReplacementReceiptId == owned.GuardReplacementReceiptId
            && activation.Replacement.DetachedJws == owned.DetachedJws && activation.Replacement.CommittedIssuedGuardRevision == owned.CommittedIssuedGuardRevision
            && o.Outcome.Status == DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise && o.Outcome.ReceiptId == batch.Outcome.ReceiptId);
        if (batch is null || batch.Outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked || batch.Outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise
            || !originalBlockedActivation && batch.Outcome.ReceiptId != owned.CompromiseBlockReceiptId || !DeletionConsumptionIdentity.SameBatch(batch.Original, owned)
            || !originalBlockedActivation && (owned.Capability.AttestationOrdinal != checked((batch.BlockedReplacement?.Capability.AttestationOrdinal ?? batch.Current.Capability.AttestationOrdinal) + 1)
                || owned.Capability.CapabilityKeyVersion == (batch.BlockedReplacement?.Capability.CapabilityKeyVersion ?? batch.Current.Capability.CapabilityKeyVersion))
            || owned.ExpectedKeyBlockSetRevision != state.KeyBlockSetRevision || keyBlock is null || DeletionConsumptionIdentity.Digest(keyBlock) != DeletionConsumptionIdentity.Digest(owned.RevocationReceipt))
        { return Result(state, id, DeletionConsumptionStatus.Conflict); }
        if (state.Operations.Count >= 10000 || authority is null || !await Budget.ReadAsync(token => authority.VerifyBlockedReplacementAsync(owned, token)).ConfigureAwait(false)) { return Unavailable(tenant, id); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var outcome = Result(next, id, DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise) with {
            ReceiptId = DeletionConsumptionIdentity.Digest(new[] { owned.OperationId, digest, "blocked-issued-replacement" }), BlockReason = DeletionConsumptionBlockReason.CapabilityKeyCompromise,
            BlockedKeyVersion = owned.Capability.CapabilityKeyVersion, RevocationRevision = keyBlock.Envelope.RevocationRevision };
        next = Replace(next, batch with { BlockedReplacement = owned, Outcome = outcome with { Status = DeletionConsumptionStatus.ConsumptionBlocked } });
        next = next with { Operations = state.Operations.Append(new DeletionConsumptionOperation(owned.OperationId, digest, outcome) { BlockedReplacementOriginal = owned }).ToArray() };
        await SaveAsync(next).ConfigureAwait(false);
        return await AdmitAsync(tenant, id, "ReconcileBlockedDeletionReplacement").ConfigureAwait(false) ? outcome : Unavailable(tenant, id);
    }
    private async Task<DeletionBlockedReplacementResult?> ReadBlockedReplacementEntryAsync(DeletionBatchCapabilityV1 capability)
    {
        ArgumentNullException.ThrowIfNull(capability); Check(capability.TenantId); _ = DeletionBatchCapabilityIdentity.SigningRequestId(capability);
        if (!await AdmitAsync(capability.TenantId, capability.BatchId, "ReadBlockedDeletionReplacement").ConfigureAwait(false)) { return null; }
        var state = await ReadAsync(capability.TenantId).ConfigureAwait(false);
        var operation = state.Operations.SingleOrDefault(o => o.BlockedReplacementOriginal?.Capability == capability);
        var phase = operation?.BlockedReplacementOriginal;
        if (phase is null || operation!.RequestDigest != DeletionConsumptionIdentity.Digest(phase)) { return null; }
        var final = await ReadAsync(capability.TenantId).ConfigureAwait(false);
        return operation is not null && DeletionConsumptionIdentity.Digest(final) == DeletionConsumptionIdentity.Digest(state)
            && await AdmitAsync(capability.TenantId, capability.BatchId, "ReadBlockedDeletionReplacement").ConfigureAwait(false) ? new(phase, operation.Outcome) : null;
    }
    private async Task<DeletionConsumptionOutcome> ActivateAsyncCoreAsync(DeletionReattestationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation); var owned = activation with { Replacement = await CaptureAsync(() => DeletionConsumptionIdentity.Capture(activation.Replacement)).ConfigureAwait(false) };
        string tenant = owned.Replacement.Capability.TenantId; string id = owned.Replacement.Capability.BatchId; Check(tenant);
        foreach (string value in new[] { owned.OperationId, owned.CompromiseBlockReceiptId, owned.GuardReplacementReceiptId }) { DeletionConsumptionIdentity.Text(value); }
        if (owned.ExpectedKeyBlockSetRevision < 0) { throw new ArgumentException("Invalid activation compare.", nameof(activation)); }
        if (!await AdmitAsync(tenant, id, "ActivateReattestedDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, id); }
        var state = await ReadAsync(tenant, true).ConfigureAwait(false); string digest = DeletionConsumptionIdentity.Digest(owned);
        var prior = state.Operations.SingleOrDefault(o => o.OperationId == owned.OperationId);
        if (prior is not null) { return prior.RequestDigest == digest ? prior.Outcome : Result(state, id, DeletionConsumptionStatus.Conflict); }
        if (state.Operations.Count >= 10000) { return Result(state, id, DeletionConsumptionStatus.Unavailable); }
        var batch = Find(state, id);
        if (batch is null || batch.Outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked
            || batch.Outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise
            || batch.Outcome.ReceiptId != owned.CompromiseBlockReceiptId || !DeletionConsumptionIdentity.SameBatch(batch.Original, owned.Replacement)
            || owned.Replacement.Capability.AttestationOrdinal != checked((batch.BlockedReplacement?.Capability.AttestationOrdinal ?? batch.Current.Capability.AttestationOrdinal) + 1)
            || owned.Replacement.Capability.CapabilityKeyVersion == (batch.BlockedReplacement?.Capability.CapabilityKeyVersion ?? batch.Current.Capability.CapabilityKeyVersion))
        { return Result(state, id, DeletionConsumptionStatus.Conflict); }
        if (authority is null || !await Budget.ReadAsync(token => authority.VerifyActivationAsync(owned, token)).ConfigureAwait(false)) { return Result(state, id, DeletionConsumptionStatus.Unavailable); }
        var replacementBlock = KeyBlock(state, owned.Replacement.Capability.CapabilityKeyVersion);
        if (replacementBlock is null && owned.ExpectedKeyBlockSetRevision != state.KeyBlockSetRevision) { return Result(state, id, DeletionConsumptionStatus.Conflict); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var outcome = Result(next, id, replacementBlock is null ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise)
            with { ReceiptId = DeletionConsumptionIdentity.Digest(new[] { owned.OperationId, digest, "activation" }),
                BlockReason = replacementBlock is null ? null : DeletionConsumptionBlockReason.CapabilityKeyCompromise,
                BlockedKeyVersion = replacementBlock?.Envelope.KeyVersion, RevocationRevision = replacementBlock?.Envelope.RevocationRevision };
        var durable = outcome with { Status = replacementBlock is null ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ConsumptionBlocked };
        next = Replace(next, batch with { Current = owned.Replacement, Outcome = durable, BlockedReplacement = null });
        next = next with { Operations = state.Operations.Append(new DeletionConsumptionOperation(owned.OperationId, digest, outcome) { ActivationOriginal = owned }).ToArray() };
        await SaveAsync(next).ConfigureAwait(false); return outcome;
    }
    private async Task<DeletionConsumptionOutcome> LookupEntryAsync(string tenantId, string batchId)
    {
        Check(tenantId);
        if (!(await AdmitAsync(tenantId, batchId, "LookupDeletionBatch").ConfigureAwait(false))) { return Unavailable(tenantId, batchId); }
        var result = await LookupAsyncCoreAsync(tenantId, batchId).ConfigureAwait(false);
        if (!(await AdmitAsync(tenantId, batchId, "LookupDeletionBatch").ConfigureAwait(false))) { return Unavailable(tenantId, batchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> LookupAsyncCoreAsync(string tenantId, string batchId)
    {
        Check(tenantId); DeletionConsumptionIdentity.Text(batchId);
        if (!await AdmitAsync(tenantId, batchId, "LookupDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenantId, batchId); }
        var state = await ReadAsync(tenantId).ConfigureAwait(false); var batch = Find(state, batchId);
        return batch is null ? Result(state, batchId, DeletionConsumptionStatus.Unavailable)
            : batch.Outcome.Status == DeletionConsumptionStatus.ConsumptionReserved ? await RecoverAsync(state, batch, false).ConfigureAwait(false) : batch.Outcome;
    }
    private async Task<DeletionCapabilityRevocationReceipt?> LookupRevocationEntryAsync(DeletionCapabilityRevocationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope); Check(envelope.TenantId);
        if (!(await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "LookupDeletionCapabilityRevocation").ConfigureAwait(false))) { return null; }
        var result = await LookupRevocationAsyncCoreAsync(envelope).ConfigureAwait(false);
        if (!(await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "LookupDeletionCapabilityRevocation").ConfigureAwait(false))) { return null; }
        return result;
    }
    private async Task<DeletionCapabilityRevocationReceipt?> LookupRevocationAsyncCoreAsync(DeletionCapabilityRevocationEnvelope envelope)
    {
        DeletionConsumptionIdentity.Revocation(envelope); Check(envelope.TenantId);
        if (!await AdmitAsync(envelope.TenantId, envelope.EventIdentity, "LookupDeletionCapabilityRevocation").ConfigureAwait(false)) { return null; }
        var state = await ReadAsync(envelope.TenantId).ConfigureAwait(false);
        var receipt = state.Revocations.SingleOrDefault(r => r.Envelope.EventIdentity == envelope.EventIdentity);
        if (receipt is not null && receipt.Envelope != envelope) { throw new ArgumentException("Changed revocation evidence.", nameof(envelope)); } return receipt;
    }
    private async Task<bool> AdmitAsync(string tenant, string identity, string operation) => authority is not null
        && await Budget.ReadAsync(token => authority.AuthorizeOperationAsync(tenant, identity, operation, token)).ConfigureAwait(false);
    private static DeletionConsumptionOutcome Unavailable(string tenant, string id) => new(tenant, id, DeletionConsumptionStatus.Unavailable, 0, 0, null, null, null, null, []);
    private async Task<DeletionConsumptionOutcome> RecoverAsync(DeletionConsumptionLedger state, DeletionConsumptionBatch batch, bool first)
    {
        if (_entryReservation.Value is { } holder) { holder.Reservation = batch.Outcome; }
        if (provider is null) { return batch.Outcome; }
        var deadline = Budget;
        DeletionManifestProviderResult? result;
        try
        {
            result = await ReadPhysicalAsync(first).ConfigureAwait(false);
            if (!first && result?.State == DeletionManifestProviderState.NotStarted)
            { result = await ReadPhysicalAsync(true).ConfigureAwait(false); }
        }
        catch (Exception) { return batch.Outcome; }
        if (result?.State != DeletionManifestProviderState.Consumed) { return batch.Outcome; }
        var receipts = result.TargetReceipts;
        Task<DeletionManifestProviderResult?> ReadPhysicalAsync(bool consume) => deadline.ReadAsync(async token =>
        {
            var supplied = await (consume ? provider.ConsumeAsync(batch.Current, batch.Outcome.ReceiptId!, token)
                : provider.LookupAsync(batch.Current, batch.Outcome.ReceiptId!, token)).ConfigureAwait(false);
            // Provider-owned Count/traversal is part of the same physical deadline. Abandoned capture touches local material only.
            deadline.ThrowIfCancellationRequested();
            if (!ExactProvider(batch, supplied) || supplied.TargetReceipts is null) { return null; }
            int count = supplied.TargetReceipts.Count;
            if (supplied.State == DeletionManifestProviderState.NotStarted)
            { deadline.ThrowIfCancellationRequested(); return count == 0 ? supplied with { TargetReceipts = Array.Empty<DeletionTargetReceipt>() } : null; }
            if (supplied.State != DeletionManifestProviderState.Consumed || count != batch.Current.Targets.Count) { return null; }
            var owned = new List<DeletionTargetReceipt>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in supplied.TargetReceipts)
            {
                deadline.ThrowIfCancellationRequested();
                if (owned.Count >= batch.Current.Targets.Count || receipt is null || receipt.Target != batch.Current.Targets[owned.Count]
                    || receipt.OriginalBatchId != batch.Current.Capability.BatchId) { return null; }
                try { DeletionConsumptionIdentity.Text(receipt.ReceiptId); } catch (ArgumentException) { return null; }
                if (!ids.Add(receipt.ReceiptId)) { return null; }
                owned.Add(receipt);
            }
            deadline.ThrowIfCancellationRequested();
            return owned.Count == batch.Current.Targets.Count ? supplied with { TargetReceipts = owned.AsReadOnly() } : null;
        });
        var next = state with { Revision = checked(state.Revision + 1) };
        var consumed = batch with { Outcome = Result(next, batch.Current.Capability.BatchId, DeletionConsumptionStatus.Consumed)
            with { ReceiptId = batch.Outcome.ReceiptId, TargetReceipts = receipts } };
        await SaveAsync(Replace(next, consumed)).ConfigureAwait(false); return consumed.Outcome;
    }
    private static bool ExactProvider(DeletionConsumptionBatch batch, DeletionManifestProviderResult? result) => result is not null
        && result.TenantId == batch.Current.Capability.TenantId && result.BatchId == batch.Current.Capability.BatchId
        && result.ReservationReceiptId == batch.Outcome.ReceiptId;
    private void Check(string tenant)
    { DeletionConsumptionIdentity.Text(tenant); if (Host.Id.GetId() != GetActorId(tenant)) { throw new ArgumentException("Protection tenant scope mismatch."); } }
    private async Task<DeletionConsumptionLedger> ReadAsync(string tenant, bool recoverAdmittedOriginal = false)
    {
        await StateIoAsync(() => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var value = await StateIoAsync(() => StateManager.TryGetStateAsync<DeletionConsumptionLedger>(StateKey)).ConfigureAwait(false);
        var raw = value.HasValue ? value.Value : new DeletionConsumptionLedger(tenant, 0, 0, [], [], []);
        if (authority is null) { throw new InvalidOperationException("Independent protection authority is absent."); }
        var captured = await CaptureAsync(() => CaptureLedger(raw, tenant)).ConfigureAwait(false);
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, captured, await ReadPendingAsync().ConfigureAwait(false),
            next => CaptureLedger(next, tenant), next => Budget.ReadAsync(token => authority.ValidateStateAsync(tenant, next.Revision, DeletionConsumptionIdentity.Digest(next), token)), new DeadlineDeletionConsumptionAuthority(authority, Budget),
            PersistTargetAsync, recoverAdmittedOriginal).ConfigureAwait(false);
    }
    private static DeletionConsumptionLedger CaptureLedger(DeletionConsumptionLedger state, string tenant)
    {
        if (state.TenantId == tenant && state.Revision == 0 && state.KeyBlockSetRevision == 0 && state.Batches.Count == 0 && state.Revocations.Count == 0 && state.Operations.Count == 0) { return state; }
        if (state.TenantId != tenant || state.Revision <= 0 || state.KeyBlockSetRevision < 0 || state.Batches is null || state.Revocations is null || state.Operations is null || state.Batches.Count > 1000 || state.Revocations.Count > 10000 || state.Operations.Count > 10000
            || state.KeyBlockSetRevision != state.Revocations.Count)
        { throw new InvalidOperationException("Malformed durable protection ledger."); }
        var batches = state.Batches.Select(b => b with { Original = DeletionConsumptionIdentity.Capture(b.Original), Current = DeletionConsumptionIdentity.Capture(b.Current),
            Outcome = b.Outcome with { TargetReceipts = Array.AsReadOnly(b.Outcome.TargetReceipts.ToArray()) }, BlockedReplacement = b.BlockedReplacement is null ? null : DeletionConsumptionIdentity.Capture(b.BlockedReplacement) }).ToArray();
        if (batches.Select(b => b.Current.Capability.BatchId).Distinct(StringComparer.Ordinal).Count() != batches.Length
            || batches.Any(b => b.Current.Capability.TenantId != tenant || !DeletionConsumptionIdentity.SameBatch(b.Original, b.Current)
                || b.Outcome.TenantId != tenant || b.Outcome.BatchId != b.Current.Capability.BatchId
                || b.Outcome.OwnerRevision > state.Revision || b.Outcome.KeyBlockSetRevision > state.KeyBlockSetRevision
                || b.Outcome.Status is not (DeletionConsumptionStatus.Unconsumed or DeletionConsumptionStatus.ConsumptionReserved
                    or DeletionConsumptionStatus.ConsumptionBlocked or DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch) || string.IsNullOrWhiteSpace(b.Outcome.ReceiptId)))
        { throw new InvalidOperationException("Malformed durable protection batch."); }
        foreach (var b in batches)
        {
            var outcome = b.Outcome; DeletionConsumptionIdentity.Text(outcome.ReceiptId!);
            if (b.BlockedReplacement is { } phase && (outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked
                || outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise || !DeletionConsumptionIdentity.SameBatch(b.Original, phase)
                || phase.Capability.AttestationOrdinal < b.Current.Capability.AttestationOrdinal
                || phase.Capability.AttestationOrdinal == b.Current.Capability.AttestationOrdinal && phase.Capability != b.Current.Capability
                || phase.ExpectedKeyBlockSetRevision > state.KeyBlockSetRevision
                || !state.Revocations.Any(r => DeletionConsumptionIdentity.Digest(r) == DeletionConsumptionIdentity.Digest(phase.RevocationReceipt))
                || !state.Operations.Any(o => o.OperationId == phase.OperationId && o.RequestDigest == DeletionConsumptionIdentity.Digest(phase)
                    && o.Outcome.ReceiptId == outcome.ReceiptId && o.Outcome.Status == DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise)))
            { throw new InvalidOperationException("Malformed retained blocked replacement."); }
            if (outcome.OwnerRevision <= 0 || outcome.KeyBlockSetRevision < 0 || b.Current.Capability.AttestationOrdinal < b.Original.Capability.AttestationOrdinal
                || outcome.Status is DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch && (outcome.TargetReceipts.Count != b.Current.Targets.Count
                    || outcome.TargetReceipts.Select(r => r.ReceiptId).Distinct(StringComparer.Ordinal).Count() != outcome.TargetReceipts.Count
                    || outcome.TargetReceipts.Where((r, i) => r is null || r.Target != b.Current.Targets[i] || (outcome.Status == DeletionConsumptionStatus.Consumed ? r.OriginalBatchId != b.Current.Capability.BatchId
                            : !batches.Any(original => original.Outcome.Status == DeletionConsumptionStatus.Consumed && original.Current.Capability.BatchId == r.OriginalBatchId
                                && original.Outcome.TargetReceipts.Contains(r)))
                        || string.IsNullOrWhiteSpace(r.ReceiptId)).Any())
                || outcome.Status is not (DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch) && outcome.TargetReceipts.Count != 0
                || outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked && (outcome.BlockReason is not null || outcome.BlockedKeyVersion is not null || outcome.RevocationRevision is not null)
                || outcome.Status == DeletionConsumptionStatus.ConsumptionBlocked && (outcome.BlockReason is not
                    (DeletionConsumptionBlockReason.AdmissionIntegrity or DeletionConsumptionBlockReason.CapabilityKeyCompromise)
                    || outcome.BlockReason == DeletionConsumptionBlockReason.AdmissionIntegrity && (outcome.BlockedKeyVersion is not null || outcome.RevocationRevision is not null)
                    || outcome.BlockReason == DeletionConsumptionBlockReason.CapabilityKeyCompromise && (outcome.BlockedKeyVersion != (b.BlockedReplacement?.Capability.CapabilityKeyVersion ?? b.Current.Capability.CapabilityKeyVersion)
                        || outcome.RevocationRevision is null or <= 0)))
            { throw new InvalidOperationException("Malformed durable protection outcome."); }
        }
        foreach (var revocation in state.Revocations)
        {
            DeletionConsumptionIdentity.Revocation(revocation.Envelope);
            if (revocation.Envelope.TenantId != tenant || revocation.ReceiptId != DeletionConsumptionIdentity.Digest(revocation.Envelope)
                || revocation.OwnerRevision <= 0 || revocation.OwnerRevision > state.Revision || revocation.KeyBlockSetRevision <= 0
                || revocation.KeyBlockSetRevision > state.KeyBlockSetRevision || revocation.AffectedBatchIds is null || revocation.AffectedBatchIds.Count > 1000
                || revocation.AffectedBatchIds.Distinct(StringComparer.Ordinal).Count() != revocation.AffectedBatchIds.Count
                || revocation.AffectedBatchIds.Any(id => !batches.Any(b => b.Current.Capability.BatchId == id)))
            { throw new InvalidOperationException("Malformed durable protection revocation."); }
        }
        if (state.Revocations.Select(r => r.Envelope.EventIdentity).Distinct(StringComparer.Ordinal).Count() != state.Revocations.Count
            || state.Operations.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != state.Operations.Count
            || state.Operations.Any(o => o.Outcome.TenantId != tenant || !Enum.IsDefined(o.Outcome.Status)
                || o.Outcome.OwnerRevision <= 0 || o.Outcome.OwnerRevision > state.Revision || string.IsNullOrWhiteSpace(o.OperationId)
                || o.RequestDigest.Length != 64 || o.RequestDigest.Any(c => !char.IsAsciiHexDigit(c))))
        { throw new InvalidOperationException("Malformed durable protection operation."); }
        foreach (var operation in state.Operations.Where(o => o.ActivationOriginal is not null))
        {
            var activation = operation.ActivationOriginal!; var replacement = DeletionConsumptionIdentity.Capture(activation.Replacement);
            var batch = batches.SingleOrDefault(b => b.Current.Capability.BatchId == replacement.Capability.BatchId);
            if (batch is null || !DeletionConsumptionIdentity.SameBatch(batch.Original, replacement) || replacement.Capability.TenantId != tenant
                || activation.OperationId != operation.OperationId || operation.RequestDigest != DeletionConsumptionIdentity.Digest(activation)
                || operation.Outcome.BatchId != replacement.Capability.BatchId || operation.Outcome.Status is not (DeletionConsumptionStatus.Unconsumed or DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise)
                || operation.Outcome.ReceiptId != DeletionConsumptionIdentity.Digest(new[] { activation.OperationId, operation.RequestDigest, "activation" })
                || activation.ExpectedKeyBlockSetRevision < 0 || activation.ExpectedKeyBlockSetRevision > state.KeyBlockSetRevision
                || string.IsNullOrWhiteSpace(activation.CompromiseBlockReceiptId) || string.IsNullOrWhiteSpace(activation.GuardReplacementReceiptId))
            { throw new InvalidOperationException("Malformed retained activation original."); }
        }
        foreach (var operation in state.Operations.Where(o => o.BlockedReplacementOriginal is not null))
        {
            var phase = DeletionConsumptionIdentity.Capture(operation.BlockedReplacementOriginal!); var batch = batches.SingleOrDefault(b => b.Current.Capability.BatchId == phase.Capability.BatchId);
            if (batch is null || !DeletionConsumptionIdentity.SameBatch(batch.Original, phase) || phase.Capability.TenantId != tenant
                || operation.OperationId != phase.OperationId || operation.RequestDigest != DeletionConsumptionIdentity.Digest(phase)
                || operation.Outcome.Status != DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise || operation.Outcome.BatchId != phase.Capability.BatchId
                || operation.Outcome.KeyBlockSetRevision != phase.ExpectedKeyBlockSetRevision || operation.Outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise
                || operation.Outcome.BlockedKeyVersion != phase.Capability.CapabilityKeyVersion || operation.Outcome.RevocationRevision != phase.RevocationReceipt.Envelope.RevocationRevision
                || operation.Outcome.ReceiptId != DeletionConsumptionIdentity.Digest(new[] { phase.OperationId, operation.RequestDigest, "blocked-issued-replacement" })
                || operation.Outcome.TargetReceipts.Count != 0 || !state.Revocations.Any(r => DeletionConsumptionIdentity.Digest(r) == DeletionConsumptionIdentity.Digest(phase.RevocationReceipt)))
            { throw new InvalidOperationException("Malformed retained reconciliation original."); }
        }
        var owned = state with { Batches = Array.AsReadOnly(batches), Revocations = Array.AsReadOnly(state.Revocations.Select(r => r with {
            AffectedBatchIds = Array.AsReadOnly(r.AffectedBatchIds.ToArray()) }).ToArray()), Operations = Array.AsReadOnly(state.Operations.Select(o => o with { BlockedReplacementOriginal = o.BlockedReplacementOriginal is null ? null : DeletionConsumptionIdentity.Capture(o.BlockedReplacementOriginal),
            ActivationOriginal = o.ActivationOriginal is null ? null : o.ActivationOriginal with { Replacement = DeletionConsumptionIdentity.Capture(o.ActivationOriginal.Replacement) } }).ToArray()) };
        return owned;
    }
    private async Task SaveAsync(DeletionConsumptionLedger state)
    {
        // Defensive write bound as well as per-operation denial: never persist a state the reader cannot release.
        if (state.Batches.Count > 1000 || state.Operations.Count > 10000 || state.Revocations.Count > 10000) { throw new InvalidOperationException("Protection ledger write bound exceeded."); }
        if (authority is null) { throw new InvalidOperationException("Independent protection authority is absent."); }
        var previous = await ReadAsync(state.TenantId).ConfigureAwait(false);
        if (previous.Revision != state.Revision - 1) { throw new InvalidOperationException("Protection comparison changed."); }
        var owned = await CaptureAsync(() => CaptureLedger(state, state.TenantId)).ConfigureAwait(false);
        var pending = RecoverableAnchoredState.Prepare(PendingScope, previous.Revision, owned.Revision, previous, owned);
        if (!await RecoverableAnchoredState.CommitAsync(pending, new DeadlineDeletionConsumptionAuthority(authority, Budget), ReadPendingAsync, PersistPendingAsync).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent protection transition compare failed."); }
        var persisted = await ReadAsync(state.TenantId).ConfigureAwait(false);
        if (DeletionConsumptionIdentity.Digest(persisted) != DeletionConsumptionIdentity.Digest(state)) { throw new InvalidOperationException("Protection outcome not confirmed durable."); }
    }

    private string PendingScope => Host.Id.GetId() + "|" + StateKey;
    private const string PendingKey = StateKey + "-pending-transition-v1";
    private async Task<AnchoredStateTransition?> ReadPendingAsync()
    {
        await StateIoAsync(() => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var pending = await StateIoAsync(() => StateManager.TryGetStateAsync<AnchoredStateTransition>(PendingKey)).ConfigureAwait(false);
        return pending.HasValue ? pending.Value : null;
    }
    private async Task PersistPendingAsync(AnchoredStateTransition pending)
    {
        await StateIoAsync(() => StateManager.SetStateAsync(PendingKey, pending)).ConfigureAwait(false);
        await StateIoAsync(() => StateManager.SaveStateAsync()).ConfigureAwait(false);
    }
    private async Task<DeletionConsumptionLedger> PersistTargetAsync(DeletionConsumptionLedger next)
    {
        await StateIoAsync(() => StateManager.SetStateAsync(StateKey, next)).ConfigureAwait(false);
        _ = await StateIoAsync(() => StateManager.TryRemoveStateAsync(PendingKey)).ConfigureAwait(false);
        await StateIoAsync(() => StateManager.SaveStateAsync()).ConfigureAwait(false);
        await StateIoAsync(() => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var confirmed = await StateIoAsync(() => StateManager.TryGetStateAsync<DeletionConsumptionLedger>(StateKey)).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private async Task<DeletionConsumptionOutcome> RetainCoveredAsync(DeletionConsumptionLedger state, DeletionBatchConsumptionRequest request,
        IReadOnlyList<DeletionTargetReceipt> originals)
    {
        var existing = Find(state, request.Capability.BatchId);
        if (existing is not null && !DeletionConsumptionIdentity.Same(existing.Current, request)) { return Result(state, request.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (existing?.Outcome.Status == DeletionConsumptionStatus.AlreadyDestroyedByBatch) { return existing.Outcome; }
        if (existing is null && state.Batches.Count >= 1000) { return Result(state, request.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        if (originals.Count != request.Targets.Count || !originals.Select(r => r.Target).SequenceEqual(request.Targets)
            || originals.Select(r => r.ReceiptId).Distinct(StringComparer.Ordinal).Count() != originals.Count)
        { throw new InvalidOperationException("Incomplete exact original destruction coverage."); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var result = Result(next, request.Capability.BatchId, DeletionConsumptionStatus.AlreadyDestroyedByBatch) with {
            ReceiptId = DeletionConsumptionIdentity.Digest(new object[] { "already-destroyed", request, originals, next.Revision }),
            TargetReceipts = Array.AsReadOnly(originals.ToArray()) };
        var retained = new DeletionConsumptionBatch(existing?.Original ?? request, request, result);
        next = existing is null ? next with { Batches = next.Batches.Append(retained).ToArray() } : Replace(next, retained);
        await SaveAsync(next).ConfigureAwait(false); return result;
    }
    private static DeletionConsumptionBatch? Find(DeletionConsumptionLedger state, string id) => state.Batches.SingleOrDefault(b => b.Current.Capability.BatchId == id);
    private static DeletionCapabilityRevocationReceipt? KeyBlock(DeletionConsumptionLedger state, string key) => state.Revocations
        .Where(r => r.Envelope.KeyVersion == key).OrderByDescending(r => r.Envelope.RevocationRevision).FirstOrDefault();
    private static DeletionConsumptionLedger Replace(DeletionConsumptionLedger state, DeletionConsumptionBatch batch) => state with {
        Batches = state.Batches.Select(b => b.Current.Capability.BatchId == batch.Current.Capability.BatchId ? batch : b).ToArray() };
    private static IReadOnlyList<DeletionTargetReceipt>? FindDestroyed(DeletionConsumptionLedger state, DeletionBatchConsumptionRequest request)
    {
        var receipts = new List<DeletionTargetReceipt>();
        foreach (var target in request.Targets)
        {
            var receipt = state.Batches.Where(b => b.Outcome.Status == DeletionConsumptionStatus.Consumed).SelectMany(b => b.Outcome.TargetReceipts)
                .SingleOrDefault(r => r.Target == target);
            if (receipt is null) { return null; } receipts.Add(receipt);
        }
        return Array.AsReadOnly(receipts.ToArray());
    }
    private static DeletionConsumptionBatch? FindCoveredReservation(DeletionConsumptionLedger state, DeletionBatchConsumptionRequest request) => state.Batches
        .SingleOrDefault(b => b.Outcome.Status == DeletionConsumptionStatus.ConsumptionReserved && request.Targets.All(b.Current.Targets.Contains));
    private static bool OverlapsCoveredOrReserved(DeletionConsumptionLedger state, DeletionBatchConsumptionRequest request) => state.Batches
        .Where(b => b.Current.Capability.BatchId != request.Capability.BatchId && b.Outcome.Status is
            DeletionConsumptionStatus.ConsumptionReserved or DeletionConsumptionStatus.Consumed)
        .Any(b => b.Current.Targets.Any(request.Targets.Contains));
    private static string Receipt(string kind, DeletionBatchConsumptionRequest request, long revision) => DeletionConsumptionIdentity.Digest(new[] {
        kind, DeletionConsumptionIdentity.Digest(request), revision.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    private static DeletionConsumptionOutcome Result(DeletionConsumptionLedger state, string id, DeletionConsumptionStatus status) => new(state.TenantId, id,
        status, state.Revision, state.KeyBlockSetRevision, null, null, null, null, []);
}
