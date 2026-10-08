using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Implements the dedicated replay actor's single-save operation/ledger/blob/final-result protocol.</summary>
/// <remarks>Only its owning actor may call it. No registration, SQL or cross-owner atomicity is supplied.</remarks>
internal sealed class DaprReplayOperationOwner : IAsyncDisposable
{
    private const string OperationKey = "logical-replay:operation:v1";
    private const string FinalKey = "logical-replay:final:v1";
    private const string FinalStateKey = "logical-replay:final-state:v1";
    private readonly RegisteredLogicalReplayBinding? _reconstruction;
    private readonly IActorStateManager _stateManager;
    private readonly string _tenant;
    private readonly string _operation;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly EventBufferBudget _bufferBudget;
    private DaprReplayPendingTransition? _pending;
    private readonly List<DaprLogicalResponseOwner> _cacheQuarantine = [];
    private bool _disposed;
    private DaprLogicalResponseOwner? _lastGoodDiagnostic;

    /// <summary>Gets the private last-good diagnostic sequence; failed page state never grants progress.</summary>
    internal long LastGoodDiagnosticSequence
    {
        get; private set;
    }

    /// <summary>Gets whether this owner admits canonical reconstruction participants.</summary>
    internal bool HasReconstructionBinding => _reconstruction is not null;

    /// <summary>Checks the exact supplied state route and callable identities before final response release.</summary>
    internal void RequireReconstructionCurrent(DaprLogicalSourceBinding source, CancellationToken token)
    {
        _reconstruction?.RequireSource(source, token);
    }

    /// <summary>Returns a charged private diagnostic copy without establishing committed continuation.</summary>
    internal DaprLogicalResponseOwner? CaptureLastGoodDiagnostic(EventBufferBudget budget)
        => _lastGoodDiagnostic is null ? null : DaprLogicalResponseOwner.Capture(_lastGoodDiagnostic.Bytes.Span, budget);

    private async Task<DaprLogicalResponseOwner> CreateInitialStateAsync(EventBufferBudget budget, Func<CancellationToken, Task> fence, CancellationToken token)
    {
        using ImmutablePayload initial = await _reconstruction!.CreateInitialAsync(budget, fence, token).ConfigureAwait(false);
        return CaptureState(initial, budget);
    }

    private static DaprLogicalResponseOwner CaptureState(IReadOnlyPayload payload, EventBufferBudget budget)
    {
        EventBufferReservation charge = budget.Reserve(checked(payload.Length + 256));
        byte[]? image = null;
        try
        {
            image = new byte[payload.Length];
            payload.CopyTo(0, image);
            return new DaprLogicalResponseOwner(image, charge);
        }
        catch
        {
            if (image is not null)
            {
                CryptographicOperations.ZeroMemory(image);
            }
            charge.Dispose();
            throw;
        }
    }

    private async Task PrepareReconstructionIntakeAsync(DaprReplayPreparedTransition prepared, DaprReplayOperationRecord prior,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        DaprLogicalResponseOwner? previous = await ReadStateAsync(prior.PageOrdinal, prior.CanonicalStateHash!, prepared.Budget, token).ConfigureAwait(false);
        if (previous is null)
        {
            throw new InvalidOperationException("ReplayRestartRequired: canonical predecessor is unavailable.");
        }

        EventBufferBudget? partition = null;
        PrivateLogicalReplayPage? intake = null;
        try
        {
            partition = prepared.Budget.CreatePartition(_reconstruction!.GetPreparationCapacity(prepared.Response.Bytes.Length, previous.Bytes.Length));
            intake = PrivateLogicalReplayPage.Capture(prepared.Response.Bytes.Span, binding, trust, prior.Accumulator,
                _reconstruction.Evolution, partition, token);
            prepared.AttachIntake(previous, intake, partition);
            previous = null;
            intake = null;
            partition = null;
        }
        finally
        {
            previous?.Dispose();
            intake?.Dispose();
            partition?.Dispose();
        }
    }

    private async Task AttachReconstructionAsync(DaprReplayPreparedTransition prepared, DaprReplayOperationRecord prior,
        DaprLogicalReplaySource actualSource, DaprLogicalSourceBinding source, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        EventBufferBudget budget = prepared.ReconstructionBudget!;
        DaprLogicalResponseOwner previous = prepared.PriorState!;
        _reconstruction!.RequireCurrent(token);
        DaprLogicalResponseOwner? successor = null;
        EventBufferReservation? staging = null;
        try
        {
            using EventBufferReservation priorCopy = budget.Reserve(previous.Bytes.Length);
            using var predecessor = new ImmutablePayload(previous.Bytes.ToArray(), previous.Bytes.Length, token);
            using PrivateLogicalReplayFold folded = await _reconstruction.FoldAsync(prepared.Intake!, predecessor, source, budget,
                cancellation => actualSource.RequireCurrentAsync(source, trust, cancellation), token).ConfigureAwait(false);
            if (folded.Failure is not null)
            {
                _lastGoodDiagnostic?.Dispose();
                _lastGoodDiagnostic = CaptureState(folded.State, budget);
                LastGoodDiagnosticSequence = folded.LastGoodSequence;
                throw new DaprLogicalReplayApplyException(folded.FailedSequence!.Value, folded.FailedEventType!, folded.Failure);
            }

            successor = CaptureState(folded.State, budget);
            staging = budget.Reserve(checked(successor.Bytes.Length * 6 + 4096));
            prepared.AttachState(previous, successor, staging);
            successor = null;
            staging = null;
        }
        finally
        {
            successor?.Dispose();
            staging?.Dispose();
        }
    }

