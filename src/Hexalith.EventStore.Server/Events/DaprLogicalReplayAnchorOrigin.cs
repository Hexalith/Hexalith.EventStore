using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Owns canonical state captured from the actual complete operation and retains its fresh owner fence.</summary>
internal sealed class DaprLogicalReplayAnchorOrigin : IDisposable
{
    private DaprLogicalResponseOwner? _state;
    private DaprReplayOperationRecord? _record;
    private Func<CancellationToken, Task>? _fence;
    private EventBufferReservation? _charge;
    private readonly CancellationToken _originatingToken;
    private readonly string _serializer;
    private readonly string _domain;
    private readonly string _aggregateId;
    private readonly string _aggregateType;
    /// <summary>Takes private state, record and pin capacity from the freshly verified actual operation owner.</summary>
    internal DaprLogicalReplayAnchorOrigin(DaprLogicalResponseOwner state, DaprReplayOperationRecord record, string serializer, DaprLogicalSourceBinding source, EventBufferBudget budget, Func<CancellationToken, Task> actualFence, EventBufferReservation charge, CancellationToken token)
    {
        Budget = budget;
        _state = state;
        _record = record;
        _serializer = serializer;
        _domain = source.Identity.Domain;
        _aggregateId = source.Identity.AggregateId;
        _aggregateType = source.AggregateType;
        _fence = actualFence;
        _charge = charge;
        _originatingToken = token;
    }

    /// <summary>Gets the exact shared parent capacity scope used for this captured origin.</summary>
    internal EventBufferBudget Budget { get; }

    /// <summary>Gets privately owned canonical state after this origin's actual currentness check.</summary>
    internal ReadOnlyMemory<byte> State
    {
        get
        {
            RequirePins(_originatingToken);
            return _state!.Bytes;
        }
    }

    /// <summary>Creates untrusted paired witness fields from this actual captured completed origin.</summary>
    internal DaprLogicalSnapshotWitness CreateWitness(string storageKey, string witnessKey)
    {
        RequirePins(_originatingToken);
        DaprReplayOperationRecord record = _record!;
        return new DaprLogicalSnapshotWitness(record.TenantId, _domain, _aggregateId, _aggregateType, record.SourceBindingHash.ToArray(), record.CompletedSequence, storageKey, witnessKey, record.CanonicalStateHash!.ToArray(), record.CanonicalStateHash!.ToArray(), record.RegistryFingerprint.ToArray(), record.ReconstructionBindingHash!.ToArray(), record.Accumulator.ToArray(), record.OperationId, record.Generation, record.TranscriptHash!.ToArray(), record.EffectiveChainHash!.ToArray(), "plaintext-canonical-v1", DaprLogicalSnapshotCodec.SnapshotModel, _serializer);
    }

    /// <summary>Compares every completed provenance pin; a caller witness cannot supply missing history.</summary>
    internal bool Matches(DaprLogicalSnapshotWitness witness)
    {
        ArgumentNullException.ThrowIfNull(witness);
        RequirePins(_originatingToken);
        DaprReplayOperationRecord record = _record!;
        return witness.TenantId == record.TenantId && witness.Domain == _domain && witness.AggregateId == _aggregateId && witness.AggregateType == _aggregateType && witness.CoveredSequence == record.CompletedSequence && witness.OperationId == record.OperationId && witness.Generation == record.Generation && witness.SerializerId == _serializer && witness.SourceBindingHash.Span.SequenceEqual(record.SourceBindingHash) && witness.FoldedHash.Span.SequenceEqual(record.CanonicalStateHash) && witness.RegistryFingerprint.Span.SequenceEqual(record.RegistryFingerprint) && witness.ReconstructionBindingHash.Span.SequenceEqual(record.ReconstructionBindingHash) && witness.Accumulator.Span.SequenceEqual(record.Accumulator) && witness.TranscriptHash.Span.SequenceEqual(record.TranscriptHash) && witness.EffectiveChainHash.Span.SequenceEqual(record.EffectiveChainHash);
    }

    /// <summary>Rechecks private bytes on both sides of full actual completed-history readback.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins(token);
        try
        {
            await _fence!(token).ConfigureAwait(false);
        }
        finally
        {
            _originatingToken.ThrowIfCancellationRequested();
        }

        RequirePins(token);
    }

    private void RequirePins(CancellationToken token)
    {
        _originatingToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_state is null || _record is null || _fence is null, this);
        if (token != _originatingToken)
        {
            throw new InvalidOperationException("AnchorOriginHold: the originating token changed.");
        }

        if (!SHA256.HashData(_state.Bytes.Span).AsSpan().SequenceEqual(_record.CanonicalStateHash))
        {
            throw new InvalidOperationException("AnchorOriginHold: private canonical origin changed.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _fence = null;
        Interlocked.Exchange(ref _state, null)?.Dispose();
        DaprReplayOperationRecord? record = Interlocked.Exchange(ref _record, null);
        if (record is not null)
        {
            foreach (byte[]? hash in new[]
            {
                record.SourceBindingHash,
                record.RegistryFingerprint,
                record.Accumulator,
                record.ReconstructionBindingHash,
                record.CanonicalStateHash,
                record.EffectiveChainHash,
                record.TranscriptHash,
                record.CommandRouteHash,
                record.CommandProofHash
            }

            )
            {
                if (hash is not null)
                {
                    CryptographicOperations.ZeroMemory(hash);
                }
            }
        }

        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
