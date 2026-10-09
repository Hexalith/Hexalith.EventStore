using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Retains exact initial checkpoint participants and uncertain SDK ownership under one composed parent.</summary>
internal sealed class DaprLogicalCheckpointWrite : IDisposable
{
    private EventBufferReservation? _workspace;
    private readonly List<DaprLogicalResponseOwner> _reads = [];
    private readonly List<DaprLogicalResponseOwner> _desired = [];
    private readonly List<byte[]> _pins = [];
    private byte[]? _sourcePin;
    private byte[]? _foldPin;
    private string[] _keys = [];
    private byte[]? _keysPin;
    /// <summary>Reserves materialization/encoding workspace before allocations and remembers the exact pending admission.</summary>
    internal DaprLogicalCheckpointWrite(DaprLogicalProjectionFold fold, ReadOnlySpan<byte> source, int maximumStateBytes)
    {
        ArgumentNullException.ThrowIfNull(fold);
        if (maximumStateBytes is < 1 or > 8 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        }

        if (source.Length != 32)
        {
            throw new ArgumentException("CheckpointHold: exact source hash required.", nameof(source));
        }

        Budget = fold.Budget;
        MaximumStateBytes = maximumStateBytes;
        Declaration = fold;
        _workspace = Budget.Reserve(checked(4 * maximumStateBytes + 8 * 64 * 1024 + 8192));
        try
        {
            _sourcePin = source.ToArray();
            _foldPin = fold.Fingerprint.ToArray();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the exact shared retained parent.</summary>
    internal EventBufferBudget Budget { get; }
    /// <summary>Gets the conservative canonical state ceiling.</summary>
    internal int MaximumStateBytes { get; }
    /// <summary>Gets the exact local declaration owning this pending attempt.</summary>
    internal DaprLogicalProjectionFold Declaration { get; }

    /// <summary>Gets the ordered immutable state/current pointer/checkpoint application keys.</summary>
    internal string Key(int index)
    {
        RequirePins();
        return _keys[index];
    }

    /// <summary>Gets whether complete desired participants were captured.</summary>
    internal bool HasDesired => _desired.Count == 3;

    /// <summary>Refuses another scope, declaration or admission ceiling before actual pending readback.</summary>
    internal void RequireAdmission(DaprLogicalProjectionFold fold, ReadOnlySpan<byte> source, int maximumStateBytes)
    {
        RequirePins();
        if (!ReferenceEquals(fold, Declaration) || maximumStateBytes != MaximumStateBytes || !source.SequenceEqual(_sourcePin) || !fold.Fingerprint.Span.SequenceEqual(_foldPin))
        {
            throw new InvalidOperationException("CheckpointRecoveryHold: pending attempt belongs to another exact declaration/source.");
        }
    }

    /// <summary>Privately captures all participants before any actor SDK staging callback.</summary>
    internal void SetDesired(string[] keys, ReadOnlySpan<byte> state, ReadOnlySpan<byte> root, ReadOnlySpan<byte> witness)
    {
        if (HasDesired || keys.Length != 3 || state.Length > MaximumStateBytes || root.Length > 64 * 1024 || witness.Length > 64 * 1024)
        {
            throw new InvalidOperationException("CheckpointHold: bounded exact three-participant image required.");
        }

        _keysPin = DaprLogicalCheckpointCodec.Keys(keys);
        _keys = keys.ToArray();
        CaptureDesired(state);
        CaptureDesired(root);
        CaptureDesired(witness);
    }

    private void CaptureDesired(ReadOnlySpan<byte> image)
    {
        _desired.Add(DaprLogicalResponseOwner.Capture(image, Budget));
        _pins.Add(SHA256.HashData(image));
    }

    /// <summary>Gets an exact privately owned SDK staging array under retained charge.</summary>
    internal byte[] Array(int index)
    {
        RequirePins();
        if (!MemoryMarshal.TryGetArray(_desired[index].Bytes, out ArraySegment<byte> segment) || segment.Offset != 0 || segment.Count != segment.Array!.Length)
        {
            throw new InvalidOperationException("CheckpointHold: exact owned staging array required.");
        }

        return segment.Array;
    }

    /// <summary>Captures detached actual typed images and retains them until SDK cache release.</summary>
    internal DaprLogicalResponseOwner CaptureRead(byte[] bytes, int index)
    {
        RequirePins();
        if (bytes.Length > (index == 0 ? MaximumStateBytes : 64 * 1024))
        {
            throw new InvalidOperationException("ReadableLimit: typed checkpoint row exceeds admitted capacity.");
        }

        DaprLogicalResponseOwner result = DaprLogicalResponseOwner.Capture(bytes, Budget);
        _reads.Add(result);
        return result;
    }

    /// <summary>Classifies exact actual participants independently of trust, acknowledgement or source currentness.</summary>
    internal DaprReplayCommitOutcome Classify(DaprLogicalResponseOwner? [] actual)
    {
        RequirePins();
        if (actual.Length != 3 || !HasDesired)
        {
            throw new InvalidOperationException("CheckpointHold: complete participants required.");
        }

        return actual.Select((image, index) => image is not null && image.Bytes.Span.SequenceEqual(_desired[index].Bytes.Span)).All(exact => exact) ? DaprReplayCommitOutcome.Proven : actual.All(image => image is null) ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate;
    }

    /// <summary>Requires freshly regenerated actual-origin images to match every retained desired byte.</summary>
    internal bool Matches(ReadOnlySpan<byte> state, ReadOnlySpan<byte> root, ReadOnlySpan<byte> witness)
    {
        RequirePins();
        return HasDesired && state.SequenceEqual(_desired[0].Bytes.Span) && root.SequenceEqual(_desired[1].Bytes.Span) && witness.SequenceEqual(_desired[2].Bytes.Span);
    }

    /// <summary>Rechecks private arrays before and after every callback or actual await.</summary>
    internal void RequirePins()
    {
        ObjectDisposedException.ThrowIf(_workspace is null, this);
        if (_keysPin is not null)
        {
            byte[] current = DaprLogicalCheckpointCodec.Keys(_keys);
            try
            {
                if (!current.AsSpan().SequenceEqual(_keysPin))
                {
                    throw new InvalidOperationException("CheckpointHold: participant keys changed.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(current);
            }
        }

        for (int index = 0; index < _pins.Count; index++)
        {
            if (!SHA256.HashData(_desired[index].Bytes.Span).AsSpan().SequenceEqual(_pins[index]))
            {
                throw new InvalidOperationException("CheckpointHold: privately pinned participant changed.");
            }
        }
    }

    /// <summary>Clears detached reads only after the SDK has released cache aliases.</summary>
    internal void ReleaseReads()
    {
        foreach (DaprLogicalResponseOwner image in _reads)
        {
            image.Dispose();
        }

        _reads.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseReads();
        foreach (DaprLogicalResponseOwner image in _desired)
        {
            image.Dispose();
        }

        _desired.Clear();
        foreach (byte[] pin in _pins)
        {
            CryptographicOperations.ZeroMemory(pin);
        }

        _pins.Clear();
        foreach (byte[]? hash in new[]
        {
            Interlocked.Exchange(ref _sourcePin, null),
            Interlocked.Exchange(ref _foldPin, null)
        }

        )
        {
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }

        if (_keysPin is not null)
        {
            CryptographicOperations.ZeroMemory(_keysPin);
        }

        _keysPin = null;
        _keys = [];
        Interlocked.Exchange(ref _workspace, null)?.Dispose();
    }
}