    private async Task<DaprLogicalResponseOwner?> ReadStateAsync(long ordinal, byte[] expectedHash, EventBufferBudget budget, CancellationToken token)
    {
        ConditionalValue<byte[]> value = await _stateManager.TryGetStateAsync<byte[]>(StateKey(ordinal), token).ConfigureAwait(false);
        if (!value.HasValue || value.Value is null || value.Value.Length > 64 * 1024 * 1024
            || expectedHash.Length != 32 || !SHA256.HashData(value.Value).AsSpan().SequenceEqual(expectedHash))
        {
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            return null;
        }

        DaprLogicalResponseOwner owned = DaprLogicalResponseOwner.Capture(value.Value, budget);
        try
        {
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            return owned;
        }
        catch
        {
            _cacheQuarantine.Add(owned);
            throw;
        }
    }

    private async Task<bool> RequireStateAsync(long ordinal, byte[]? expectedHash, bool isFinal, CancellationToken token, bool permitLaterFinal = false)
    {
        ConditionalValue<byte[]> state = await _stateManager.TryGetStateAsync<byte[]>(StateKey(ordinal), token).ConfigureAwait(false);
        ConditionalValue<byte[]> final = await _stateManager.TryGetStateAsync<byte[]>(FinalStateKey, token).ConfigureAwait(false);
        if (_reconstruction is null)
        {
            return !state.HasValue && !final.HasValue && expectedHash is null;
        }

        if (expectedHash is not
            { Length: 32 }
        || !state.HasValue || state.Value is null || state.Value.Length > 64 * 1024 * 1024
            || !SHA256.HashData(state.Value).AsSpan().SequenceEqual(expectedHash))
        {
            return false;
        }

        return isFinal ? final.HasValue && final.Value is not null && final.Value.AsSpan().SequenceEqual(state.Value) : permitLaterFinal || !final.HasValue;
    }

    /// <summary>Fixes stable tenant/operation identity independently of any page request.</summary>
    internal DaprReplayOperationOwner(IActorStateManager stateManager, string tenantId, string operationId,
        int maximumBufferBytes = 128 * 1024 * 1024, RegisteredLogicalReplayBinding? reconstruction = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        var identity = new AggregateIdentity(tenantId, "replay-operation", operationId);
        if (identity.TenantId != tenantId)
        {
            throw new ArgumentException("Replay operation tenant must already be canonical.");
        }

        _tenant = tenantId;
        _operation = identity.AggregateId;
        _reconstruction = reconstruction;
        _bufferBudget = new EventBufferBudget(maximumBufferBytes);
    }

    /// <summary>Creates or fences a fixed-source operation; takeover requires the exact observed prior generation.</summary>
    /// <remarks>Proven incomplete admission establishes only the owner generation; ExecutePage admits the complete participant chain.</remarks>
    internal async Task<DaprReplayOperationResult> BeginAsync(DaprLogicalReplaySource source, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, string ownerId, long? takeoverGeneration, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!await ReleaseCacheQuarantineAsync().ConfigureAwait(false))
            {
                return new(DaprReplayCommitOutcome.Indeterminate, 0, null, false);
            }

