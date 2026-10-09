using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Retains detached privately pinned checkpoint images, granting no projection or replay authority.</summary>
internal sealed class DaprLogicalCheckpointCandidate : IDisposable
{
    private DaprLogicalResponseOwner? []? _images;
    private byte[][]? _imagePins;
    private byte[]? _sourcePin;
    private byte[]? _foldPin;
    private EventBufferReservation? _metadata;
    private DaprLogicalProjectionFold? _fold;
    private DaprLogicalSourceBinding? _binding;
    private DaprLogicalClaimTrust? _trust;
    private Func<DaprLogicalProjectionFold, bool>? _exactDeclaration;
    private Func<CancellationToken, Task<DaprLogicalCheckpointCandidate>>? _refresh;
    private readonly CancellationToken _token;
    /// <summary>Gets the distinct internal candidate policy; it selects no signing purpose or replay parser.</summary>
    internal const string ModelId = "dapr-actor-logical-checkpoint-candidate-v1";
    /// <summary>Copies all three proven images only after retaining metadata and every overlapping private copy.</summary>
    internal DaprLogicalCheckpointCandidate(DaprLogicalCheckpointWrite write, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, Func<CancellationToken, Task<DaprLogicalCheckpointCandidate>> refresh, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(fold);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(refresh);
        _token = token;
        Budget = write.Budget;
        try
        {
            _metadata = Budget.Reserve(8192);
            _fold = fold;
            _binding = binding;
            _trust = trust;
            _exactDeclaration = actual => ReferenceEquals(actual, fold) && ReferenceEquals(actual.Budget, Budget);
            _refresh = refresh;
            _sourcePin = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, Budget);
            _foldPin = fold.Fingerprint.ToArray();
            _images = new DaprLogicalResponseOwner? [3];
            _imagePins = new byte[3][];
            for (int index = 0; index < 3; index++)
            {
                byte[] image = write.Array(index);
                _images[index] = DaprLogicalResponseOwner.Capture(image, Budget);
                _imagePins[index] = SHA256.HashData(image);
            }

            RequirePins(token);
        }
        catch
        {
            Dispose();
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    /// <summary>Gets the exact borrowed composed parent retaining all candidate images.</summary>
    internal EventBufferBudget Budget { get; }
    /// <summary>Gets private canonical candidate bytes within this lifetime, without granting consumer admission.</summary>
    internal ReadOnlyMemory<byte> State => Image(0);
    /// <summary>Gets the complete private root image after local pins.</summary>
    internal ReadOnlyMemory<byte> Root => Image(1);
    /// <summary>Gets the complete private nine-field witness image after local pins.</summary>
    internal ReadOnlyMemory<byte> Witness => Image(2);

    /// <summary>Gets the candidate's exact fixed covered target; it is not committed replay progress.</summary>
    internal long CoveredSequence
    {
        get
        {
            RequirePins(_token);
            return _binding!.TargetSequence;
        }
    }

    /// <summary>Requires the captured declaration object and complete original source before unsigned initial adoption.</summary>
    internal void RequireBinding(DaprLogicalProjectionFold fold, DaprLogicalSourceBinding checkpoint, CancellationToken token)
    {
        RequireToken(token);
        ObjectDisposedException.ThrowIf(_metadata is null || _images is null || _refresh is null, this);
        if (!ReferenceEquals(fold, _fold))
        {
            throw new InvalidOperationException("CheckpointCandidateHold: exact captured fold instance required.");
        }

        byte[] hash = DaprLogicalClaimCodec.ComputeSourceBindingHash(checkpoint, Budget);
        try
        {
            if (!hash.AsSpan().SequenceEqual(_sourcePin))
            {
                throw new InvalidOperationException("CheckpointCandidateHold: complete original checkpoint source changed.");
            }

            RequirePins(token);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
            _token.ThrowIfCancellationRequested();
        }
    }

    private ReadOnlyMemory<byte> Image(int index)
    {
        RequirePins(_token);
        return _images![index]!.Bytes;
    }

    /// <summary>Preserves original-token precedence at ownership cleanup without invoking artifact or source callbacks.</summary>
    internal void RequireToken(CancellationToken token)
    {
        _token.ThrowIfCancellationRequested();
        if (token != _token)
        {
            throw new InvalidOperationException("CheckpointCandidateHold: exact originating token required.");
        }
    }

    /// <summary>Refuses changed private images, exact declaration/source or original token before subsequent work.</summary>
    internal void RequirePins(CancellationToken token)
    {
        try
        {
            _token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_metadata is null || _images is null || _refresh is null, this);
            if (token != _token)
            {
                throw new InvalidOperationException("CheckpointCandidateHold: original token changed.");
            }

            if (!_exactDeclaration!(_fold!))
            {
                throw new InvalidOperationException("CheckpointCandidateHold: exact declaration instance changed.");
            }

            _fold!.RequireCurrent(_binding!, token);
            _trust!.RequireCurrent(token);
            byte[] source = DaprLogicalClaimCodec.ComputeSourceBindingHash(_binding!, Budget);
            try
            {
                if (!source.AsSpan().SequenceEqual(_sourcePin) || !_fold.Fingerprint.Span.SequenceEqual(_foldPin))
                {
                    throw new InvalidOperationException("CheckpointCandidateHold: exact captured source or fold changed.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(source);
            }

            for (int index = 0; index < 3; index++)
            {
                if (!SHA256.HashData(_images[index]!.Bytes.Span).AsSpan().SequenceEqual(_imagePins![index]))
                {
                    throw new InvalidOperationException("CheckpointCandidateHold: complete private participant changed.");
                }
            }
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Captures a fresh exact read-only decision and compares all three images on both sides of its actual await.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins(token);
        try
        {
            using DaprLogicalCheckpointCandidate fresh = await _refresh!(token).ConfigureAwait(false);
            RequirePins(token);
            fresh.RequirePins(token);
            for (int index = 0; index < 3; index++)
            {
                if (!_images![index]!.Bytes.Span.SequenceEqual(fresh._images![index]!.Bytes.Span))
                {
                    throw new InvalidOperationException("CheckpointCandidateHold: fresh exact participant set differs.");
                }
            }

            RequirePins(token);
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _refresh = null;
        _exactDeclaration = null;
        _fold = null;
        _binding = null;
        _trust = null;
        DaprLogicalResponseOwner? []? images = Interlocked.Exchange(ref _images, null);
        if (images is not null)
        {
            foreach (DaprLogicalResponseOwner? image in images)
            {
                image?.Dispose();
            }
        }

        byte[][]? pins = Interlocked.Exchange(ref _imagePins, null);
        if (pins is not null)
        {
            foreach (byte[]? pin in pins)
            {
                if (pin is not null)
                {
                    CryptographicOperations.ZeroMemory(pin);
                }
            }
        }

        byte[]? source = Interlocked.Exchange(ref _sourcePin, null);
        byte[]? fold = Interlocked.Exchange(ref _foldPin, null);
        if (source is not null)
        {
            CryptographicOperations.ZeroMemory(source);
        }

        if (fold is not null)
        {
            CryptographicOperations.ZeroMemory(fold);
        }

        Interlocked.Exchange(ref _metadata, null)?.Dispose();
    }
}
