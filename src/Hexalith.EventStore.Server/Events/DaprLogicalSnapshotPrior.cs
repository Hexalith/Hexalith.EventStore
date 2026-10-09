using System.Security.Cryptography;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Retains admitted prior witness decoding and its actual completed origin inside an existing serialized issuer decision.</summary>
internal sealed class DaprLogicalSnapshotPrior : IDisposable
{
    /// <summary>Identifies the explicit replacement policy without changing the snapshot pair encoding.</summary>
    internal const string ModelId = "dapr-actor-logical-snapshot-replacement-v1";
    private DaprLogicalReplayAnchorOrigin? _origin;
    private EventBufferReservation? _decoding;
    private DaprLogicalSnapshotWitness? _fields;
    private DaprLogicalResponseOwner? _state;
    private DaprLogicalResponseOwner? _witness;
    private byte[]? _witnessPin;
    private Action? _pins;
    private readonly CancellationToken _token;
    private DaprLogicalSnapshotPrior(DaprLogicalReplayAnchorOrigin origin, EventBufferReservation decoding, DaprLogicalSnapshotWitness fields, DaprLogicalResponseOwner state, DaprLogicalResponseOwner witness, Action pins, CancellationToken token)
    {
        _origin = origin;
        _decoding = decoding;
        _fields = fields;
        _state = state;
        _witness = witness;
        _witnessPin = SHA256.HashData(witness.Bytes.Span);
        _pins = pins;
        _token = token;
    }

    /// <summary>Admits a strictly older exact pair through full actual history and a canonical roundtrip before any stage.</summary>
    internal static async Task<DaprLogicalSnapshotPrior> AcquireAsync(DaprLogicalResponseOwner state, DaprLogicalResponseOwner witness, DaprLogicalSnapshotWrite write, DaprLogicalSourceBinding binding, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireOrigin, Func<CancellationToken, Task> sourceFence, Action pins, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        EventBufferBudget budget = write.Budget;
        EventBufferReservation? decoding = null;
        DaprLogicalReplayAnchorOrigin? origin = null;
        DaprLogicalSnapshotPrior? captured = null;
        bool returned = false;
        try
        {
            pins();
            decoding = budget.Reserve(checked(4 * witness.Bytes.Length + 4096));
            DaprLogicalSnapshotWitness fields;
            try
            {
                fields = DaprLogicalSnapshotCodec.DecodeSnapshot(witness.Bytes);
            }
            catch (ArgumentException error)
            {
                throw new InvalidOperationException("SnapshotPriorHold: prior witness is not canonical evidence.", error);
            }

            if (fields.CoveredSequence >= binding.TargetSequence || fields.TenantId != binding.Identity.TenantId || fields.Domain != binding.Identity.Domain || fields.AggregateId != binding.Identity.AggregateId || fields.AggregateType != binding.AggregateType || fields.StorageKey != write.StorageKey || fields.WitnessKey != write.WitnessKey || fields.SerializerId != reconstruction.SerializerId || !fields.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span) || !fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) || !fields.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(binding with { TargetSequence = fields.CoveredSequence }, budget)) || !fields.StorageHash.Span.SequenceEqual(SHA256.HashData(state.Bytes.Span)) || !fields.FoldedHash.Span.SequenceEqual(fields.StorageHash.Span))
            {
                throw new InvalidOperationException("SnapshotPriorHold: prior pair is not an exact strictly older fixed-source image.");
            }

            await sourceFence(token).ConfigureAwait(false);
            try
            {
                origin = await acquireOrigin(fields.CoveredSequence, budget, token).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            pins();
            if (origin is null || !ReferenceEquals(origin.Budget, budget))
            {
                throw new InvalidOperationException("SnapshotPriorHold: exact parent-bound prior completed origin is missing.");
            }

            captured = new DaprLogicalSnapshotPrior(origin, decoding, fields, state, witness, pins, token);
            origin = null;
            decoding = null;
            await captured.RequireCurrentAsync(token).ConfigureAwait(false);
            async Task FenceAsync(CancellationToken boundary)
            {
                await captured.RequireSourceAndOriginAsync(sourceFence, boundary).ConfigureAwait(false);
            }

            try
            {
                await DaprLogicalSnapshotCanonical.RequireAsync(state.Bytes, witness.Bytes.Length, reconstruction, budget, FenceAsync, token).ConfigureAwait(false);
            }
            catch (LogicalReplayCanonicalMismatchException error)
            {
                throw new InvalidOperationException("SnapshotPriorHold: prior state is not canonical under the pinned codec.", error);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            await sourceFence(token).ConfigureAwait(false);
            await captured.RequireCurrentAsync(token).ConfigureAwait(false);
            pins();
            token.ThrowIfCancellationRequested();
            returned = true;
            return captured;
        }
        finally
        {
            origin?.Dispose();
            decoding?.Dispose();
            if (!returned)
            {
                captured?.Dispose();
            }

            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                captured?.Dispose();
                throw;
            }
        }
    }

    /// <summary>Checks exact private prior bytes and full actual origin on both sides of its asynchronous owner readback.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins(token);
        try
        {
            await _origin!.RequireCurrentAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }

        RequirePins(token);
    }

    /// <summary>Refuses foreign or cancelled originating tokens before any addressed source callback, then fences both source and actual origin.</summary>
    internal async Task RequireSourceAndOriginAsync(Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        RequirePins(token);
        try
        {
            await sourceFence(token).ConfigureAwait(false);
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }

        RequirePins(token);
        await RequireCurrentAsync(token).ConfigureAwait(false);
        RequirePins(token);
    }

    private void RequirePins(CancellationToken token)
    {
        _token.ThrowIfCancellationRequested();
        if (token != _token)
        {
            throw new InvalidOperationException("SnapshotPriorHold: original decision token changed.");
        }

        ObjectDisposedException.ThrowIf(_origin is null, this);
        _pins!();
        if (!SHA256.HashData(_witness!.Bytes.Span).AsSpan().SequenceEqual(_witnessPin) || !_origin.Matches(_fields!) || !_origin.State.Span.SequenceEqual(_state!.Bytes.Span))
        {
            throw new InvalidOperationException("SnapshotPriorHold: prior pair differs from its current actual completed origin.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Interlocked.Exchange(ref _origin, null)?.Dispose();
        _fields = null;
        _state = null;
        _witness = null;
        if (_witnessPin is not null)
        {
            CryptographicOperations.ZeroMemory(_witnessPin);
        }

        _witnessPin = null;
        _pins = null;
        Interlocked.Exchange(ref _decoding, null)?.Dispose();
    }
}
