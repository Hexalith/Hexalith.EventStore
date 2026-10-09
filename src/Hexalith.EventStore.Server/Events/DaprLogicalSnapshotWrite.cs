using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Retains exact desired and predecessor snapshot images while actor cache/save ownership is uncertain.</summary>
internal sealed class DaprLogicalSnapshotWrite : IDisposable
{
    private EventBufferReservation? _materialization;
    private readonly List<DaprLogicalResponseOwner> _reads = [];
    private DaprLogicalResponseOwner? _state;
    private DaprLogicalResponseOwner? _witness;
    private DaprLogicalResponseOwner? _priorState;
    private DaprLogicalResponseOwner? _priorWitness;
    private byte[]? _statePin;
    private byte[]? _witnessPin;
    private byte[]? _priorStatePin;
    private byte[]? _priorWitnessPin;
    /// <summary>Admits all bounded typed materialization and witness encoding workspace before actor reads or allocations.</summary>
    internal DaprLogicalSnapshotWrite(EventBufferBudget budget, int maximumStateBytes, string storageKey, string witnessKey, string policyId = DaprLogicalSnapshotCodec.SnapshotModel)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (maximumStateBytes is < 1 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        }

        if (policyId is not DaprLogicalSnapshotCodec.SnapshotModel and not DaprLogicalSnapshotPrior.ModelId and not DaprLogicalSnapshotRewitnessPolicy.ModelId)
        {
            throw new ArgumentException("SnapshotRecoveryHold: exact supported write policy is required.", nameof(policyId));
        }

