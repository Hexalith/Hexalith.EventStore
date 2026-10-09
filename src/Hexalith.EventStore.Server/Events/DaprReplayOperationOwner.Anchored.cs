using System.Security.Cryptography;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Supplies explicit distinct anchored genesis and currentness to the actual replay-owner protocol.</summary>
internal sealed partial class DaprReplayOperationOwner
{
    private DaprLogicalReplayInitialAnchor? _initialAnchor;
    private DaprLogicalResponseOwner? _anchorImage;
    private DaprLogicalAnchoredIntake? _anchoredIntake;
    private readonly CancellationToken _anchoredOriginatingToken;
    private readonly bool _isAnchoredOperation;
    private const string AnchorKey = "logical-replay:anchored:selection:v1";
    /// <summary>Takes private initial ownership into a separately selected reconstruction-only operation on the exact parent budget.</summary>
    internal DaprReplayOperationOwner(IActorStateManager stateManager, string tenant, string operation, RegisteredLogicalReplayBinding reconstruction, DaprLogicalReplayInitialAnchor initial, DaprLogicalAnchoredClaimTrust trust, CancellationToken token) : this(stateManager, tenant, operation, reconstruction: reconstruction)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _anchoredOriginatingToken = token;
        _isAnchoredOperation = true;
        try
        {
            initial.RequireReconstruction(reconstruction, token);
            ArgumentNullException.ThrowIfNull(trust);
            DaprLogicalReplayAnchorSelection selection = initial.Selection;
            if (selection.TenantId != tenant)
            {
                throw new InvalidOperationException("AnchorCapabilityHold: initial anchor is outside its actual operation tenant.");
            }

            _bufferBudget = initial.Budget;
            _anchoredIntake = new DaprLogicalAnchoredIntake(trust, selection, initial.SelectionHash);
            _anchorImage = DaprLogicalResponseOwner.Capture(initial.SelectionImage.Span, _bufferBudget);
            _initialAnchor = initial;
            token.ThrowIfCancellationRequested();
        }
        catch
        {
            _anchorImage?.Dispose();
            _anchorImage = null;
            _anchoredIntake = null;
            initial.Dispose();
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    private void RequireAnchoredEntry(CancellationToken token)
    {
        _anchoredOriginatingToken.ThrowIfCancellationRequested();
        if (_isAnchoredOperation && token != _anchoredOriginatingToken)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: the request must retain the exact originating operation token.");
        }
    }

    private async Task RequireSourceCurrentAsync(DaprLogicalReplaySource source, DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        try
        {
            RequireAnchorPins(binding, trust, token);
            if (_initialAnchor is not null)
            {
                await _initialAnchor.RequireCurrentAsync(token).ConfigureAwait(false);
            }

            await source.RequireCurrentAsync(binding, trust, token).ConfigureAwait(false);
            if (_initialAnchor is not null)
            {
                await _initialAnchor.RequireCurrentAsync(token).ConfigureAwait(false);
            }

            RequireAnchorPins(binding, trust, token);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }
    }

    private void RequireAnchorPins(DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_anchoredIntake is null)
        {
            return;
        }

        (_initialAnchor ?? throw new ObjectDisposedException(nameof(DaprReplayOperationOwner))).RequireBinding(_reconstruction!, binding, token);
        _anchoredIntake.Trust.RequireCurrent(trust, token);
        using EventBufferReservation encoding = _bufferBudget.Reserve(2 * DaprLogicalReplayAnchorCodec.MaximumBytes + 256);
        byte[] captured = DaprLogicalReplayAnchorCodec.Encode(_anchoredIntake.Selection);
        try
        {
            if (!_initialAnchor.SelectionHash.Span.SequenceEqual(_anchoredIntake.SelectionHash.Span) || !captured.AsSpan().SequenceEqual(_anchorImage!.Bytes.Span) || !_anchorImage.Bytes.Span.SequenceEqual(_initialAnchor.SelectionImage.Span))
            {
                throw new InvalidOperationException("AnchorCapabilityHold: retained exact initial selection changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(captured);
        }
    }

    private async Task<bool> RequireAnchorParticipantAsync(CancellationToken token)
    {
        if (_anchoredIntake is null)
        {
            return true;
        }

        ConditionalValue<byte[]> value = await _stateManager.TryGetStateAsync<byte[]>(AnchorKey, token).ConfigureAwait(false);
        return value.HasValue && value.Value is not null && value.Value.Length <= DaprLogicalReplayAnchorCodec.MaximumBytes && SHA256.HashData(value.Value).AsSpan().SequenceEqual(_anchoredIntake.SelectionHash.Span) && value.Value.AsSpan().SequenceEqual(_anchorImage!.Bytes.Span);
    }

    private byte[] EncodeLedger(DaprReplayPageLedger ledger)
    {
        return _anchoredIntake is null ? DaprReplayLedgerCodec.Encode(ledger) : DaprAnchoredReplayLedgerCodec.Encode(ledger, _bufferBudget);
    }

    private bool IsAnchoredZeroTail(DaprReplayPageLedger ledger, DaprLogicalSourceBinding source)
    {
        long covered = _anchoredIntake!.Selection.CoveredSequence;
        return covered == source.TargetSequence && covered == source.ActorHead && ledger.EndSequence == covered && ledger.StartSequence == (covered == long.MaxValue ? long.MaxValue : covered + 1);
    }
}
