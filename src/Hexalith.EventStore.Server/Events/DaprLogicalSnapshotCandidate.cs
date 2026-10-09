using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Owns an admitted private snapshot candidate without granting any replay continuation or command authority.</summary>
internal sealed class DaprLogicalSnapshotCandidate : IDisposable
{
    private ImmutablePayload? _state;
    private DaprLogicalResponseOwner? _witness;
    private DaprLogicalReplayAnchorOrigin? _origin;
    private EventBufferBudget? _partition;
    private EventBufferReservation? _decoding;
    private Func<CancellationToken, Task>? _fence;
    private byte[]? _sourcePin;
    private byte[]? _witnessPin;
    private readonly CancellationToken _originatingToken;
    /// <summary>Takes exclusive private storage and retained working ownership after the final actual fence.</summary>
    internal DaprLogicalSnapshotCandidate(ImmutablePayload state, DaprLogicalResponseOwner witness, DaprLogicalReplayAnchorOrigin origin, EventBufferBudget partition, EventBufferReservation decoding, long coveredSequence, long target, byte[] sourcePin, byte[] witnessPin, CancellationToken originatingToken, Func<CancellationToken, Task> fence, EventBufferBudget budget)
    {
        _state = state;
        _witness = witness;
        _origin = origin;
        _partition = partition;
        _decoding = decoding;
        _fence = fence;
        _sourcePin = sourcePin;
        _witnessPin = witnessPin;
        _originatingToken = originatingToken;
        Budget = budget;
        CoveredSequence = coveredSequence;
        TargetSequence = target;
        StartSequence = DaprLogicalSnapshotCodec.TailStart(coveredSequence);
    }

    /// <summary>Gets the witnessed covered sequence; it is not a committed page pointer.</summary>
    internal long CoveredSequence { get; }
    /// <summary>Gets the original immutable requested target.</summary>
    internal long TargetSequence { get; }
    /// <summary>Gets the candidate tail start without incrementing long.MaxValue.</summary>
    internal long StartSequence { get; }
    /// <summary>Gets whether the exact head snapshot proposes zero tail events.</summary>
    internal bool HasZeroTail => CoveredSequence == TargetSequence;

    /// <summary>Gets the exact composed parent that owns all retained candidate and origin capacity.</summary>
    internal EventBufferBudget Budget { get; }

    /// <summary>Gets private witness bytes only within the originating owned lifetime for distinct anchor adoption.</summary>
    internal ReadOnlyMemory<byte> WitnessBytes(CancellationToken token)
    {
        _originatingToken.ThrowIfCancellationRequested();
        if (token != _originatingToken)
        {
            throw new InvalidOperationException("SnapshotCapabilityHold: original token changed.");
        }

        return (_witness ?? throw new ObjectDisposedException(nameof(DaprLogicalSnapshotCandidate))).Bytes;
    }

    /// <summary>Requires a consumer to use the exact requested fixed source captured by this candidate.</summary>
    internal void RequireSourceBinding(DaprLogicalSourceBinding source, CancellationToken token)
    {
        _originatingToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_sourcePin is null || _witness is null, this);
        if (token != _originatingToken || !DaprLogicalClaimCodec.ComputeSourceBindingHash(source, Budget).AsSpan().SequenceEqual(_sourcePin))
        {
            throw new InvalidOperationException("SnapshotCapabilityHold: exact candidate source or originating token changed.");
        }
    }

    /// <summary>Gets canonical private bytes within this candidate's owned lifetime.</summary>
    internal IReadOnlyPayload CanonicalState
    {
        get
        {
            _originatingToken.ThrowIfCancellationRequested();
            return _state ?? throw new ObjectDisposedException(nameof(DaprLogicalSnapshotCandidate));
        }
    }

    /// <summary>Rechecks actual pair, completed history and local pins before a future consumer can use this candidate.</summary>
    internal Task RequireCurrentAsync(CancellationToken token)
    {
        _originatingToken.ThrowIfCancellationRequested();
        return (_fence ?? throw new ObjectDisposedException(nameof(DaprLogicalSnapshotCandidate)))(token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _fence = null;
        byte[]? source = Interlocked.Exchange(ref _sourcePin, null);
        byte[]? witness = Interlocked.Exchange(ref _witnessPin, null);
        if (source is not null)
        {
            CryptographicOperations.ZeroMemory(source);
        }

        if (witness is not null)
        {
            CryptographicOperations.ZeroMemory(witness);
        }

        Interlocked.Exchange(ref _state, null)?.Dispose();
        Interlocked.Exchange(ref _witness, null)?.Dispose();
        Interlocked.Exchange(ref _origin, null)?.Dispose();
        Interlocked.Exchange(ref _decoding, null)?.Dispose();
        Interlocked.Exchange(ref _partition, null)?.Dispose();
    }
}