        _materialization = budget.Reserve(checked(4 * maximumStateBytes + 6 * 64 * 1024 + 4096));
        Budget = budget;
        MaximumStateBytes = maximumStateBytes;
        StorageKey = storageKey;
        WitnessKey = witnessKey;
        PolicyId = policyId;
    }

    /// <summary>Gets the exact composed parent retained across save and reconciliation.</summary>
    internal EventBufferBudget Budget { get; }
    /// <summary>Gets the admitted typed state ceiling.</summary>
    internal int MaximumStateBytes { get; }
    /// <summary>Gets the separate canonical application key.</summary>
    internal string StorageKey { get; }
    /// <summary>Gets the exact separate witness key.</summary>
    internal string WitnessKey { get; }
    /// <summary>Gets the immutable policy that owns this pending attempt; another entry cannot reconcile it.</summary>
    internal string PolicyId { get; }
    /// <summary>Gets whether the desired pair was fully captured before staging.</summary>
    internal bool HasDesired => _state is not null && _witness is not null;
    /// <summary>Gets whether this pending write passed explicit older-pair replacement admission.</summary>
    internal bool ReplacementAdmitted { get; private set; }
    /// <summary>Gets the sealed predecessor state borrowed only inside the retained write lifetime.</summary>
    internal DaprLogicalResponseOwner PriorState
    {
        get
        {
            RequirePriorPins();
            return _priorState ?? throw new InvalidOperationException("SnapshotPriorHold: retained predecessor state is missing.");
        }
    }

    /// <summary>Gets the sealed predecessor witness borrowed only inside the retained write lifetime.</summary>
    internal DaprLogicalResponseOwner PriorWitness
    {
        get
        {
            RequirePriorPins();
            return _priorWitness ?? throw new InvalidOperationException("SnapshotPriorHold: retained predecessor witness is missing.");
        }
    }

    /// <summary>Retains the recovery obligation only after complete older-pair admission and final pre-stage comparison.</summary>
    internal void MarkReplacementAdmitted()
    {
        _ = PriorState;
        _ = PriorWitness;
        ReplacementAdmitted = true;
    }
    /// <summary>Gets the privately owned canonical image passed to the actor SDK within its charged lifetime.</summary>
    internal byte[] StateArray => Array(_state!);
    /// <summary>Gets the privately owned witness image passed to the actor SDK within its charged lifetime.</summary>
    internal byte[] WitnessArray => Array(_witness!);

    /// <summary>Captures a detached bounded read and retains it until confirmed cache release.</summary>
    internal DaprLogicalResponseOwner CaptureRead(byte[] value, bool witness)
    {
        ObjectDisposedException.ThrowIf(_materialization is null, this);
        if (value.Length > (witness ? 64 * 1024 : MaximumStateBytes))
        {
            throw new InvalidOperationException("ReadableLimit: typed logical snapshot image exceeds its admitted ceiling.");
        }

        DaprLogicalResponseOwner owner = DaprLogicalResponseOwner.Capture(value, Budget);
        _reads.Add(owner);
        return owner;
    }

    /// <summary>Transfers already detached and cache-released read owners into the exact predecessor.</summary>
    internal void SetPrior(DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? witness)
    {
        _priorState = state;
        _priorWitness = witness;
        _priorStatePin = state is null ? null : SHA256.HashData(state.Bytes.Span);
        _priorWitnessPin = witness is null ? null : SHA256.HashData(witness.Bytes.Span);
        if (state is not null)
        {
            _reads.Remove(state);
        }

        if (witness is not null)
        {
            _reads.Remove(witness);
        }
    }

    /// <summary>Captures the exact desired pair before any actor SDK staging callback.</summary>
    internal void SetDesired(ReadOnlySpan<byte> state, ReadOnlySpan<byte> witness)
    {
        if (state.Length > MaximumStateBytes || witness.Length > 64 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical snapshot pair exceeds its admitted ceiling.");
        }

        _state = DaprLogicalResponseOwner.Capture(state, Budget);
        _witness = DaprLogicalResponseOwner.Capture(witness, Budget);
        _statePin = SHA256.HashData(state);
        _witnessPin = SHA256.HashData(witness);
    }

    /// <summary>Classifies only actual exact bytes, independently of trust/callback currentness or save acknowledgement.</summary>
    internal DaprReplayCommitOutcome Classify(DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? witness)
    {
        RequireDesiredPins();
        return HasDesired && Exact(state, _state) && Exact(witness, _witness) ? DaprReplayCommitOutcome.Proven : Exact(state, _priorState) && Exact(witness, _priorWitness) ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate;
    }

    /// <summary>Refuses SDK-retained staging aliases changed across callbacks or actual awaits.</summary>
    internal void RequireDesiredPins()
    {
        ObjectDisposedException.ThrowIf(_materialization is null, this);
        RequirePriorPins();
        if (HasDesired && (!SHA256.HashData(_state!.Bytes.Span).AsSpan().SequenceEqual(_statePin) || !SHA256.HashData(_witness!.Bytes.Span).AsSpan().SequenceEqual(_witnessPin)))
        {
            throw new InvalidOperationException("SnapshotCapabilityHold: privately pinned staging images changed.");
        }
    }

    /// <summary>Refuses changed private predecessor bytes before any callback, staging or classification.</summary>
    internal void RequirePriorPins()
    {
        ObjectDisposedException.ThrowIf(_materialization is null, this);
        if ((_priorState is not null && !SHA256.HashData(_priorState.Bytes.Span).AsSpan().SequenceEqual(_priorStatePin)) || (_priorWitness is not null && !SHA256.HashData(_priorWitness.Bytes.Span).AsSpan().SequenceEqual(_priorWitnessPin)))
        {
            throw new InvalidOperationException("SnapshotPriorHold: privately pinned predecessor images changed.");
        }
    }

    /// <summary>Compares fresh cache-released actual images with the exact sealed predecessor.</summary>
    internal bool MatchesPrior(DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? witness)
    {
        RequirePriorPins();
        return Exact(state, _priorState) && Exact(witness, _priorWitness);
    }

    /// <summary>Requires the freshly captured actual origin to describe this exact pending desired pair.</summary>
    internal bool MatchesOrigin(DaprLogicalReplayAnchorOrigin origin)
    {
        RequireDesiredPins();
        DaprLogicalSnapshotWitness witness = DaprLogicalSnapshotCodec.DecodeSnapshot(_witness!.Bytes);
        return origin.Matches(witness) && origin.State.Span.SequenceEqual(_state!.Bytes.Span);
    }

    /// <summary>Clears detached read copies only after the SDK has confirmed cache release.</summary>
    internal void ReleaseReads()
    {
        foreach (DaprLogicalResponseOwner owner in _reads)
        {
            owner.Dispose();
        }

        _reads.Clear();
    }

    private static bool Exact(DaprLogicalResponseOwner? left, DaprLogicalResponseOwner? right) => left is null ? right is null : right is not null && left.Bytes.Span.SequenceEqual(right.Bytes.Span);
    private static byte[] Array(DaprLogicalResponseOwner owner)
    {
        if (!MemoryMarshal.TryGetArray(owner.Bytes, out ArraySegment<byte> segment) || segment.Offset != 0 || segment.Count != segment.Array!.Length)
        {
            throw new InvalidOperationException("SnapshotCapabilityHold: private staging image has no exact owned array.");
        }

        return segment.Array;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseReads();
        Interlocked.Exchange(ref _state, null)?.Dispose();
        Interlocked.Exchange(ref _witness, null)?.Dispose();
        Interlocked.Exchange(ref _priorState, null)?.Dispose();
        Interlocked.Exchange(ref _priorWitness, null)?.Dispose();
        if (_statePin is not null)
        {
            CryptographicOperations.ZeroMemory(_statePin);
        }

        if (_witnessPin is not null)
        {
            CryptographicOperations.ZeroMemory(_witnessPin);
        }

        if (_priorStatePin is not null)
        {
            CryptographicOperations.ZeroMemory(_priorStatePin);
        }

        if (_priorWitnessPin is not null)
        {
            CryptographicOperations.ZeroMemory(_priorWitnessPin);
        }

        _statePin = null;
        _witnessPin = null;
        _priorStatePin = null;
        _priorWitnessPin = null;
        ReplacementAdmitted = false;
        Interlocked.Exchange(ref _materialization, null)?.Dispose();
    }
}
