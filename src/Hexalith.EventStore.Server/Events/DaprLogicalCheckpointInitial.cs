using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Owns immutable checkpoint prior bytes and unsigned range preparation, granting no consumer authority.</summary>
internal sealed class DaprLogicalCheckpointInitial : IDisposable
{
    private DaprLogicalCheckpointCandidate? _candidate;
    private DaprLogicalProjectionFold? _fold;
    private DaprLogicalSourceBinding? _checkpoint;
    private DaprLogicalSourceBinding? _requested;
    private ImmutablePayload? _canonical;
    private DaprLogicalResponseOwner? _selection;
    private EventBufferReservation? _metadata;
    private byte[]? _selectionHash;
    private readonly CancellationToken _token;
    private DaprLogicalCheckpointInitial(DaprLogicalCheckpointCandidate candidate, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding checkpoint, DaprLogicalSourceBinding requested, EventBufferReservation metadata, CancellationToken token)
    {
        _candidate = candidate;
        _fold = fold;
        _checkpoint = checkpoint;
        _requested = requested;
        _metadata = metadata;
        _token = token;
        Budget = candidate.Budget;
    }

    /// <summary>Gets the exact borrowed composed budget retaining every private copy.</summary>
    internal EventBufferBudget Budget { get; }

    /// <summary>Gets private immutable canonical prior bytes after local pins, never handler admission.</summary>
    internal IReadOnlyPayload CanonicalState
    {
        get
        {
            RequirePins(_token);
            return _canonical!;
        }
    }

    /// <summary>Gets the unsigned private selection image after complete local pins.</summary>
    internal ReadOnlyMemory<byte> SelectionImage
    {
        get
        {
            RequirePins(_token);
            return _selection!.Bytes;
        }
    }

    /// <summary>Takes the actual candidate, including on refusal, and validates private canonical adoption.</summary>
    internal static async Task<DaprLogicalCheckpointInitial> AdoptAsync(string model, DaprLogicalCheckpointCandidate candidate, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding checkpoint, DaprLogicalSourceBinding requested, EventBufferBudget budget, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        DaprLogicalCheckpointInitial? captured = null;
        EventBufferReservation? metadata = null;
        bool returned = false;
        try
        {
            candidate.RequireToken(token);
            ArgumentNullException.ThrowIfNull(fold);
            ArgumentNullException.ThrowIfNull(checkpoint);
            ArgumentNullException.ThrowIfNull(requested);
            ArgumentNullException.ThrowIfNull(budget);
            if (model != DaprLogicalCheckpointInitialCodec.ModelId || !ReferenceEquals(candidate.Budget, budget) || !ReferenceEquals(fold.Budget, budget))
            {
                throw new InvalidOperationException("CheckpointPreparationHold: unsigned policy and exact parent required.");
            }

            RequireRequested(checkpoint, requested, budget);
            candidate.RequireBinding(fold, checkpoint, token);
            await candidate.RequireCurrentAsync(token).ConfigureAwait(false);
            metadata = budget.Reserve(12288);
            captured = new DaprLogicalCheckpointInitial(candidate, fold, checkpoint, requested, metadata, token);
            metadata = null;
            captured._canonical = CaptureCanonical(candidate, budget, token);
            DaprLogicalCheckpointInitialSelection fields = captured.CaptureSelection();
            try
            {
                using EventBufferReservation encoding = budget.Reserve(checked(2 * DaprLogicalCheckpointInitialCodec.Measure(fields) + 256));
                byte[] image = DaprLogicalCheckpointInitialCodec.Encode(fields);
                try
                {
                    captured._selection = DaprLogicalResponseOwner.Capture(image, budget);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(image);
                }
            }
            finally
            {
                ClearFields(fields);
            }

            captured._selectionHash = SHA256.HashData(captured._selection.Bytes.Span);
            using EventBufferBudget partition = budget.CreatePartition(fold.Reconstruction.GetCommandCapacity(captured._canonical.Length, captured._selection.Bytes.Length, 0));
            using EventBufferReservation graphs = fold.Reconstruction.ReserveCommandGraphs(partition);
            _ = await fold.Reconstruction.ReadCommandStateAsync(captured.CanonicalState, partition, captured.RequireCurrentAsync, token).ConfigureAwait(false);
            await captured.RequireCurrentAsync(token).ConfigureAwait(false);
            captured.RequirePins(token);
            returned = true;
            return captured;
        }
        finally
        {
            metadata?.Dispose();
            try
            {
                candidate.RequireToken(token);
                if (!returned)
                {
                    captured?.Dispose();
                    candidate.Dispose();
                }
            }
            catch
            {
                captured?.Dispose();
                candidate.Dispose();
                throw;
            }
        }
    }

