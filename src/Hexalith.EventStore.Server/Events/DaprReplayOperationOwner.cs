using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Implements the dedicated replay actor's single-save operation/ledger/blob/final-result protocol.</summary>
/// <remarks>Only its owning actor may call it. No registration, SQL or cross-owner atomicity is supplied.</remarks>
internal sealed partial class DaprReplayOperationOwner : IAsyncDisposable
{
    private string OperationKey => _anchoredIntake is null ? "logical-replay:operation:v1" : "logical-replay:anchored:operation:v1";
    private string FinalKey => _anchoredIntake is null ? "logical-replay:final:v1" : "logical-replay:anchored:final:v1";
    private string FinalStateKey => _anchoredIntake is null ? "logical-replay:final-state:v1" : "logical-replay:anchored:final-state:v1";
    private string CommandProofKey => _anchoredIntake is null ? "logical-replay:command-proof:v1" : "logical-replay:anchored:command-proof:v1";
    private readonly RegisteredLogicalReplayBinding? _reconstruction;
    private readonly IActorStateManager _stateManager;
    private readonly string _tenant;
    private readonly string _operation;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly EventBufferBudget _bufferBudget;
    private CommandEnvelope? _command;
    private readonly byte[]? _commandRouteHash;
    private readonly EventBufferReservation? _commandCharge;
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
                _reconstruction.Evolution, partition, token, _anchoredIntake);
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
                cancellation => RequireSourceCurrentAsync(actualSource, source, trust, cancellation), token).ConfigureAwait(false);
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
        int maximumBufferBytes = 128 * 1024 * 1024, RegisteredLogicalReplayBinding? reconstruction = null,
        CommandEnvelope? command = null)
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
        if (command is not null)
        {
            if (reconstruction is null || command.TenantId != tenantId || command.Payload is null || command.Payload.Length > 16 * 1024 * 1024)
            { throw new ArgumentException("A logical command operation requires its exact reconstruction route and bounded command."); }
            _commandCharge = _bufferBudget.Reserve(checked(command.Payload.Length + 4 * 1024 * 1024));
            try
            {
                // Admit metadata before retaining dictionary slots or a private command payload.
                _ = DaprLogicalCommandStateCodec.CommandHash(command, _bufferBudget, CancellationToken.None);
                _command = command with { Payload = command.Payload.ToArray(), Extensions = command.Extensions is null
                    ? null : new Dictionary<string, string>(command.Extensions, StringComparer.Ordinal) };
                _commandRouteHash = DaprLogicalCommandStateCodec.CommandHash(_command, _bufferBudget, CancellationToken.None);
            }
            catch { if (_command is not null) { CryptographicOperations.ZeroMemory(_command.Payload); } _commandCharge.Dispose(); throw; }
        }
    }

    /// <summary>Creates or fences a fixed-source operation; takeover requires the exact observed prior generation.</summary>
    /// <remarks>Proven incomplete admission establishes only the owner generation; ExecutePage admits the complete participant chain.</remarks>
    internal async Task<DaprReplayOperationResult> BeginAsync(DaprLogicalReplaySource source, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, string ownerId, long? takeoverGeneration, CancellationToken cancellationToken)
    {
        RequireAnchoredEntry(cancellationToken);
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
            if (ownerId.Length > 256 || Encoding.UTF8.GetByteCount(ownerId) > 256 || binding.Identity.TenantId != _tenant)
            {
                throw new ArgumentException("Replay owner or source tenant mismatch.");
            }

            _reconstruction?.RequireCurrent(cancellationToken);
            _reconstruction?.RequireSource(binding, cancellationToken);
            RequireCommandSource(binding, cancellationToken);
            await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
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
                ? _initialAnchor is null
                    ? await CreateInitialStateAsync(budget, token => RequireSourceCurrentAsync(source, binding, trust, token), cancellationToken).ConfigureAwait(false)
                    : CaptureState(_initialAnchor.CanonicalState, budget)
                : null;
            EventBufferReservation staging;
            try
            {
                staging = budget.Reserve(initial is null ? 4096 : checked(initial.Bytes.Length * 4 + (_anchorImage?.Bytes.Length ?? 0) * 4 + 4096));
            }
            catch
            {
                initial?.Dispose();
                throw;
            }

            DaprReplayOperationRecord expected;
            byte[] participantDigest;
            try
            {
                expected = prior is null
                ? new DaprReplayOperationRecord(_tenant, _operation, ownerId, 1, sourceHash, trust.RegistryFingerprint.ToArray(),
                    binding.TargetSequence, 0, _anchoredIntake?.Selection.CoveredSequence ?? 0,
                    _initialAnchor is null ? DaprLogicalClaimCodec.ComputeGenesis(sourceHash, trust.RegistryFingerprint) : _initialAnchor.AccumulatorSeed.ToArray(), false)
                {
                    ReconstructionBindingHash = _reconstruction?.Fingerprint.ToArray(),
                    CanonicalStateHash = initial is null ? null : SHA256.HashData(initial.Bytes.Span),
                    EffectiveChainHash = _initialAnchor is null
                        ? DaprLogicalReplayCommitmentCodec.EffectiveGenesis(sourceHash, trust.RegistryFingerprint, _reconstruction?.Fingerprint, budget)
                        : _initialAnchor.EffectiveSeed.ToArray(),
                    TranscriptHash = _initialAnchor is null
                        ? DaprLogicalReplayCommitmentCodec.TranscriptGenesis(_tenant, _operation, sourceHash,
                            trust.RegistryFingerprint, _reconstruction?.Fingerprint, Memory(initial is null ? null : SHA256.HashData(initial.Bytes.Span)),
                            Memory(_commandRouteHash), budget)
                        : DaprLogicalReplayCommitmentCodec.AnchoredTranscriptGenesis(_tenant, _operation, _initialAnchor.SelectionHash,
                            _initialAnchor.TranscriptSeed, SHA256.HashData(initial!.Bytes.Span), budget),
                    CommandRouteHash = _commandRouteHash?.ToArray(),
                    LogicalEvidenceModelId = _anchoredIntake is null ? DaprLogicalSourceBinding.ModelId : DaprLogicalReplayAnchorCodec.ModelId,
                    AnchorSelectionHash = _anchoredIntake?.SelectionHash.ToArray(),
                    AnchorCoveredSequence = _anchoredIntake?.Selection.CoveredSequence
                }
                : prior with
                {
                    OwnerId = ownerId,
                    Generation = prior.Generation + 1
                };
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
                    if (_anchorImage is not null)
                    {
                        await _stateManager.SetStateAsync(AnchorKey, _anchorImage.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                    }
                }

                _reconstruction?.RequireCurrent(cancellationToken);
                await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
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
                    await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
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
        if (_anchoredIntake is not null && (await _stateManager.TryGetStateAsync<byte[]>(AnchorKey, token).ConfigureAwait(false)).HasValue)
        {
            return false;
        }
        ConditionalValue<DaprReplayPageLedger> ledger = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(1), token).ConfigureAwait(false);
        foreach (string key in new[] { ResponseKey(1), FinalKey, StateKey(0), StateKey(1), FinalStateKey, CommandProofKey })
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
        DaprLogicalResponseOwner? initialOverride, CancellationToken token, DaprReplayPageLedger? ledgerOverride = null, DaprReplayPreparedTransition? prepared = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("HX-EV-DAPR-BEGIN-PARTICIPANTS-1\0"u8);
        if (_anchoredIntake is not null)
        {
            await AppendBytesParticipantAsync(hash, AnchorKey, initialOverride is not null ? _anchorImage : null, token).ConfigureAwait(false);
        }
        for (long ordinal = 0; ordinal <= record.PageOrdinal + 1; ordinal++)
        {
            if (ordinal > 0)
            {
                string ledgerKey = LedgerKey(ordinal);
                ConditionalValue<DaprReplayPageLedger> ledger = ledgerOverride?.PageOrdinal == ordinal
                    ? new ConditionalValue<DaprReplayPageLedger>(true, ledgerOverride)
                    : await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(ledgerKey, token).ConfigureAwait(false);
                byte[] encoded = ledger.HasValue && ledger.Value is not null ? EncodeLedger(ledger.Value) : [];
                try
                {
                    AppendParticipant(hash, ledgerKey, ledger.HasValue, ledger.Value is not null, encoded);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encoded);
                }

                await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
                await AppendBytesParticipantAsync(hash, ResponseKey(ordinal), ledgerOverride?.PageOrdinal == ordinal ? prepared?.Response : null, token).ConfigureAwait(false);
            }

            await AppendBytesParticipantAsync(hash, StateKey(ordinal), ordinal == 0 ? initialOverride : ledgerOverride?.PageOrdinal == ordinal ? prepared?.CanonicalState : null, token).ConfigureAwait(false);
        }

        await AppendBytesParticipantAsync(hash, FinalKey, ledgerOverride?.IsFinal == true ? prepared?.Response : null, token).ConfigureAwait(false);
        await AppendBytesParticipantAsync(hash, FinalStateKey, ledgerOverride?.IsFinal == true ? prepared?.CanonicalState : null, token).ConfigureAwait(false);
        await AppendBytesParticipantAsync(hash, CommandProofKey, prepared?.CommandProof, token).ConfigureAwait(false);
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
        RequireAnchoredEntry(cancellationToken);
        if (pageOrdinal is < 1 or > 65536 || maxCount is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(pageOrdinal));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (requestId.Length > 256 || ownerId.Length > 256 || Encoding.UTF8.GetByteCount(ownerId) > 256)
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
            RequireCommandSource(binding, cancellationToken);
            await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
            if (_beginPending is not null)
            {
                return new(DaprReplayCommitOutcome.Indeterminate, _beginPending.Prior?.Generation ?? 0, null, false);
            }

            if (_pending is not null)
            {
                DaprReplayPendingTransition pending = _pending;
                DaprReplayOperationResult recovered = await InspectSaveAsync(pending.Prior, pending.Expected, pending.Ledger,
                    pending.Prepared.Response, pending.Ledger.IsFinal, pending.Prepared.Budget, pending.Prepared.CanonicalState, pending.Prepared.ReconstructionBudget, pending.Prepared).ConfigureAwait(false);
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
                    await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
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

            byte[] admittedPriorParticipants = await ComputeParticipantDigestAsync(prior, null, cancellationToken).ConfigureAwait(false);
            DaprReplayPreparedTransition prepared = await PrepareTransitionAsync(source, binding, trust, signingKey,
                prior, maxCount, budget, cancellationToken).ConfigureAwait(false);
            DaprLogicalReplayPage page = prepared.Page;
            DaprLogicalResponseOwner response = prepared.Response;
            try
            {
                byte[] effective = DaprLogicalReplayCommitmentCodec.EffectiveSuccessor(response.Bytes.Span, binding, trust,
                    prior.Accumulator, prior.EffectiveChainHash!, Memory(prior.ReconstructionBindingHash), prepared.CommitmentBudget!, cancellationToken, _anchoredIntake);
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
                    CanonicalStateHash = prepared.CanonicalState is null ? null : SHA256.HashData(prepared.CanonicalState.Bytes.Span),
                    PreviousEffectiveChainHash = prior.EffectiveChainHash!.ToArray(), EffectiveChainHash = effective,
                    PreviousTranscriptHash = prior.TranscriptHash!.ToArray(),
                    AnchorSelectionHash = prior.AnchorSelectionHash?.ToArray()
                };
                ledger = ledger with { TranscriptHash = TranscriptSuccessor(prior, ledger, prepared.CommitmentBudget!) };
                var expected = prior with
                {
                    PageOrdinal = pageOrdinal,
                    CompletedSequence = ledger.EndSequence,
                    Accumulator = ledger.Accumulator.ToArray(),
                    IsComplete = isFinal,
                    CanonicalStateHash = ledger.CanonicalStateHash,
                    EffectiveChainHash = effective, TranscriptHash = ledger.TranscriptHash
                };
                if (isFinal && _command is not null)
                {
                    AttachCommandProof(prepared, expected, binding, trust, signingKey, cancellationToken);
                    byte[] proofHash = SHA256.HashData(prepared.CommandProof!.Bytes.Span);
                    ledger = ledger with { CommandProofHash = proofHash };
                    expected = expected with { CommandProofHash = proofHash };
                }
                DaprReplayOperationRecord? freshPrior = await ReadOperationAsync(cancellationToken).ConfigureAwait(false);
                byte[] freshParticipants = await ComputeParticipantDigestAsync(prior, null, cancellationToken).ConfigureAwait(false);
                if (!Exact(freshPrior, prior) || !freshParticipants.AsSpan().SequenceEqual(admittedPriorParticipants))
                { throw new InvalidOperationException("ProofMismatch: admitted predecessor changed during page callbacks."); }
                prepared.PriorParticipantDigest = admittedPriorParticipants;
                prepared.ExpectedParticipantDigest = await ComputeParticipantDigestAsync(expected, null, cancellationToken, ledger, prepared).ConfigureAwait(false);
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

                    if (prepared.CommandProof is not null)
                    {
                        await _stateManager.SetStateAsync(CommandProofKey, prepared.CommandProof.Bytes.ToArray(), cancellationToken).ConfigureAwait(false);
                    }
                    _reconstruction?.RequireCurrent(cancellationToken);
                    await RequireSourceCurrentAsync(source, binding, trust, cancellationToken).ConfigureAwait(false);
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

                DaprReplayOperationResult inspected = await InspectSaveAsync(prior, expected, ledger, response, isFinal, budget, prepared.CanonicalState, prepared.ReconstructionBudget, prepared).ConfigureAwait(false);
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

    /// <summary>Captures a private snapshot origin only from full actual completed reconstruction participants.</summary>
    internal async Task<DaprLogicalReplayAnchorOrigin> CaptureCompletedAnchorOriginAsync(DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        RequireAnchoredEntry(token);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        DaprLogicalResponseOwner? state = null;
        EventBufferReservation? charge = null;
        DaprLogicalReplayAnchorOrigin? captured = null;
        bool successfulReturn = false;
        try
        {
            token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(budget);
            if (_anchoredIntake is not null || _reconstruction is null || _command is not null || _pending is not null || _beginPending is not null || _bufferBudget.LiveBytes != 0)
            {
                throw new DaprLogicalAnchorRefusalException("AnchorOriginHold: actual completed reconstruction is unavailable.");
            }
            _reconstruction.RequireSource(binding, token);
            RequireCommandSource(binding, token);
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
            DaprReplayOperationRecord record = await ReadOperationAsync(token).ConfigureAwait(false)
                ?? throw new DaprLogicalAnchorRefusalException("AnchorOriginHold: actual operation is absent.");
            RequireOperation(record, DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget),
                trust.RegistryFingerprint.Span, binding.TargetSequence);
            if (!record.IsComplete || !await RequireCommittedChainAsync(record, binding, trust, budget, token).ConfigureAwait(false))
            {
                throw new DaprLogicalAnchorRefusalException("AnchorOriginHold: actual completed history changed.");
            }
            charge = budget.Reserve(4096);
            DaprReplayOperationRecord expected = CopyRecord(record);
            state = await ReadStateAsync(record.PageOrdinal, record.CanonicalStateHash!, budget, token).ConfigureAwait(false)
                ?? throw new DaprLogicalAnchorRefusalException("AnchorOriginHold: canonical final state is unavailable.");
            await RequireCompletedAnchorCoreAsync(expected, source, binding, trust, budget, token).ConfigureAwait(false);
            captured = new DaprLogicalReplayAnchorOrigin(state, expected, _reconstruction.SerializerId, binding, budget,
                cancellation => RequireCompletedAnchorCurrentAsync(expected, source, binding, trust, budget, cancellation), charge, token);
            state = null;
            charge = null;
            await RequireCompletedAnchorCoreAsync(expected, source, binding, trust, budget, token).ConfigureAwait(false);
            _ = captured.State;
            successfulReturn = true;
            return captured;
        }
        finally
        {
            state?.Dispose();
            charge?.Dispose();
            _gate.Release();
            try
            {
                token.ThrowIfCancellationRequested();
                if (!successfulReturn)
                {
                    captured?.Dispose();
                }
            }
            catch
            {
                captured?.Dispose();
                throw;
            }
        }
    }

    private async Task RequireCompletedAnchorCurrentAsync(DaprReplayOperationRecord expected, DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await RequireCompletedAnchorCoreAsync(expected, source, binding, trust, budget, token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            token.ThrowIfCancellationRequested();
        }
    }

    private async Task RequireCompletedAnchorCoreAsync(DaprReplayOperationRecord expected, DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reconstruction!.RequireSource(binding, token);
        RequireCommandSource(binding, token);
        await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
        DaprReplayOperationRecord? actual = await ReadOperationAsync(token).ConfigureAwait(false);
        if (_pending is not null || _beginPending is not null || !Exact(actual, expected)
            || !await RequireCommittedChainAsync(actual!, binding, trust, budget, token).ConfigureAwait(false))
        {
            throw new DaprLogicalAnchorRefusalException("AnchorOriginHold: actual complete participant authority changed.");
        }
        await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
        await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
        _reconstruction.RequireCurrent(token);
    }

    /// <summary>Captures command authority only from the complete actual owner's fresh durable participant chain.</summary>
    internal async Task<PrivateLogicalCommandState> CaptureCompletedCommandStateAsync(DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        RequireAnchoredEntry(token);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_disposed, this);
            if (_anchoredIntake is not null || _command is null || _reconstruction is null || _pending is not null || _beginPending is not null)
            { throw new InvalidOperationException("ProofMismatch: this actual operation has no completed command authority."); }
            RequireCommandSource(binding, token); _reconstruction.RequireSource(binding, token);
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
            DaprReplayOperationRecord record = await ReadOperationAsync(token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("ProofMismatch: completed operation is absent.");
            RequireOperation(record, DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, _bufferBudget), trust.RegistryFingerprint.Span, binding.TargetSequence);
            if (!record.IsComplete || !await RequireCommittedChainAsync(record, binding, trust, _bufferBudget, token).ConfigureAwait(false))
            { throw new InvalidOperationException("ProofMismatch: actual operation is incomplete or its history changed."); }
            using DaprLogicalResponseOwner state = await ReadStateAsync(record.PageOrdinal, record.CanonicalStateHash!, _bufferBudget, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("ProofMismatch: terminal state is unavailable.");
            using DaprLogicalResponseOwner proof = await ReadCommandProofAsync(record.CommandProofHash!, token).ConfigureAwait(false);
            DaprReplayOperationRecord expected = CopyRecord(record);
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
            return PrivateLogicalCommandState.Capture(state.Bytes.Span, proof.Bytes.Span, _reconstruction, trust, _bufferBudget,
                cancellation => RequireCompletedCommandCurrentAsync(expected, source, binding, trust, cancellation), _command, token);
        }
        finally { _gate.Release(); }
    }

    private async Task<DaprLogicalResponseOwner> ReadCommandProofAsync(byte[] hash, CancellationToken token)
    {
        ConditionalValue<byte[]> proof = await _stateManager.TryGetStateAsync<byte[]>(CommandProofKey, token).ConfigureAwait(false);
        if (!proof.HasValue || proof.Value is null || proof.Value.Length > DaprLogicalCommandStateProofCodec.MaximumProofBytes
            || !SHA256.HashData(proof.Value).AsSpan().SequenceEqual(hash))
        { throw new InvalidOperationException("ProofMismatch: terminal command proof is unavailable."); }
        DaprLogicalResponseOwner owned = DaprLogicalResponseOwner.Capture(proof.Value, _bufferBudget);
        try { await _stateManager.ClearCacheAsync(token).ConfigureAwait(false); return owned; }
        catch { _cacheQuarantine.Add(owned); throw; }
    }

    private async Task RequireCompletedCommandCurrentAsync(DaprReplayOperationRecord expected, DaprLogicalReplaySource source,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_disposed, this);
            _reconstruction!.RequireSource(binding, token); RequireCommandSource(binding, token);
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
            DaprReplayOperationRecord? actual = await ReadOperationAsync(token).ConfigureAwait(false);
            if (_pending is not null || _beginPending is not null || !Exact(actual, expected)
                || !await RequireCommittedChainAsync(actual!, binding, trust, _bufferBudget, token).ConfigureAwait(false))
            { throw new InvalidOperationException("ProofMismatch: completed command participant authority changed."); }
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
            _reconstruction.RequireCurrent(token);
        }
        finally { _gate.Release(); }
    }

    private static DaprReplayOperationRecord CopyRecord(DaprReplayOperationRecord value)
        => value with { SourceBindingHash = value.SourceBindingHash.ToArray(), RegistryFingerprint = value.RegistryFingerprint.ToArray(),
            Accumulator = value.Accumulator.ToArray(), ReconstructionBindingHash = value.ReconstructionBindingHash?.ToArray(),
            CanonicalStateHash = value.CanonicalStateHash?.ToArray(), EffectiveChainHash = value.EffectiveChainHash?.ToArray(),
            TranscriptHash = value.TranscriptHash?.ToArray(), CommandRouteHash = value.CommandRouteHash?.ToArray(), CommandProofHash = value.CommandProofHash?.ToArray(),
            AnchorSelectionHash = value.AnchorSelectionHash?.ToArray() };

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
            await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
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
        int count = binding.TargetSequence == prior.CompletedSequence ? 1 : (int)Math.Min(maxCount, binding.TargetSequence - prior.CompletedSequence);
        Task RequireOperationAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            _reconstruction?.RequireCurrent(cancellation);
            return _initialAnchor is null ? Task.CompletedTask : RequireSourceCurrentAsync(source, binding, trust, cancellation);
        }
        while (true)
        {
            DaprLogicalReplayPage? page = null;
            DaprLogicalResponseOwner? response = null;
            EventBufferReservation? stages = null;
            try
            {
                page = _anchoredIntake is not null
                    ? await source.ReadAnchoredPageAsync(binding, count, prior.CompletedSequence, prior.Accumulator, trust, key,
                        budget, token, _anchoredIntake, RequireOperationAsync).ConfigureAwait(false)
                    : prior.PageOrdinal == 0
                    ? await source.ReadFirstPageAsync(binding, count, trust, key, budget, token, RequireOperationAsync).ConfigureAwait(false)
                    : await source.ReadNextPageAsync(binding, count, new DaprReplayCommittedProgress(prior.SourceBindingHash,
                        prior.RegistryFingerprint, prior.CompletedSequence, prior.Accumulator), trust, key, budget, token,
                        RequireOperationAsync).ConfigureAwait(false);
                response = page.EncodeResponse(budget, token);
                // Admits separate cache staging/readback arrays and a returned private image before any save.
                stages = budget.Reserve(checked(response.Bytes.Length * 4 + 4096));
                var prepared = new DaprReplayPreparedTransition(page, response, stages, budget);
                try
                {
                    // Reserve every future commitment/proof workspace before the first reconstruction callback.
                    prepared.AttachCommitments(budget.CreatePartition(checked(DaprLogicalReplayCommitmentCodec.GetPreparationCapacity(response.Bytes.Span, token, _anchoredIntake is not null)
                        + (_command is null ? 0 : 16 * 1024 * 1024))));
                }
                catch { prepared.Dispose(); throw; }
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
                await RequireSourceCurrentAsync(source, binding, trust, token).ConfigureAwait(false);
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

    private byte[] TranscriptSuccessor(DaprReplayOperationRecord prior, DaprReplayPageLedger ledger, EventBufferBudget budget)
    {
        var entry = new DaprLogicalPageTranscriptEntry(ledger.PageOrdinal, ledger.Generation, ledger.RequestHash,
            ledger.PreviousAccumulator, ledger.Accumulator, ledger.StartSequence, ledger.EndSequence, ledger.Count,
            ledger.ResponseHash, ledger.IsFinal, Memory(ledger.PriorStateHash), Memory(ledger.CanonicalStateHash),
            ledger.PreviousEffectiveChainHash!, ledger.EffectiveChainHash!);
        return _anchoredIntake is null
            ? DaprLogicalReplayCommitmentCodec.TranscriptStep(_tenant, _operation, prior.SourceBindingHash,
                prior.RegistryFingerprint, Memory(prior.ReconstructionBindingHash), prior.TranscriptHash!, entry, budget)
            : DaprLogicalReplayCommitmentCodec.AnchoredTranscriptStep(_tenant, _operation,
                _anchoredIntake.SelectionHash, prior.TranscriptHash!, entry, budget);
    }

    private void AttachCommandProof(DaprReplayPreparedTransition prepared, DaprReplayOperationRecord expected,
        DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, ECDsa key, CancellationToken token)
    {
        EventBufferBudget budget = prepared.CommitmentBudget!;
        (byte[] prefix, byte[] proof) = DaprLogicalReplayCommitmentCodec.FinalProofHashes(prepared.Response.Bytes.Span, budget, token);
        (DateTimeOffset issued, DateTimeOffset expires) = trust.CommandValidity(token);
        var claim = new DaprLogicalCommandStateClaim(_tenant, _command!.Domain, _command.AggregateId, binding.AggregateType,
            _command.CommandType, _command.MessageId, _commandRouteHash!, expected.SourceBindingHash, binding.ActorHead,
            binding.TargetSequence, expected.RegistryFingerprint, expected.ReconstructionBindingHash!, _operation,
            expected.OwnerId, expected.Generation, expected.PageOrdinal, expected.CompletedSequence, expected.Accumulator,
            prefix, proof, SHA256.HashData(prepared.Response.Bytes.Span), expected.CanonicalStateHash!,
            expected.EffectiveChainHash!, expected.TranscriptHash!, issued, expires);
        using var encoded = new DaprLogicalEncodedClaim(() => DaprLogicalCommandStateCodec.Encode(claim), budget, token);
        using DaprLogicalSignedClaim signed = trust.Sign(7, encoded.Bytes.Span, key, budget, token);
        using var framed = new DaprLogicalEncodedClaim(() => DaprLogicalCommandStateProofCodec.Encode(signed), budget, token, 4 * 1024 * 1024 + 1024);
        DaprLogicalResponseOwner owned = DaprLogicalResponseOwner.Capture(framed.Bytes.Span, budget);
        try { prepared.AttachCommandProof(owned, budget.Reserve(checked(owned.Bytes.Length * 4 + 4096))); }
        catch { owned.Dispose(); throw; }
    }

    private async Task<bool> RequireCommittedChainAsync(DaprReplayOperationRecord record, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        if (!await RequireAnchorParticipantAsync(token).ConfigureAwait(false))
        {
            return false;
        }
        byte[] accumulator = _initialAnchor is null ? DaprLogicalClaimCodec.ComputeGenesis(record.SourceBindingHash, record.RegistryFingerprint)
            : _initialAnchor.AccumulatorSeed.ToArray();
        long end = _anchoredIntake?.Selection.CoveredSequence ?? 0;
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
            if (_anchoredIntake is not null && !priorStateHash.AsSpan().SequenceEqual(_anchoredIntake.Selection.CanonicalStateHash.Span))
            {
                return false;
            }
            if (record.PageOrdinal == 0 && !OptionalEqual(priorStateHash, record.CanonicalStateHash))
            {
                return false;
            }
        }

        byte[] effective = _initialAnchor is null
            ? DaprLogicalReplayCommitmentCodec.EffectiveGenesis(record.SourceBindingHash, record.RegistryFingerprint, Memory(record.ReconstructionBindingHash), budget)
            : _initialAnchor.EffectiveSeed.ToArray();
        byte[] transcript = _initialAnchor is null
            ? DaprLogicalReplayCommitmentCodec.TranscriptGenesis(_tenant, _operation, record.SourceBindingHash,
                record.RegistryFingerprint, Memory(record.ReconstructionBindingHash), Memory(priorStateHash), Memory(record.CommandRouteHash), budget)
            : DaprLogicalReplayCommitmentCodec.AnchoredTranscriptGenesis(_tenant, _operation, _anchoredIntake!.SelectionHash,
                _initialAnchor.TranscriptSeed, priorStateHash!, budget);
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
                || !OptionalEqual(ledger.AnchorSelectionHash, record.AnchorSelectionHash)
                || (_anchoredIntake is not null && ledger.Count == 0
                    ? ordinal != 1 || !IsAnchoredZeroTail(ledger, binding)
                    : binding.TargetSequence == 0 ? ordinal != 1 || ledger.StartSequence != 1 || ledger.EndSequence != 0 || ledger.Count != 0
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
                if (!OptionalEqual(ledger.PreviousEffectiveChainHash, effective) || !OptionalEqual(ledger.PreviousTranscriptHash, transcript))
                { return false; }
                byte[] successor = DaprLogicalReplayCommitmentCodec.EffectiveSuccessor(response.Bytes.Span, binding, trust,
                    accumulator, effective, Memory(record.ReconstructionBindingHash), budget, token, _anchoredIntake);
                byte[] nextTranscript = TranscriptSuccessor(record with { TranscriptHash = transcript }, ledger, budget);
                if (!OptionalEqual(ledger.EffectiveChainHash, successor) || !OptionalEqual(ledger.TranscriptHash, nextTranscript)
                    || (_command is null || !ledger.IsFinal ? ledger.CommandProofHash is not null : !OptionalEqual(ledger.CommandProofHash, record.CommandProofHash)))
                { return false; }
                effective = successor; transcript = nextTranscript;
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
                if (ledger.IsFinal && _command is not null)
                {
                    ConditionalValue<byte[]> completed = await _stateManager.TryGetStateAsync<byte[]>(CommandProofKey, token).ConfigureAwait(false);
                    if (!completed.HasValue || completed.Value is null) { return false; }
                    using DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> verified = DaprLogicalCommandStateProofCodec.Verify(completed.Value, trust, budget, token);
                    (byte[] prefix, byte[] proof) = DaprLogicalReplayCommitmentCodec.FinalProofHashes(response.Bytes.Span, budget, token);
                    if (!verified.Value.FinalPrefixHash.Span.SequenceEqual(prefix) || !verified.Value.FinalSourceProofHash.Span.SequenceEqual(proof)
                        || !verified.Value.FinalResponseHash.Span.SequenceEqual(ledger.ResponseHash)) { return false; }
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

        ConditionalValue<DaprReplayPageLedger> next = await _stateManager.TryGetStateAsync<DaprReplayPageLedger>(LedgerKey(record.PageOrdinal + 1), token).ConfigureAwait(false);
        if (next.HasValue) { return false; }
        foreach (string key in new[] { ResponseKey(record.PageOrdinal + 1), StateKey(record.PageOrdinal + 1) })
        {
            if ((await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false)).HasValue) { return false; }
        }
        if (!record.IsComplete)
        {
            foreach (string key in new[] { FinalKey, FinalStateKey, CommandProofKey })
            {
                if ((await _stateManager.TryGetStateAsync<byte[]>(key, token).ConfigureAwait(false)).HasValue) { return false; }
            }
        }

        ConditionalValue<byte[]> commandProof = await _stateManager.TryGetStateAsync<byte[]>(CommandProofKey, token).ConfigureAwait(false);
        if (_command is null || !record.IsComplete)
        {
            if (commandProof.HasValue) { return false; }
        }
        else
        {
            if (!commandProof.HasValue || commandProof.Value is null || commandProof.Value.Length > DaprLogicalCommandStateProofCodec.MaximumProofBytes
                || !OptionalEqual(SHA256.HashData(commandProof.Value), record.CommandProofHash)) { return false; }
            using DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> verified = DaprLogicalCommandStateProofCodec.Verify(commandProof.Value, trust, budget, token);
            RequireCompletedClaim(verified.Value, record, binding);
        }
        return end == record.CompletedSequence && accumulator.AsSpan().SequenceEqual(record.Accumulator)
            && OptionalEqual(priorStateHash, record.CanonicalStateHash) && OptionalEqual(effective, record.EffectiveChainHash)
            && OptionalEqual(transcript, record.TranscriptHash);
    }

    private void RequireCompletedClaim(DaprLogicalCommandStateClaim claim, DaprReplayOperationRecord record, DaprLogicalSourceBinding source)
    {
        if (!record.IsComplete || claim.TenantId != _tenant || claim.Domain != _command!.Domain || claim.AggregateId != _command.AggregateId
            || claim.AggregateType != source.AggregateType || claim.CommandType != _command.CommandType || claim.MessageId != _command.MessageId
            || !claim.CommandHash.Span.SequenceEqual(_commandRouteHash) || !claim.SourceBindingHash.Span.SequenceEqual(record.SourceBindingHash)
            || claim.ActorHead != source.ActorHead || claim.TargetSequence != source.TargetSequence
            || !claim.RegistryFingerprint.Span.SequenceEqual(record.RegistryFingerprint)
            || !claim.ReconstructionBindingHash.Span.SequenceEqual(record.ReconstructionBindingHash)
            || claim.OperationId != _operation || claim.OwnerId != record.OwnerId || claim.Generation != record.Generation
            || claim.PageOrdinal != record.PageOrdinal || claim.CompletedSequence != record.CompletedSequence
            || !claim.Accumulator.Span.SequenceEqual(record.Accumulator) || !claim.CanonicalStateHash.Span.SequenceEqual(record.CanonicalStateHash)
            || !claim.EffectiveChainHash.Span.SequenceEqual(record.EffectiveChainHash) || !claim.TranscriptHash.Span.SequenceEqual(record.TranscriptHash))
        { throw new InvalidOperationException("ProofMismatch: completed command state differs from the actual operation."); }
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
        DaprReplayOperationRecord expected, DaprReplayPageLedger ledger, DaprLogicalResponseOwner response, bool isFinal, EventBufferBudget budget, DaprLogicalResponseOwner? canonicalState, EventBufferBudget? stateBudget, DaprReplayPreparedTransition prepared)
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

            byte[] participants = await ComputeParticipantDigestAsync(expected, null, recovery.Token).ConfigureAwait(false);
            bool complete = participants.AsSpan().SequenceEqual(prepared.ExpectedParticipantDigest) && stateComplete && Exact(actual, expected) && actualLedger.HasValue && Exact(actualLedger.Value, ledger)
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
            byte[] previousParticipants = await ComputeParticipantDigestAsync(prior, null, recovery.Token).ConfigureAwait(false);
            bool absent = previousParticipants.AsSpan().SequenceEqual(prepared.PriorParticipantDigest) && predecessor && Exact(actual, prior) && !actualLedger.HasValue && !blob.HasValue && !final.HasValue && !state.HasValue && !finalState.HasValue;
            await _stateManager.ClearCacheAsync(recovery.Token).ConfigureAwait(false);
            return new(absent ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
        }
        catch
        {
            return new(DaprReplayCommitOutcome.Indeterminate, prior.Generation, null, false);
        }
    }

    private void RequirePinned(ReadOnlySpan<byte> bytes, DaprReplayPageLedger ledger, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        using DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> verified = DaprLogicalReplayResponseVerifier.Verify(bytes, binding, trust, ledger.PreviousAccumulator, budget, token, _anchoredIntake);
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
            || (record.PageOrdinal == 0 && record.CompletedSequence != (_anchoredIntake?.Selection.CoveredSequence ?? 0))
            || (target == 0 && record.PageOrdinal > 1)
            || (target > 0 && record.PageOrdinal > record.CompletedSequence)
            || record.EffectiveChainHash is not { Length: 32 } || record.TranscriptHash is not { Length: 32 }
            || !OptionalEqual(record.CommandRouteHash, _commandRouteHash)
            || record.LogicalEvidenceModelId != (_anchoredIntake is null ? DaprLogicalSourceBinding.ModelId : DaprLogicalReplayAnchorCodec.ModelId)
            || record.AnchorCoveredSequence != _anchoredIntake?.Selection.CoveredSequence
            || !OptionalEqual(record.AnchorSelectionHash, _anchoredIntake?.SelectionHash.ToArray())
            || (_command is null || !record.IsComplete ? record.CommandProofHash is not null : record.CommandProofHash is not { Length: 32 })
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
        if (_anchoredIntake is not null)
        {
            return DaprLogicalReplayCommitmentCodec.AnchoredRequestHash(_tenant, _operation, owner, generation,
                page, request, maxCount, _anchoredIntake.SelectionHash, source, registry, _bufferBudget);
        }
        using EventBufferReservation working = _bufferBudget.Reserve(8192 + 256);
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

    private string StateKey(long ordinal) => (_anchoredIntake is null ? "logical-replay:state:" : "logical-replay:anchored:state:") + ordinal.ToString(CultureInfo.InvariantCulture);
    private static ReadOnlyMemory<byte>? Memory(byte[]? value)
    { if (value is null) { return null; } return new ReadOnlyMemory<byte>(value); }

    private void RequireCommandSource(DaprLogicalSourceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_command is not null && (_command.TenantId != binding.Identity.TenantId || _command.Domain != binding.Identity.Domain
            || _command.AggregateId != binding.Identity.AggregateId
            || !DaprLogicalCommandStateCodec.CommandHash(_command, _bufferBudget, token).AsSpan().SequenceEqual(_commandRouteHash)))
        { throw new InvalidOperationException("AddressMismatch: command is outside its private exact source route."); }
    }
    private static bool OptionalEqual(byte[]? left, byte[]? right) => left is null || right is null ? left is null && right is null : left.AsSpan().SequenceEqual(right);
    private string LedgerKey(long ordinal) => (_anchoredIntake is null ? "logical-replay:ledger:" : "logical-replay:anchored:ledger:") + ordinal.ToString(CultureInfo.InvariantCulture);
    private string ResponseKey(long ordinal) => (_anchoredIntake is null ? "logical-replay:response:" : "logical-replay:anchored:response:") + ordinal.ToString(CultureInfo.InvariantCulture);
    private static bool Exact(DaprReplayOperationRecord? left, DaprReplayOperationRecord? right)
        => left is null || right is null ? left is null && right is null
            : left.TenantId == right.TenantId && left.OperationId == right.OperationId && left.OwnerId == right.OwnerId
            && left.Generation == right.Generation && left.TargetSequence == right.TargetSequence && left.PageOrdinal == right.PageOrdinal
            && left.CompletedSequence == right.CompletedSequence && left.IsComplete == right.IsComplete
            && left.SourceBindingHash.AsSpan().SequenceEqual(right.SourceBindingHash) && left.RegistryFingerprint.AsSpan().SequenceEqual(right.RegistryFingerprint)
            && left.Accumulator.AsSpan().SequenceEqual(right.Accumulator)
            && OptionalEqual(left.ReconstructionBindingHash, right.ReconstructionBindingHash) && OptionalEqual(left.CanonicalStateHash, right.CanonicalStateHash)
            && OptionalEqual(left.EffectiveChainHash, right.EffectiveChainHash) && OptionalEqual(left.TranscriptHash, right.TranscriptHash)
            && OptionalEqual(left.CommandRouteHash, right.CommandRouteHash) && OptionalEqual(left.CommandProofHash, right.CommandProofHash)
            && left.LogicalEvidenceModelId == right.LogicalEvidenceModelId && left.AnchorCoveredSequence == right.AnchorCoveredSequence
            && OptionalEqual(left.AnchorSelectionHash, right.AnchorSelectionHash);
    private static bool Exact(DaprReplayPageLedger? left, DaprReplayPageLedger? right)
        => left is null || right is null ? left is null && right is null
            : left.PageOrdinal == right.PageOrdinal && left.Generation == right.Generation && left.StartSequence == right.StartSequence
            && left.EndSequence == right.EndSequence && left.Count == right.Count && left.IsFinal == right.IsFinal
            && left.RequestHash.AsSpan().SequenceEqual(right.RequestHash) && left.PreviousAccumulator.AsSpan().SequenceEqual(right.PreviousAccumulator)
            && left.Accumulator.AsSpan().SequenceEqual(right.Accumulator) && left.ResponseHash.AsSpan().SequenceEqual(right.ResponseHash)
            && OptionalEqual(left.PriorStateHash, right.PriorStateHash) && OptionalEqual(left.CanonicalStateHash, right.CanonicalStateHash)
            && OptionalEqual(left.PreviousEffectiveChainHash, right.PreviousEffectiveChainHash) && OptionalEqual(left.EffectiveChainHash, right.EffectiveChainHash)
            && OptionalEqual(left.PreviousTranscriptHash, right.PreviousTranscriptHash) && OptionalEqual(left.TranscriptHash, right.TranscriptHash)
            && OptionalEqual(left.CommandProofHash, right.CommandProofHash) && OptionalEqual(left.AnchorSelectionHash, right.AnchorSelectionHash);

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
            if (_command is not null)
            {
                CryptographicOperations.ZeroMemory(_command.Payload);
                ((Dictionary<string, string>?)_command.Extensions)?.Clear();
                _command = null;
            }
            _commandCharge?.Dispose();
            _anchorImage?.Dispose();
            _anchorImage = null;
            // Decoded hashes are slices of the initial owned image; drop all slice/string references before releasing it.
            _anchoredIntake = null;
            _initialAnchor?.Dispose();
            _initialAnchor = null;
            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
