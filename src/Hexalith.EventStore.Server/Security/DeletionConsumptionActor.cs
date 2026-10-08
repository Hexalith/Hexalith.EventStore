using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual DAPR durable same-tenant block/reserve/activation owner. Registration and independent authority/backend remain disabled by default.</summary>
/// <remarks>Production must use non-reentrant actor turns and a qualified durable conditional state store. Provider absence is not destruction evidence.</remarks>
/// <param name="host">Exact private tenant actor.</param>
/// <param name="authority">Independently authenticated signature/online guard and registrar verifier.</param>
/// <param name="provider">Qualified physical all-or-none exact reservation backend.</param>
public sealed class DeletionConsumptionActor(ActorHost host, IDeletionConsumptionAuthority? authority = null,
    IAtomicDeletionManifestProvider? provider = null) : Actor(host), IDeletionConsumptionActor
{
    private const string StateKey = "deletion-consumption-v23";
    /// <summary>Gets the exact private actor registration name.</summary>
    public const string ActorTypeName = "DeletionConsumptionActor";
    /// <summary>Gets the exact same-tenant actor shared by reserve, block, revocation and activation.</summary>
    public static string GetActorId(string tenantId) => new AggregateIdentity(tenantId, "protection", "deletion-consumption-v23").ActorId;
    /// <inheritdoc/>
    public async Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.Capability.TenantId);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        var result = await RegisterAsyncCoreAsync(request).ConfigureAwait(false);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> RegisterAsyncCoreAsync(DeletionBatchConsumptionRequest request)
    {
        var owned = DeletionConsumptionIdentity.Capture(request); string tenant = owned.Capability.TenantId; Check(tenant);
        if (!await AdmitAsync(tenant, owned.Capability.BatchId, "RegisterDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, owned.Capability.BatchId); }
        var state = await ReadAsync(tenant).ConfigureAwait(false); var batch = Find(state, owned.Capability.BatchId);
        if (batch is not null) { return DeletionConsumptionIdentity.Same(batch.Current, owned) ? batch.Outcome : Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (authority is null || provider is null || !await authority.VerifyDispatchAsync(owned).ConfigureAwait(false))
        { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        var reservation = FindCoveredReservation(state, owned);
        if (reservation is not null)
        {
            var original = await RecoverAsync(state, reservation, false).ConfigureAwait(false);
            return original.Status == DeletionConsumptionStatus.Consumed ? Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.AlreadyDestroyedByBatch)
                with { OwnerRevision = original.OwnerRevision, TargetReceipts = Array.AsReadOnly(original.TargetReceipts.Where(r => owned.Targets.Contains(r.Target)).ToArray()) } : original;
        }
        var prior = FindDestroyed(state, owned);
        if (prior is not null) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.AlreadyDestroyedByBatch) with { TargetReceipts = prior }; }
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
    /// <inheritdoc/>
    public async Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.Capability.TenantId);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        var result = await ReserveAndConsumeAsyncCoreAsync(request).ConfigureAwait(false);
        if (!(await AdmitAsync(request.Capability.TenantId, request.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false))) { return Unavailable(request.Capability.TenantId, request.Capability.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> ReserveAndConsumeAsyncCoreAsync(DeletionBatchConsumptionRequest request)
    {
        var owned = DeletionConsumptionIdentity.Capture(request); string tenant = owned.Capability.TenantId; Check(tenant);
        if (!await AdmitAsync(tenant, owned.Capability.BatchId, "ReserveAndConsumeDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, owned.Capability.BatchId); }
        var state = await ReadAsync(tenant).ConfigureAwait(false); var batch = Find(state, owned.Capability.BatchId);
        if (batch is null) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        if (!DeletionConsumptionIdentity.Same(batch.Current, owned)) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        if (batch.Outcome.Status == DeletionConsumptionStatus.ConsumptionReserved) { return await RecoverAsync(state, batch, false).ConfigureAwait(false); }
        if (batch.Outcome.Status != DeletionConsumptionStatus.Unconsumed) { return batch.Outcome; }
        if (authority is null || provider is null || !await authority.VerifyDispatchAsync(owned).ConfigureAwait(false))
        { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Unavailable); }
        var keyBlock = KeyBlock(state, owned.Capability.CapabilityKeyVersion);
        if (keyBlock is not null) { throw new InvalidOperationException("Registered unconsumed batch bypassed durable key block."); }
        var covered = FindDestroyed(state, owned);
        if (covered is not null) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.AlreadyDestroyedByBatch) with { TargetReceipts = covered }; }
        if (OverlapsCoveredOrReserved(state, owned)) { return Result(state, owned.Capability.BatchId, DeletionConsumptionStatus.Conflict); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var reserved = batch with { Outcome = Result(next, owned.Capability.BatchId, DeletionConsumptionStatus.ConsumptionReserved)
            with { ReceiptId = Receipt("reserve", owned, next.Revision) } };
        next = Replace(next, reserved); await SaveAsync(next).ConfigureAwait(false);
        return await RecoverAsync(next, reserved, true).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<DeletionConsumptionOutcome> BlockAsync(DeletionBatchBlockRequest request)
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
        var state = await ReadAsync(request.TenantId).ConfigureAwait(false); string digest = DeletionConsumptionIdentity.Digest(request);
        var prior = state.Operations.SingleOrDefault(o => o.OperationId == request.OperationId);
        if (prior is not null) { return prior.RequestDigest == digest ? prior.Outcome : Result(state, request.BatchId, DeletionConsumptionStatus.Conflict); }
        if (state.Operations.Count >= 10000) { return Result(state, request.BatchId, DeletionConsumptionStatus.Unavailable); }
        var batch = Find(state, request.BatchId);
        if (batch is null || authority is null || !await authority.VerifyAdmissionBlockAsync(request).ConfigureAwait(false))
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
    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> RegisterRevocationAsync(DeletionCapabilityRevocationEnvelope envelope)
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
        var state = await ReadAsync(envelope.TenantId).ConfigureAwait(false);
        var prior = state.Revocations.SingleOrDefault(r => r.Envelope.EventIdentity == envelope.EventIdentity);
        if (prior is not null) { if (prior.Envelope != envelope) { throw new ArgumentException("Changed revocation event identity.", nameof(envelope)); } return prior; }
        if (state.Revocations.Count >= 10000) { return null; }
        if (authority is null || !await authority.VerifyRevocationAsync(envelope).ConfigureAwait(false)) { return null; }
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
    /// <inheritdoc/>
    public async Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation); var c = activation.Replacement.Capability; Check(c.TenantId);
        if (!(await AdmitAsync(c.TenantId, c.BatchId, "ActivateReattestedDeletionBatch").ConfigureAwait(false))) { return Unavailable(c.TenantId, c.BatchId); }
        var result = await ActivateAsyncCoreAsync(activation).ConfigureAwait(false);
        if (!(await AdmitAsync(c.TenantId, c.BatchId, "ActivateReattestedDeletionBatch").ConfigureAwait(false))) { return Unavailable(c.TenantId, c.BatchId); }
        return result;
    }
    private async Task<DeletionConsumptionOutcome> ActivateAsyncCoreAsync(DeletionReattestationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation); var owned = activation with { Replacement = DeletionConsumptionIdentity.Capture(activation.Replacement) };
        string tenant = owned.Replacement.Capability.TenantId; string id = owned.Replacement.Capability.BatchId; Check(tenant);
        foreach (string value in new[] { owned.OperationId, owned.CompromiseBlockReceiptId, owned.GuardReplacementReceiptId }) { DeletionConsumptionIdentity.Text(value); }
        if (owned.ExpectedKeyBlockSetRevision < 0) { throw new ArgumentException("Invalid activation compare.", nameof(activation)); }
        if (!await AdmitAsync(tenant, id, "ActivateReattestedDeletionBatch").ConfigureAwait(false)) { return Unavailable(tenant, id); }
        var state = await ReadAsync(tenant).ConfigureAwait(false); string digest = DeletionConsumptionIdentity.Digest(owned);
        var prior = state.Operations.SingleOrDefault(o => o.OperationId == owned.OperationId);
        if (prior is not null) { return prior.RequestDigest == digest ? prior.Outcome : Result(state, id, DeletionConsumptionStatus.Conflict); }
        if (state.Operations.Count >= 10000) { return Result(state, id, DeletionConsumptionStatus.Unavailable); }
        var batch = Find(state, id);
        if (batch is null || batch.Outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked
            || batch.Outcome.BlockReason != DeletionConsumptionBlockReason.CapabilityKeyCompromise
            || batch.Outcome.ReceiptId != owned.CompromiseBlockReceiptId || !DeletionConsumptionIdentity.SameBatch(batch.Original, owned.Replacement)
            || owned.Replacement.Capability.AttestationOrdinal != checked(batch.Current.Capability.AttestationOrdinal + 1)
            || owned.Replacement.Capability.CapabilityKeyVersion == batch.Current.Capability.CapabilityKeyVersion)
        { return Result(state, id, DeletionConsumptionStatus.Conflict); }
        if (authority is null || !await authority.VerifyActivationAsync(owned).ConfigureAwait(false)) { return Result(state, id, DeletionConsumptionStatus.Unavailable); }
        var replacementBlock = KeyBlock(state, owned.Replacement.Capability.CapabilityKeyVersion);
        if (replacementBlock is null && owned.ExpectedKeyBlockSetRevision != state.KeyBlockSetRevision) { return Result(state, id, DeletionConsumptionStatus.Conflict); }
        var next = state with { Revision = checked(state.Revision + 1) };
        var outcome = Result(next, id, replacementBlock is null ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise)
            with { ReceiptId = DeletionConsumptionIdentity.Digest(new[] { owned.OperationId, digest, "activation" }),
                BlockReason = replacementBlock is null ? null : DeletionConsumptionBlockReason.CapabilityKeyCompromise,
                BlockedKeyVersion = replacementBlock?.Envelope.KeyVersion, RevocationRevision = replacementBlock?.Envelope.RevocationRevision };
        var durable = outcome with { Status = replacementBlock is null ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ConsumptionBlocked };
        next = Replace(next, batch with { Current = owned.Replacement, Outcome = durable });
        next = next with { Operations = state.Operations.Append(new DeletionConsumptionOperation(owned.OperationId, digest, outcome)).ToArray() };
        await SaveAsync(next).ConfigureAwait(false); return outcome;
    }
    /// <inheritdoc/>
    public async Task<DeletionConsumptionOutcome> LookupAsync(string tenantId, string batchId)
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
    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> LookupRevocationAsync(DeletionCapabilityRevocationEnvelope envelope)
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
        && await authority.AuthorizeOperationAsync(tenant, identity, operation).ConfigureAwait(false);
    private static DeletionConsumptionOutcome Unavailable(string tenant, string id) => new(tenant, id, DeletionConsumptionStatus.Unavailable, 0, 0, null, null, null, null, []);
    private async Task<DeletionConsumptionOutcome> RecoverAsync(DeletionConsumptionLedger state, DeletionConsumptionBatch batch, bool first)
    {
        if (provider is null) { return batch.Outcome; }
        DeletionManifestProviderResult result;
        try
        {
            result = first ? await provider.ConsumeAsync(batch.Current, batch.Outcome.ReceiptId!).ConfigureAwait(false)
                : await provider.LookupAsync(batch.Current, batch.Outcome.ReceiptId!).ConfigureAwait(false);
            if (!first && ExactProvider(batch, result) && result.State == DeletionManifestProviderState.NotStarted && result.TargetReceipts is { Count: 0 })
            { result = await provider.ConsumeAsync(batch.Current, batch.Outcome.ReceiptId!).ConfigureAwait(false); }
        }
        catch (Exception) { return batch.Outcome; }
        if (!ExactProvider(batch, result) || result.State != DeletionManifestProviderState.Consumed || result.TargetReceipts is null
            || result.TargetReceipts.Count != batch.Current.Targets.Count) { return batch.Outcome; }
        var receipts = new List<DeletionTargetReceipt>();
        foreach (var receipt in result.TargetReceipts)
        {
            if (receipts.Count >= batch.Current.Targets.Count || receipt is null || receipt.Target != batch.Current.Targets[receipts.Count]
                || receipt.OriginalBatchId != batch.Current.Capability.BatchId) { return batch.Outcome; }
            try { DeletionConsumptionIdentity.Text(receipt.ReceiptId); } catch (ArgumentException) { return batch.Outcome; }
            receipts.Add(receipt);
        }
        if (receipts.Count != batch.Current.Targets.Count) { return batch.Outcome; }
        var next = state with { Revision = checked(state.Revision + 1) };
        var consumed = batch with { Outcome = Result(next, batch.Current.Capability.BatchId, DeletionConsumptionStatus.Consumed)
            with { ReceiptId = batch.Outcome.ReceiptId, TargetReceipts = Array.AsReadOnly(receipts.ToArray()) } };
        await SaveAsync(Replace(next, consumed)).ConfigureAwait(false); return consumed.Outcome;
    }
    private static bool ExactProvider(DeletionConsumptionBatch batch, DeletionManifestProviderResult? result) => result is not null
        && result.TenantId == batch.Current.Capability.TenantId && result.BatchId == batch.Current.Capability.BatchId
        && result.ReservationReceiptId == batch.Outcome.ReceiptId;
    private void Check(string tenant)
    { DeletionConsumptionIdentity.Text(tenant); if (Host.Id.GetId() != GetActorId(tenant)) { throw new ArgumentException("Protection tenant scope mismatch."); } }
    private async Task<DeletionConsumptionLedger> ReadAsync(string tenant)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var value = await StateManager.TryGetStateAsync<DeletionConsumptionLedger>(StateKey).ConfigureAwait(false);
        if (!value.HasValue)
        {
            var initial = new DeletionConsumptionLedger(tenant, 0, 0, [], [], []);
            if (authority is null || !await authority.ValidateStateAsync(tenant, 0, DeletionConsumptionIdentity.Digest(initial)).ConfigureAwait(false))
            { throw new InvalidOperationException("Independent protection state anchor is absent or stale."); }
            return initial;
        }
        var state = value.Value;
        if (state.TenantId != tenant || state.Revision <= 0 || state.KeyBlockSetRevision < 0 || state.Batches is null || state.Revocations is null || state.Operations is null || state.Batches.Count > 1000 || state.Revocations.Count > 10000 || state.Operations.Count > 10000
            || state.KeyBlockSetRevision != state.Revocations.Count)
        { throw new InvalidOperationException("Malformed durable protection ledger."); }
        var batches = state.Batches.Select(b => b with { Original = DeletionConsumptionIdentity.Capture(b.Original), Current = DeletionConsumptionIdentity.Capture(b.Current),
            Outcome = b.Outcome with { TargetReceipts = Array.AsReadOnly(b.Outcome.TargetReceipts.ToArray()) } }).ToArray();
        if (batches.Select(b => b.Current.Capability.BatchId).Distinct(StringComparer.Ordinal).Count() != batches.Length
            || batches.Any(b => b.Current.Capability.TenantId != tenant || !DeletionConsumptionIdentity.SameBatch(b.Original, b.Current)
                || b.Outcome.TenantId != tenant || b.Outcome.BatchId != b.Current.Capability.BatchId
                || b.Outcome.OwnerRevision > state.Revision || b.Outcome.KeyBlockSetRevision > state.KeyBlockSetRevision
                || b.Outcome.Status is not (DeletionConsumptionStatus.Unconsumed or DeletionConsumptionStatus.ConsumptionReserved
                    or DeletionConsumptionStatus.ConsumptionBlocked or DeletionConsumptionStatus.Consumed) || string.IsNullOrWhiteSpace(b.Outcome.ReceiptId)))
        { throw new InvalidOperationException("Malformed durable protection batch."); }
        foreach (var b in batches)
        {
            var outcome = b.Outcome; DeletionConsumptionIdentity.Text(outcome.ReceiptId!);
            if (outcome.OwnerRevision <= 0 || outcome.KeyBlockSetRevision < 0 || b.Current.Capability.AttestationOrdinal < b.Original.Capability.AttestationOrdinal
                || outcome.Status == DeletionConsumptionStatus.Consumed && (outcome.TargetReceipts.Count != b.Current.Targets.Count
                    || outcome.TargetReceipts.Where((r, i) => r is null || r.Target != b.Current.Targets[i] || r.OriginalBatchId != b.Current.Capability.BatchId
                        || string.IsNullOrWhiteSpace(r.ReceiptId)).Any())
                || outcome.Status != DeletionConsumptionStatus.Consumed && outcome.TargetReceipts.Count != 0
                || outcome.Status != DeletionConsumptionStatus.ConsumptionBlocked && (outcome.BlockReason is not null || outcome.BlockedKeyVersion is not null || outcome.RevocationRevision is not null)
                || outcome.Status == DeletionConsumptionStatus.ConsumptionBlocked && (outcome.BlockReason is not
                    (DeletionConsumptionBlockReason.AdmissionIntegrity or DeletionConsumptionBlockReason.CapabilityKeyCompromise)
                    || outcome.BlockReason == DeletionConsumptionBlockReason.AdmissionIntegrity && (outcome.BlockedKeyVersion is not null || outcome.RevocationRevision is not null)
                    || outcome.BlockReason == DeletionConsumptionBlockReason.CapabilityKeyCompromise && (outcome.BlockedKeyVersion != b.Current.Capability.CapabilityKeyVersion
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
        var owned = state with { Batches = Array.AsReadOnly(batches), Revocations = Array.AsReadOnly(state.Revocations.Select(r => r with {
            AffectedBatchIds = Array.AsReadOnly(r.AffectedBatchIds.ToArray()) }).ToArray()), Operations = Array.AsReadOnly(state.Operations.ToArray()) };
        if (authority is null || !await authority.ValidateStateAsync(tenant, owned.Revision, DeletionConsumptionIdentity.Digest(owned)).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent protection state anchor is absent or stale."); }
        return owned;
    }
    private async Task SaveAsync(DeletionConsumptionLedger state)
    {
        // Defensive write bound as well as per-operation denial: never persist a state the reader cannot release.
        if (state.Batches.Count > 1000 || state.Operations.Count > 10000 || state.Revocations.Count > 10000) { throw new InvalidOperationException("Protection ledger write bound exceeded."); }
        if (authority is null || !await authority.RecordRevisionAsync(state.TenantId, state.Revision - 1, state.Revision, DeletionConsumptionIdentity.Digest(state)).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent protection state anchor compare failed."); }
        await StateManager.SetStateAsync(StateKey, state).ConfigureAwait(false); await StateManager.SaveStateAsync().ConfigureAwait(false);
        var persisted = await ReadAsync(state.TenantId).ConfigureAwait(false);
        if (DeletionConsumptionIdentity.Digest(persisted) != DeletionConsumptionIdentity.Digest(state)) { throw new InvalidOperationException("Protection outcome not confirmed durable."); }
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