            if (_pending is not null)
            {
                return new(DaprReplayCommitOutcome.Indeterminate, _pending.Prior.Generation, null, false);
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
            if (ownerId.Length > 256 || binding.Identity.TenantId != _tenant)
            {
                throw new ArgumentException("Replay owner or source tenant mismatch.");
            }

            _reconstruction?.RequireCurrent(cancellationToken);
            _reconstruction?.RequireSource(binding, cancellationToken);
            await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
            byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding);
            if (_beginPending is not null)
            {
                RequireOperation(_beginPending.Expected, sourceHash, trust.RegistryFingerprint.Span, binding.TargetSequence);
                if (_beginPending.Expected.OwnerId != ownerId)
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, _beginPending.Prior?.Generation ?? 0, null, false);
                }

                DaprReplayOperationResult pendingResult = await InspectBeginAsync(_beginPending, binding, trust).ConfigureAwait(false);
                if (pendingResult.Outcome == DaprReplayCommitOutcome.Indeterminate)
                {
                    return pendingResult;
                }

                _beginPending.Dispose();
                _beginPending = null;
                if (pendingResult.Outcome == DaprReplayCommitOutcome.Proven)
                {
                    return await AuthorizeCommittedResponseAsync(pendingResult, source, binding, trust, cancellationToken).ConfigureAwait(false);
                }
            }

            var budget = _bufferBudget;
            DaprReplayOperationRecord? prior = await ReadOperationAsync(cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                RequireOperation(prior, sourceHash, trust.RegistryFingerprint.Span, binding.TargetSequence);
                if (!await RequireCommittedChainAsync(prior, binding, trust, budget, cancellationToken).ConfigureAwait(false))
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                }

                if (prior.OwnerId == ownerId)
                {
                    var admitted = new DaprReplayOperationResult(prior.IsComplete && _reconstruction is null ? DaprReplayCommitOutcome.Indeterminate : DaprReplayCommitOutcome.Proven,
                        prior.Generation, null, _reconstruction is not null && prior.IsComplete);
                    return await AuthorizeCommittedResponseAsync(admitted, source, binding, trust, cancellationToken).ConfigureAwait(false);
                }

                if (prior.IsComplete || takeoverGeneration != prior.Generation || prior.Generation == long.MaxValue)
                {
                    throw new InvalidOperationException("ReplayOwnerFenceMismatch: takeover does not match the committed owner generation.");
                }
            }
            else
            {
                if (takeoverGeneration.HasValue)
                {
                    throw new InvalidOperationException("ReplayOwnerFenceMismatch: no prior owner exists.");
                }

                if (!await RequireInitialAbsenceAsync(cancellationToken).ConfigureAwait(false))
                {
                    await _stateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
                    return new(DaprReplayCommitOutcome.Indeterminate, 0, null, false);
                }
            }

            DaprLogicalResponseOwner? initial = prior is null && _reconstruction is not null
                ? await CreateInitialStateAsync(budget, token => source.RequireCurrentAsync(binding, trust, token), cancellationToken).ConfigureAwait(false) : null;
            EventBufferReservation staging;
            try
            {
                staging = budget.Reserve(initial is null ? 4096 : checked(initial.Bytes.Length * 4 + 4096));
            }
            catch
            {
                initial?.Dispose();
                throw;
            }

            var expected = prior is null
                ? new DaprReplayOperationRecord(_tenant, _operation, ownerId, 1, sourceHash, trust.RegistryFingerprint.ToArray(),
                    binding.TargetSequence, 0, 0, DaprLogicalClaimCodec.ComputeGenesis(sourceHash, trust.RegistryFingerprint), false)
                {
                    ReconstructionBindingHash = _reconstruction?.Fingerprint.ToArray(),
                    CanonicalStateHash = initial is null ? null : SHA256.HashData(initial.Bytes.Span)
                }
                : prior with
                {
                    OwnerId = ownerId,
                    Generation = prior.Generation + 1
                };
            byte[] participantDigest;
            try
            {
                participantDigest = await ComputeParticipantDigestAsync(expected, initial, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                initial?.Dispose();
                staging.Dispose();
                throw;
            }

            var prepared = new DaprReplayBeginTransition(prior, expected, initial, staging, budget, participantDigest);
            bool saveAttempted = false;
            try
            {
                await _stateManager.SetStateAsync(OperationKey, expected, cancellationToken).ConfigureAwait(false);
                if (initial is not null)
                {
                    await _stateManager.SetStateAsync(StateKey(0), initial.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                }

                _reconstruction?.RequireCurrent(cancellationToken);
                await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                saveAttempted = true;
                await _stateManager.SaveStateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (saveAttempted)
            {
                /* Exact fresh logical readback decides committed truth. */
            }
            catch
            {
                try
                {
                    await _stateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
                    prepared.Dispose();
                }
                catch
                {
                    _beginPending = prepared;
                }

                throw;
            }

            DaprReplayOperationResult result = await InspectBeginAsync(prepared, binding, trust).ConfigureAwait(false);
            if (result.Outcome == DaprReplayCommitOutcome.Indeterminate)
            {
                _beginPending = prepared;
            }
            else
            {
                prepared.Dispose();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return result with
                {
                    OriginatingCancellationObserved = true
                };
            }

            if (result.Outcome == DaprReplayCommitOutcome.Proven)
            {
                try
                {
                    _reconstruction?.RequireCurrent(cancellationToken);
                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return result with
                    {
                        ResponseUnavailable = true
                    };
                }
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private DaprReplayBeginTransition? _beginPending;

    private async Task<bool> RequireInitialAbsenceAsync(CancellationToken token)
    {
        ConditionalValue<DaprReplayPageLedger> ledger = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(1), token).ConfigureAwait(false);
        foreach (string key in new[] { ResponseKey(1), FinalKey, StateKey(0), StateKey(1), FinalStateKey })
        {
            ConditionalValue<byte[]> state = await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false);
            if (state.HasValue)
            {
                return false;
            }
        }

        return !ledger.HasValue;
    }

    private async Task<byte[]> ComputeParticipantDigestAsync(DaprReplayOperationRecord record,
        DaprLogicalResponseOwner? initialOverride, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("HX-EV-DAPR-BEGIN-PARTICIPANTS-1\0"u8);
        for (long ordinal = 0; ordinal <= record.PageOrdinal + 1; ordinal++)
        {
            if (ordinal > 0)
            {
                string ledgerKey = LedgerKey(ordinal);
                ConditionalValue<DaprReplayPageLedger> ledger = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(ledgerKey, token).ConfigureAwait(false);
                byte[] encoded = ledger.HasValue && ledger.Value is not null ? DaprReplayLedgerCodec.Encode(ledger.Value) : [];
                try
                {
                    AppendParticipant(hash, ledgerKey, ledger.HasValue, ledger.Value is not null, encoded);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encoded);
                }

                await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
                await AppendBytesParticipantAsync(hash, ResponseKey(ordinal), null, token).ConfigureAwait(false);
            }

            await AppendBytesParticipantAsync(hash, StateKey(ordinal), ordinal == 0 ? initialOverride : null, token).ConfigureAwait(false);
        }

        await AppendBytesParticipantAsync(hash, FinalKey, null, token).ConfigureAwait(false);
        await AppendBytesParticipantAsync(hash, FinalStateKey, null, token).ConfigureAwait(false);
        return hash.GetHashAndReset();
    }

    private async Task AppendBytesParticipantAsync(IncrementalHash hash, string key, DaprLogicalResponseOwner? valueOverride, CancellationToken token)
    {
        if (valueOverride is not null)
        {
            AppendParticipant(hash, key, true, true, valueOverride.Bytes.Span);
            return;
        }

        ConditionalValue<byte[]> value = await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false);
        if (value.Value is { Length: > 64 * 1024 * 1024 })
        {
            throw new InvalidOperationException("ProofLimit: begin participant exceeds its ceiling.");
        }

        AppendParticipant(hash, key, value.HasValue, value.Value is not null, value.Value ?? []);
        await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
    }

    private static void AppendParticipant(IncrementalHash hash, string key, bool present, bool nonnull, ReadOnlySpan<byte> value)
    {
        using var descriptor = new EventEvolutionBinaryWriter(4096);
        descriptor.WriteString(key);
        descriptor.WriteByte(present ? (byte)1 : (byte)0);
        descriptor.WriteByte(nonnull ? (byte)1 : (byte)0);
        descriptor.WriteInt32(value.Length);
        descriptor.WriteHash(SHA256.HashData(value));
        hash.AppendData(descriptor.CopyEncodedBytes());
    }

    private async Task<DaprReplayOperationResult> InspectBeginAsync(DaprReplayBeginTransition prepared,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust)
    {
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            DaprReplayOperationRecord? actual = await ReadOperationAsync(recovery.Token).ConfigureAwait(false);
            byte[] participants = await ComputeParticipantDigestAsync(prepared.Expected, null, recovery.Token).ConfigureAwait(false);
            bool expected = Exact(actual, prepared.Expected) && participants.AsSpan().SequenceEqual(prepared.ParticipantDigest);
            bool prior = Exact(actual, prepared.Prior) && (prepared.Prior is null
                ? await RequireInitialAbsenceAsync(recovery.Token).ConfigureAwait(false)
                : participants.AsSpan().SequenceEqual(prepared.ParticipantDigest));
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            return new(expected ? DaprReplayCommitOutcome.Proven : prior ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate,
                expected ? prepared.Expected.Generation : prepared.Prior?.Generation ?? 0, null, false);
        }
        catch
        {
            return new(DaprReplayCommitOutcome.Indeterminate, prepared.Prior?.Generation ?? 0, null, false);
        }
    }

    /// <summary>Admits an exact request before source work, then atomically saves ledger, blob, pointer and final result.</summary>
    internal async Task<DaprReplayOperationResult> ExecutePageAsync(DaprLogicalReplaySource source, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, ECDsa signingKey, string ownerId, long generation, long pageOrdinal,
        string requestId, int maxCount, CancellationToken cancellationToken)
    {
        if (pageOrdinal is < 1 or > 65536 || maxCount is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(pageOrdinal));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (requestId.Length > 256 || ownerId.Length > 256)
        {
            throw new ArgumentException("Logical request identity exceeds its bound.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!await ReleaseCacheQuarantineAsync().ConfigureAwait(false))
            {
                return new(DaprReplayCommitOutcome.Indeterminate, 0, null, false);
            }

            _reconstruction?.RequireCurrent(cancellationToken);
            _reconstruction?.RequireSource(binding, cancellationToken);
            await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
            if (_beginPending is not null)
            {
                return new(DaprReplayCommitOutcome.Indeterminate, _beginPending.Prior?.Generation ?? 0, null, false);
            }

            if (_pending is not null)
            {
                DaprReplayPendingTransition pending = _pending;
                DaprReplayOperationResult recovered = await InspectSaveAsync(pending.Prior, pending.Expected, pending.Ledger,
                    pending.Prepared.Response, pending.Ledger.IsFinal, pending.Prepared.Budget, pending.Prepared.CanonicalState, pending.Prepared.ReconstructionBudget).ConfigureAwait(false);
                if (recovered.Outcome == DaprReplayCommitOutcome.Indeterminate)
                {
                    return recovered;
                }

                pending.Dispose();
                _pending = null;
                recovered.Dispose();
            }

            DaprReplayOperationRecord prior = await ReadOperationAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("ReplayRestartRequired: operation was not committed.");
            var budget = _bufferBudget;
            byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
            RequireOperation(prior, sourceHash, trust.RegistryFingerprint.Span, binding.TargetSequence);
            if (!await RequireCommittedChainAsync(prior, binding, trust, budget, cancellationToken).ConfigureAwait(false))
            {
                return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
            }

            byte[] requestHash = RequestHash(ownerId, generation, pageOrdinal, requestId, maxCount, sourceHash, trust.RegistryFingerprint);
            string ledgerKey = LedgerKey(pageOrdinal);
            string responseKey = ResponseKey(pageOrdinal);
            ConditionalValue<DaprReplayPageLedger> existing = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(ledgerKey, cancellationToken).ConfigureAwait(false);
            if (existing.HasValue)
            {
                if (existing.Value is null || pageOrdinal > prior.PageOrdinal)
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                }

                if (!existing.Value.RequestHash.AsSpan().SequenceEqual(requestHash) || existing.Value.Generation != generation)
                {
                    throw new InvalidOperationException("ReplayRequestConflict: the committed page has a different exact request.");
                }

                DaprLogicalResponseOwner? retained = await ReadPinnedAsync(existing.Value, responseKey, budget, cancellationToken).ConfigureAwait(false);
                if (retained is null)
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                }

                bool transferred = false;
                try
                {
                    RequirePinned(retained.Bytes.Span, existing.Value, binding, trust, budget, cancellationToken);
                    if (existing.Value.IsFinal && !await RequireFinalAsync(retained.Bytes, cancellationToken).ConfigureAwait(false))
                    {
                        return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                    }

                    await _stateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                    DaprLogicalResponseOwner? state = _reconstruction is null ? null
                        : await ReadStateAsync(pageOrdinal, existing.Value.CanonicalStateHash!, budget, cancellationToken).ConfigureAwait(false);
                    if (_reconstruction is not null && state is null)
                    {
                        return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                    }

                    var result = new DaprReplayOperationResult(DaprReplayCommitOutcome.Proven, prior.Generation, retained, existing.Value.IsFinal)
                    {
                        CanonicalState = state
                    };
                    transferred = true;
                    return await AuthorizeCommittedResponseAsync(result, source, binding, trust, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (!transferred)
                    {
                        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        try
                        {
                            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
                            retained.Dispose();
                        }
                        catch
                        {
                            _cacheQuarantine.Add(retained);
                        }
                    }
                }
            }

            ConditionalValue<byte[]> orphanResponse = await _stateManager.TryGetStateAsync<byte[]>(responseKey, cancellationToken).ConfigureAwait(false);
            ConditionalValue<byte[]> orphanFinal = await _stateManager.TryGetStateAsync<byte[]>(FinalKey, cancellationToken).ConfigureAwait(false);
            ConditionalValue<byte[]> orphanState = await _stateManager.TryGetStateAsync<byte[]>(StateKey(pageOrdinal), cancellationToken).ConfigureAwait(false);
            ConditionalValue<byte[]> orphanFinalState = await _stateManager.TryGetStateAsync<byte[]>(FinalStateKey, cancellationToken).ConfigureAwait(false);
            if (orphanResponse.HasValue || orphanFinal.HasValue || orphanState.HasValue || orphanFinalState.HasValue)
            {
                await _stateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
                return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
            }

            if (prior.OwnerId != ownerId || prior.Generation != generation || prior.IsComplete || pageOrdinal != prior.PageOrdinal + 1)
            {
                throw new InvalidOperationException("ReplayOwnerFenceMismatch: stale owner or noncontiguous page.");
            }

            // Fresh last-ledger/blob readback, not a caller hash, establishes successor authority.
            if (prior.PageOrdinal > 0)
            {
                ConditionalValue<DaprReplayPageLedger> previous = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(
                    LedgerKey(prior.PageOrdinal), cancellationToken).ConfigureAwait(false);
                if (!previous.HasValue || previous.Value is null || previous.Value.EndSequence != prior.CompletedSequence
                    || !previous.Value.Accumulator.AsSpan().SequenceEqual(prior.Accumulator))
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                }

                using DaprLogicalResponseOwner? previousPin = await ReadPinnedAsync(previous.Value, ResponseKey(prior.PageOrdinal), budget, cancellationToken).ConfigureAwait(false);
                if (previousPin is null)
                {
                    return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
                }

                RequirePinned(previousPin.Bytes.Span, previous.Value, binding, trust, budget, cancellationToken);
            }

            DaprReplayPreparedTransition prepared = await PrepareTransitionAsync(source, binding, trust, signingKey,
                prior, maxCount, budget, cancellationToken).ConfigureAwait(false);
            DaprLogicalReplayPage page = prepared.Page;
            DaprLogicalResponseOwner response = prepared.Response;
            try
            {
                if (_reconstruction is not null)
                {
                    await AttachReconstructionAsync(prepared, prior, source, binding, trust, cancellationToken).ConfigureAwait(false);
                }

                bool isFinal = page.PrefixFields.EndSequence == binding.TargetSequence;
                var ledger = new DaprReplayPageLedger(pageOrdinal, generation, requestHash, prior.Accumulator.ToArray(),
                    page.PrefixFields.Accumulator.ToArray(), page.PrefixFields.StartSequence, page.PrefixFields.EndSequence,
                    page.PrefixFields.Count, SHA256.HashData(response.Bytes.Span), isFinal)
                {
                    PriorStateHash = prior.CanonicalStateHash,
                    CanonicalStateHash = prepared.CanonicalState is null ? null : SHA256.HashData(prepared.CanonicalState.Bytes.Span)
                };
                var expected = prior with
                {
                    PageOrdinal = pageOrdinal,
                    CompletedSequence = ledger.EndSequence,
                    Accumulator = ledger.Accumulator.ToArray(),
                    IsComplete = isFinal,
                    CanonicalStateHash = ledger.CanonicalStateHash
                };
                bool saved = false;
                try
                {
                    await _stateManager.SetStateAsync(ledgerKey, ledger, cancellationToken).ConfigureAwait(false);
                    await _stateManager.SetStateAsync(responseKey, response.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                    await _stateManager.SetStateAsync(OperationKey, expected, cancellationToken).ConfigureAwait(false);
                    if (isFinal)
                    {
                        await _stateManager.SetStateAsync(FinalKey, response.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                    }

                    if (prepared.CanonicalState is not null)
                    {
                        await _stateManager.SetStateAsync(StateKey(pageOrdinal), prepared.CanonicalState.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                        if (isFinal)
                        {
                            await _stateManager.SetStateAsync(FinalStateKey, prepared.CanonicalState.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    _reconstruction?.RequireCurrent(cancellationToken);
                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    saved = true;
                    await _stateManager.SaveStateAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception) when (saved)
                {
                    /* Acknowledgement/cancellation cannot change committed truth. */
                }
                catch
                {
                    try
                    {
                        await _stateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        _pending = new DaprReplayPendingTransition(prior, expected, ledger, prepared);
                    }

                    throw;
                }

                DaprReplayOperationResult inspected = await InspectSaveAsync(prior, expected, ledger, response, isFinal, budget, prepared.CanonicalState, prepared.ReconstructionBudget).ConfigureAwait(false);
                if (inspected.Outcome == DaprReplayCommitOutcome.Indeterminate)
                {
                    _pending = new DaprReplayPendingTransition(prior, expected, ledger, prepared);
                }

                return await AuthorizeCommittedResponseAsync(inspected, source, binding, trust, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (_pending?.Prepared != prepared)
                {
                    prepared.Dispose();
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DaprReplayOperationResult> AuthorizeCommittedResponseAsync(DaprReplayOperationResult result,
        DaprLogicalReplaySource source, DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        if (result.Outcome != DaprReplayCommitOutcome.Proven || result.Response is null)
        {
            return result;
        }

        try
        {
            _reconstruction?.RequireCurrent(token);
            await source.RequireCurrentAsync(binding, trust, token).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result.Dispose();
            return result with
            {
                Response = null,
                CanonicalState = null,
                OriginatingCancellationObserved = true
            };
        }
        catch
        {
            result.Dispose();
            return result with
            {
                Response = null,
                CanonicalState = null,
                ResponseUnavailable = true
            };
        }
    }

    private async Task<DaprReplayPreparedTransition> PrepareTransitionAsync(DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, ECDsa key, DaprReplayOperationRecord prior,
        int maxCount, EventBufferBudget budget, CancellationToken token)
    {
        int count = binding.TargetSequence == 0 ? 1 : (int)Math.Min(maxCount, binding.TargetSequence - prior.CompletedSequence);
        while (true)
        {
            DaprLogicalReplayPage? page = null;
            DaprLogicalResponseOwner? response = null;
            EventBufferReservation? stages = null;
            try
            {
                page = prior.PageOrdinal == 0
                    ? await source.ReadFirstPageAsync(binding, count, trust, key, budget, token).ConfigureAwait(false)
                    : await source.ReadNextPageAsync(binding, count, new DaprReplayCommittedProgress(prior.SourceBindingHash,
                        prior.RegistryFingerprint, prior.CompletedSequence, prior.Accumulator), trust, key, budget, token).ConfigureAwait(false);
                response = page.EncodeResponse(budget, token);
                // Admits separate cache staging/readback arrays and a returned private image before any save.
                stages = budget.Reserve(checked(response.Bytes.Length * 4 + 4096));
                var prepared = new DaprReplayPreparedTransition(page, response, stages, budget);
                try
                {
                    if (_reconstruction is not null)
                    {
                        await PrepareReconstructionIntakeAsync(prepared, prior, binding, trust, token).ConfigureAwait(false);
                    }

                    return prepared;
                }
                catch
                {
                    prepared.Dispose();
                    throw;
                }
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("ScratchLimit:", StringComparison.Ordinal)
                || error.Message.StartsWith("ProofLimit:", StringComparison.Ordinal) || error.Message.StartsWith("ReadableLimit:", StringComparison.Ordinal)
                || error.Message.StartsWith("RawEnvelopeLimit:", StringComparison.Ordinal))
            {
                page?.Dispose();
                response?.Dispose();
                stages?.Dispose();
                await source.RequireCurrentAsync(binding, trust, token).ConfigureAwait(false);
                if (count == 1)
                {
                    throw new InvalidOperationException("ProofLimit: a single complete logical page cannot fit its composed admitted capacity.", error);
                }

                count = Math.Max(1, count / 2);
            }
            catch
            {
                page?.Dispose();
                response?.Dispose();
                stages?.Dispose();
                throw;
            }
        }
    }

    private async Task<bool> RequireCommittedChainAsync(DaprReplayOperationRecord record, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        byte[] accumulator = DaprLogicalClaimCodec.ComputeGenesis(record.SourceBindingHash, record.RegistryFingerprint);
        long end = 0;
        long generation = 0;
        byte[]? priorStateHash = null;
        if (_reconstruction is not null)
        {
            ConditionalValue<byte[]> initial = await _stateManager.TryGetStateAsync<byte[]>(StateKey(0), token).ConfigureAwait(false);
            if (!initial.HasValue || initial.Value is null || initial.Value.Length > 64 * 1024 * 1024)
            {
                return false;
            }

            priorStateHash = SHA256.HashData(initial.Value);
            if (record.PageOrdinal == 0 && !OptionalEqual(priorStateHash, record.CanonicalStateHash))
            {
                return false;
            }
        }

        for (long ordinal = 1; ordinal <= record.PageOrdinal; ordinal++)
        {
            ConditionalValue<DaprReplayPageLedger> value = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(ordinal), token).ConfigureAwait(false);
            if (!value.HasValue || value.Value is null)
            {
                return false;
            }

            DaprReplayPageLedger ledger = value.Value;
            if (ledger.PageOrdinal != ordinal || ledger.Generation < 1 || ledger.Generation < generation || ledger.Generation > record.Generation
                || ledger.RequestHash is not
                { Length: 32 }
            || ledger.ResponseHash is not
            { Length: 32 }
            || ledger.Accumulator is not
            { Length: 32 }

                || !ledger.PreviousAccumulator.AsSpan().SequenceEqual(accumulator)
                || (binding.TargetSequence == 0 ? ordinal != 1 || ledger.StartSequence != 1 || ledger.EndSequence != 0 || ledger.Count != 0
                    : end == long.MaxValue || ledger.StartSequence != end + 1 || ledger.Count is < 1 or > 256
                        || ledger.EndSequence < ledger.StartSequence || ledger.EndSequence > binding.TargetSequence
                        || ledger.EndSequence - ledger.StartSequence != ledger.Count - 1)
                || ledger.IsFinal != (ledger.EndSequence == binding.TargetSequence) || (ledger.IsFinal && ordinal != record.PageOrdinal))
            {
                return false;
            }

            DaprLogicalResponseOwner? response = await ReadPinnedAsync(ledger, ResponseKey(ordinal), budget, token).ConfigureAwait(false);
            if (response is null)
            {
                return false;
            }

            try
            {
                RequirePinned(response.Bytes.Span, ledger, binding, trust, budget, token);
                if (!OptionalEqual(ledger.PriorStateHash, priorStateHash)
                    || !await RequireStateAsync(ordinal, ledger.CanonicalStateHash, ledger.IsFinal, token, record.IsComplete).ConfigureAwait(false))
                {
                    return false;
                }

                priorStateHash = ledger.CanonicalStateHash;
                if (ledger.IsFinal && !await RequireFinalAsync(response.Bytes, token).ConfigureAwait(false))
                {
                    return false;
                }
            }
            finally
            {
                try
                {
                    await _stateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
                    response.Dispose();
                }
                catch
                {
                    _cacheQuarantine.Add(response);
                    throw;
                }
            }

            accumulator = ledger.Accumulator.ToArray();
            end = ledger.EndSequence;
            generation = ledger.Generation;
        }

        if (!record.IsComplete)
        {
            ConditionalValue<DaprReplayPageLedger> next = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(record.PageOrdinal + 1), token).ConfigureAwait(false);
            if (next.HasValue)
            {
                return false;
            }

            foreach (string key in new[] { ResponseKey(record.PageOrdinal + 1), StateKey(record.PageOrdinal + 1), FinalKey, FinalStateKey })
            {
                if ((await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false)).HasValue)
                {
                    return false;
                }
            }
        }

        return end == record.CompletedSequence && accumulator.AsSpan().SequenceEqual(record.Accumulator)
            && OptionalEqual(priorStateHash, record.CanonicalStateHash);
    }

    private async Task<bool> ReleaseCacheQuarantineAsync()
    {
        if (_cacheQuarantine.Count == 0)
        {
            return true;
        }

        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            foreach (DaprLogicalResponseOwner owned in _cacheQuarantine)
            {
                owned.Dispose();
            }

            _cacheQuarantine.Clear();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DaprReplayOperationResult> InspectSaveAsync(DaprReplayOperationRecord prior,
        DaprReplayOperationRecord expected, DaprReplayPageLedger ledger, DaprLogicalResponseOwner response, bool isFinal, EventBufferBudget budget, DaprLogicalResponseOwner? canonicalState, EventBufferBudget? stateBudget)
    {
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            DaprReplayOperationRecord? actual = await ReadOperationAsync(recovery.Token).ConfigureAwait(false);
            ConditionalValue<DaprReplayPageLedger> actualLedger = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(ledger.PageOrdinal), recovery.Token).ConfigureAwait(false);
            ConditionalValue<byte[]> blob = await _stateManager.TryGetStateAsync<byte[]>(ResponseKey(ledger.PageOrdinal), recovery.Token).ConfigureAwait(false);
            ConditionalValue<byte[]> final = await _stateManager.TryGetStateAsync<byte[]>(FinalKey, recovery.Token).ConfigureAwait(false);
            bool stateComplete = await RequireStateAsync(ledger.PageOrdinal, expected.CanonicalStateHash, isFinal, recovery.Token).ConfigureAwait(false);
            if (canonicalState is not null)
            {
                ConditionalValue<byte[]> actualState = await _stateManager.TryGetStateAsync<byte[]>(StateKey(ledger.PageOrdinal), recovery.Token).ConfigureAwait(false);
                stateComplete &= actualState.HasValue && actualState.Value is not null && actualState.Value.AsSpan().SequenceEqual(canonicalState.Bytes.Span);
            }

            bool complete = stateComplete && Exact(actual, expected) && actualLedger.HasValue && Exact(actualLedger.Value, ledger)
                && blob.HasValue && blob.Value.AsSpan().SequenceEqual(response.Bytes.Span)
                && (isFinal ? final.HasValue && final.Value.AsSpan().SequenceEqual(response.Bytes.Span) : !final.HasValue);
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            if (complete)
            {
                DaprLogicalResponseOwner resultResponse = DaprLogicalResponseOwner.Capture(response.Bytes.Span, budget);
                try
                {
                    return new(DaprReplayCommitOutcome.Proven, expected.Generation, resultResponse, isFinal)
                    {
                        CanonicalState = canonicalState is null ? null : DaprLogicalResponseOwner.Capture(canonicalState.Bytes.Span, stateBudget ?? budget)
                    };
                }
                catch
                {
                    resultResponse.Dispose();
                    throw;
                }
            }

            ConditionalValue<byte[]> state = await _stateManager.TryGetStateAsync<byte[]>(StateKey(ledger.PageOrdinal), recovery.Token).ConfigureAwait(false);
            ConditionalValue<byte[]> finalState = await _stateManager.TryGetStateAsync<byte[]>(FinalStateKey, recovery.Token).ConfigureAwait(false);
            bool predecessor = await RequireStateAsync(prior.PageOrdinal, prior.CanonicalStateHash, false, recovery.Token).ConfigureAwait(false);
            bool absent = predecessor && Exact(actual, prior) && !actualLedger.HasValue && !blob.HasValue && !final.HasValue && !state.HasValue && !finalState.HasValue;
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            return new(absent ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
        }
        catch
        {
            return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
        }
    }

    private static void RequirePinned(ReadOnlySpan<byte> bytes, DaprReplayPageLedger ledger, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        using DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> verified = DaprLogicalReplayResponseVerifier.Verify(bytes, binding, trust, ledger.PreviousAccumulator, budget, token);
        DaprLogicalPrefixClaim prefix = verified.Value;
        if (prefix.StartSequence != ledger.StartSequence || prefix.EndSequence != ledger.EndSequence || prefix.Count != ledger.Count
            || !prefix.Accumulator.Span.SequenceEqual(ledger.Accumulator) || ledger.IsFinal != (prefix.EndSequence == binding.TargetSequence))
        {
            throw new InvalidOperationException("ReplayRestartRequired: retained proof disagrees with its operation-owned ledger.");
        }
    }

    private async Task<DaprReplayOperationRecord?> ReadOperationAsync(CancellationToken token)
    {
        await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
        ConditionalValue<DaprReplayOperationRecord> state = await _stateManager.TryGetStateAsync<DaprReplayOperationRecord>(OperationKey, token).ConfigureAwait(false);
        if (state.HasValue && state.Value is null)
        {
            throw new InvalidOperationException("ReplayRestartRequired: present operation has no logical value.");
        }

        return state.HasValue ? state.Value : null;
    }

    private async Task<DaprLogicalResponseOwner?> ReadPinnedAsync(DaprReplayPageLedger ledger, string key, EventBufferBudget budget, CancellationToken token)
    {
        ConditionalValue<byte[]> value = await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false);
        if (!value.HasValue || value.Value is null || value.Value.Length > 64 * 1024 * 1024 || !SHA256.HashData(value.Value).AsSpan().SequenceEqual(ledger.ResponseHash))
        {
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            return null;
        }

        DaprLogicalResponseOwner owned = DaprLogicalResponseOwner.Capture(value.Value, budget);
        try
        {
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            return owned;
        }
        catch
        {
            _cacheQuarantine.Add(owned);
            throw;
        }
    }

    private async Task<bool> RequireFinalAsync(ReadOnlyMemory<byte> response, CancellationToken token)
    {
        ConditionalValue<byte[]> result = await _stateManager.TryGetStateAsync<byte[]>(FinalKey, token).ConfigureAwait(false);
        return result.HasValue && result.Value.AsSpan().SequenceEqual(response.Span);
    }

    private void RequireOperation(DaprReplayOperationRecord record, ReadOnlySpan<byte> source, ReadOnlySpan<byte> registry, long target)
    {
        if (record.TenantId != _tenant || record.OperationId != _operation || record.Generation < 1 || record.PageOrdinal is < 0 or > 65536
            || record.CompletedSequence < 0 || record.CompletedSequence > target || record.TargetSequence != target
            || record.Accumulator is not
            { Length: 32 }
        || string.IsNullOrWhiteSpace(record.OwnerId) || record.OwnerId.Length > 256
            || record.IsComplete != (record.PageOrdinal > 0 && record.CompletedSequence == target)
            || (record.PageOrdinal == 0 && record.CompletedSequence != 0)
            || (target == 0 && record.PageOrdinal > 1)
            || (target > 0 && record.PageOrdinal > record.CompletedSequence)
            || !OptionalEqual(record.ReconstructionBindingHash, _reconstruction?.Fingerprint.ToArray())
            || (_reconstruction is null ? record.CanonicalStateHash is not null : record.CanonicalStateHash is not
            { Length: 32 })
            || !record.SourceBindingHash.AsSpan().SequenceEqual(source)
            || !record.RegistryFingerprint.AsSpan().SequenceEqual(registry))
        {
            throw new InvalidOperationException("ReplayRestartRequired: committed operation identity or fixed source changed.");
        }
    }

    private byte[] RequestHash(string owner, long generation, long page, string request, int maxCount,
        ReadOnlyMemory<byte> source, ReadOnlyMemory<byte> registry)
    {
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteRaw("HX-EV-DAPR-PAGE-REQUEST-1\0"u8);
        writer.WriteByte(1);
        writer.WriteString(_tenant);
        writer.WriteString(_operation);
        writer.WriteString(owner);
        writer.WriteInt64(generation);
        writer.WriteInt64(page);
        writer.WriteString(request);
        writer.WriteInt32(maxCount);
        writer.WriteHash(source.Span);
        writer.WriteHash(registry.Span);
        return writer.ComputeSha256();
    }

    private static string StateKey(long ordinal) => "logical-replay:state:" + ordinal.ToString(CultureInfo.InvariantCulture);
    private static bool OptionalEqual(byte[]? left, byte[]? right) => left is null || right is null ? left is null && right is null : left.AsSpan().SequenceEqual(right);
    private static string LedgerKey(long ordinal) => "logical-replay:ledger:" + ordinal.ToString(CultureInfo.InvariantCulture);
    private static string ResponseKey(long ordinal) => "logical-replay:response:" + ordinal.ToString(CultureInfo.InvariantCulture);
    private static bool Exact(DaprReplayOperationRecord? left, DaprReplayOperationRecord? right)
        => left is null || right is null ? left is null && right is null
            : left.TenantId == right.TenantId && left.OperationId == right.OperationId && left.OwnerId == right.OwnerId
            && left.Generation == right.Generation && left.TargetSequence == right.TargetSequence && left.PageOrdinal == right.PageOrdinal
            && left.CompletedSequence == right.CompletedSequence && left.IsComplete == right.IsComplete
            && left.SourceBindingHash.AsSpan().SequenceEqual(right.SourceBindingHash) && left.RegistryFingerprint.AsSpan().SequenceEqual(right.RegistryFingerprint)
            && left.Accumulator.AsSpan().SequenceEqual(right.Accumulator)
            && OptionalEqual(left.ReconstructionBindingHash, right.ReconstructionBindingHash) && OptionalEqual(left.CanonicalStateHash, right.CanonicalStateHash);
    private static bool Exact(DaprReplayPageLedger? left, DaprReplayPageLedger? right)
        => left is null || right is null ? left is null && right is null
            : left.PageOrdinal == right.PageOrdinal && left.Generation == right.Generation && left.StartSequence == right.StartSequence
            && left.EndSequence == right.EndSequence && left.Count == right.Count && left.IsFinal == right.IsFinal
            && left.RequestHash.AsSpan().SequenceEqual(right.RequestHash) && left.PreviousAccumulator.AsSpan().SequenceEqual(right.PreviousAccumulator)
            && left.Accumulator.AsSpan().SequenceEqual(right.Accumulator) && left.ResponseHash.AsSpan().SequenceEqual(right.ResponseHash)
            && OptionalEqual(left.PriorStateHash, right.PriorStateHash) && OptionalEqual(left.CanonicalStateHash, right.CanonicalStateHash);

    /// <summary>Releases private pending/cache charges only after successful cache detachment; durable actor arrays are never cleared.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            _pending?.Dispose();
            _pending = null;
            _beginPending?.Dispose();
            _beginPending = null;
            _lastGoodDiagnostic?.Dispose();
            _lastGoodDiagnostic = null;
            foreach (DaprLogicalResponseOwner owner in _cacheQuarantine)
            {
                owner.Dispose();
            }

            _cacheQuarantine.Clear();
            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