    /// <summary>Requires original cancellation, exact candidate/declaration/source and every owned image before work.</summary>
    internal void RequirePins(CancellationToken token)
    {
        try
        {
            _token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_candidate is null || _canonical is null || _selection is null || _metadata is null, this);
            _candidate.RequireToken(token);
            RequireRequested(_checkpoint!, _requested!, Budget);
            if (!SHA256.HashData(_selection.Bytes.Span).AsSpan().SequenceEqual(_selectionHash))
            {
                throw new InvalidOperationException("CheckpointPreparationHold: complete private selection changed.");
            }

            byte[] stateHash = _canonical.ComputeSha256();
            try
            {
                if (!stateHash.AsSpan().SequenceEqual(DaprLogicalCheckpointInitialCodec.Decode(_selection.Bytes).StateHash.Span))
                {
                    throw new InvalidOperationException("CheckpointPreparationHold: private canonical prior changed.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(stateHash);
            }

            _candidate.RequireBinding(_fold!, _checkpoint!, token);
            DaprLogicalCheckpointInitialSelection expected = CaptureSelection();
            try
            {
                using EventBufferReservation encoding = Budget.Reserve(checked(2 * DaprLogicalCheckpointInitialCodec.Measure(expected) + 256));
                byte[] image = DaprLogicalCheckpointInitialCodec.Encode(expected);
                try
                {
                    if (!image.AsSpan().SequenceEqual(_selection.Bytes.Span))
                    {
                        throw new InvalidOperationException("CheckpointPreparationHold: exact canonical/source/fold/image pins changed.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(image);
                }
            }
            finally
            {
                ClearFields(expected);
            }
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Rechecks actual current root/origin/canonical state with local immutable pins on both sides of its await.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins(token);
        try
        {
            await _candidate!.RequireCurrentAsync(token).ConfigureAwait(false);
            RequirePins(token);
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Returns only charged unsigned range bytes after fresh read-only admission; it executes no tail or handler.</summary>
    internal async Task<DaprLogicalResponseOwner> PrepareRangeAsync(DaprLogicalCheckpointRangeKind kind, int maximumCount, CancellationToken token)
    {
        RequirePins(token);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCount, 256);
        DaprLogicalResponseOwner? captured = null;
        bool returned = false;
        try
        {
            DaprLogicalCheckpointInitialSelection selection = DaprLogicalCheckpointInitialCodec.Decode(_selection!.Bytes);
            long covered = selection.CoveredSequence;
            long start = covered == long.MaxValue ? long.MaxValue : covered + 1;
            int count = kind == DaprLogicalCheckpointRangeKind.CurrentZero ? 0 : checked((int)Math.Min(maximumCount, selection.TargetSequence - covered));
            long end = count == 0 ? covered : checked(start + (count - 1));
            var range = new DaprLogicalCheckpointRangePreparation(_selectionHash!, selection.RequestedSourceHash, covered, selection.ActorHead, selection.TargetSequence, start, end, count, kind, DaprLogicalCheckpointInitialCodec.ModelId);
            int capacity = DaprLogicalCheckpointRangeCodec.Measure(range);
            await RequireCurrentAsync(token).ConfigureAwait(false);
            using EventBufferReservation encoding = Budget.Reserve(checked(2 * capacity + 256));
            byte[] image = DaprLogicalCheckpointRangeCodec.Encode(range);
            try
            {
                captured = DaprLogicalResponseOwner.Capture(image, Budget);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(image);
            }

            await RequireCurrentAsync(token).ConfigureAwait(false);
            RequirePins(token);
            returned = true;
            return captured;
        }
        finally
        {
            try
            {
                _token.ThrowIfCancellationRequested();
                if (!returned)
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

    private DaprLogicalCheckpointInitialSelection CaptureSelection()
    {
        byte[]? [] hashes = new byte[8][];
        try
        {
            hashes[0] = DaprLogicalClaimCodec.ComputeSourceBindingHash(_checkpoint!, Budget);
            hashes[1] = DaprLogicalClaimCodec.ComputeSourceBindingHash(_requested!, Budget);
            hashes[2] = _fold!.Fingerprint.ToArray();
            hashes[3] = _fold.Reconstruction.Fingerprint.ToArray();
            hashes[4] = Convert.FromHexString(_fold.Reconstruction.Evolution.RegistryFingerprint);
            hashes[5] = _canonical!.ComputeSha256();
            hashes[6] = SHA256.HashData(_candidate!.Root.Span);
            hashes[7] = SHA256.HashData(_candidate.Witness.Span);
            return new DaprLogicalCheckpointInitialSelection(hashes[0], hashes[1], hashes[2], hashes[3], hashes[4], hashes[5], hashes[6], hashes[7], _checkpoint!.TargetSequence, _requested!.ActorHead, _requested.TargetSequence, DaprLogicalCheckpointInitialCodec.ModelId);
        }
        catch
        {
            foreach (byte[]? hash in hashes)
            {
                if (hash is not null)
                {
                    CryptographicOperations.ZeroMemory(hash);
                }
            }

            throw;
        }
    }

    private static void RequireRequested(DaprLogicalSourceBinding checkpoint, DaprLogicalSourceBinding requested, EventBufferBudget budget)
    {
        if (checkpoint.TargetSequence < 1 || checkpoint.RetainedFloor != 1 || requested.RetainedFloor != 1 || requested.TargetSequence < checkpoint.TargetSequence || requested.TargetSequence > requested.ActorHead)
        {
            throw new InvalidOperationException("CheckpointPreparationHold: invalid k/H/T or retained floor.");
        }

        byte[] original = DaprLogicalClaimCodec.ComputeSourceBindingHash(checkpoint, budget);
        byte[]? normalized = null;
        try
        {
            normalized = DaprLogicalClaimCodec.ComputeSourceBindingHash(requested with { TargetSequence = checkpoint.TargetSequence }, budget);
            if (!original.AsSpan().SequenceEqual(normalized))
            {
                throw new InvalidOperationException("CheckpointPreparationHold: requested source may change only target under the same actual head.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
            if (normalized is not null)
            {
                CryptographicOperations.ZeroMemory(normalized);
            }
        }
    }

    private static ImmutablePayload CaptureCanonical(DaprLogicalCheckpointCandidate candidate, EventBufferBudget budget, CancellationToken token)
    {
        ReadOnlyMemory<byte> source = candidate.State;
        EventBufferReservation charge = budget.Reserve(source.Length);
        byte[]? image = null;
        try
        {
            token.ThrowIfCancellationRequested();
            image = source.ToArray();
            return new ImmutablePayload(image, image.Length, token, charge);
        }
        catch
        {
            if (image is not null)
            {
                CryptographicOperations.ZeroMemory(image);
            }

            charge.Dispose();
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static void ClearFields(DaprLogicalCheckpointInitialSelection fields)
    {
        foreach (ReadOnlyMemory<byte> hash in new[]
        {
            fields.CheckpointSourceHash,
            fields.RequestedSourceHash,
            fields.FoldHash,
            fields.ReconstructionHash,
            fields.RegistryFingerprint,
            fields.StateHash,
            fields.RootHash,
            fields.WitnessHash
        }

        )
        {
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(hash, out ArraySegment<byte> segment))
            {
                CryptographicOperations.ZeroMemory(segment.AsSpan());
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Interlocked.Exchange(ref _candidate, null)?.Dispose();
        Interlocked.Exchange(ref _canonical, null)?.Dispose();
        Interlocked.Exchange(ref _selection, null)?.Dispose();
        _fold = null;
        _checkpoint = null;
        _requested = null;
        byte[]? hash = Interlocked.Exchange(ref _selectionHash, null);
        if (hash is not null)
        {
            CryptographicOperations.ZeroMemory(hash);
        }

        Interlocked.Exchange(ref _metadata, null)?.Dispose();
    }
}
