using System.Security.Cryptography;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Owns a private canonical initial anchor and distinct history seeds without granting page or command authority.</summary>
internal sealed class DaprLogicalReplayInitialAnchor : IDisposable
{
    private DaprLogicalSnapshotCandidate? _candidate;
    private ImmutablePayload? _canonical;
    private DaprLogicalResponseOwner? _selection;
    private EventBufferReservation? _metadataCharge;
    private readonly RegisteredLogicalReplayBinding _reconstruction;
    private readonly CancellationToken _token;
    private byte[]? _selectionHash;
    private byte[]? _accumulator;
    private byte[]? _effective;
    private byte[]? _transcript;
    private DaprLogicalReplayInitialAnchor(DaprLogicalSnapshotCandidate candidate, ImmutablePayload canonical, DaprLogicalResponseOwner selection, EventBufferReservation charge, RegisteredLogicalReplayBinding reconstruction, CancellationToken token, byte[] selectionHash, byte[] accumulator, byte[] effective, byte[] transcript)
    {
        _candidate = candidate;
        _canonical = canonical;
        _selection = selection;
        _metadataCharge = charge;
        _reconstruction = reconstruction;
        _token = token;
        _selectionHash = selectionHash;
        _accumulator = accumulator;
        _effective = effective;
        _transcript = transcript;
    }

    /// <summary>Gets privately owned initial canonical bytes after local pins, without advancing any durable operation.</summary>
    internal IReadOnlyPayload CanonicalState
    {
        get
        {
            RequirePins();
            return _canonical!;
        }
    }

    /// <summary>Gets the strict privately captured selection image within this owned lifetime.</summary>
    internal ReadOnlyMemory<byte> SelectionImage
    {
        get
        {
            RequirePins();
            return _selection!.Bytes;
        }
    }

    /// <summary>Gets the distinct private logical accumulator seed, never an event-only v1 predecessor.</summary>
    internal ReadOnlyMemory<byte> AccumulatorSeed
    {
        get
        {
            RequirePins();
            return _accumulator!;
        }
    }

    /// <summary>Gets the distinct private complete-effective-history seed.</summary>
    internal ReadOnlyMemory<byte> EffectiveSeed
    {
        get
        {
            RequirePins();
            return _effective!;
        }
    }

    /// <summary>Gets the distinct private covered-transcript seed.</summary>
    internal ReadOnlyMemory<byte> TranscriptSeed
    {
        get
        {
            RequirePins();
            return _transcript!;
        }
    }

    /// <summary>Gets the exact composed budget retained by this admitted private anchor.</summary>
    internal EventBufferBudget Budget
    {
        get
        {
            RequirePins();
            return _candidate!.Budget;
        }
    }

    /// <summary>Captures bounded decoded private selection fields only after their exact image pins.</summary>
    internal DaprLogicalReplayAnchorSelection Selection
    {
        get
        {
            RequirePins();
            return DaprLogicalReplayAnchorCodec.Decode(_selection!.Bytes);
        }
    }

    /// <summary>Gets the exact retained selection image digest after local pins.</summary>
    internal ReadOnlyMemory<byte> SelectionHash
    {
        get
        {
            RequirePins();
            return _selectionHash!;
        }
    }

    /// <summary>Requires the exact same reconstruction instance and original requested-source route.</summary>
    internal void RequireBinding(RegisteredLogicalReplayBinding reconstruction, DaprLogicalSourceBinding source, CancellationToken token)
    {
        RequirePins();
        if (!ReferenceEquals(reconstruction, _reconstruction) || token != _token)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: exact reconstruction and originating token are required.");
        }
        _candidate!.RequireSourceBinding(source, token);
        reconstruction.RequireSource(source, token);
    }

    /// <summary>Requires the original exact reconstruction instance and originating token before ownership transfer.</summary>
    internal void RequireReconstruction(RegisteredLogicalReplayBinding reconstruction, CancellationToken token)
    {
        RequirePins();
        if (!ReferenceEquals(reconstruction, _reconstruction) || token != _token)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: exact reconstruction and originating token changed.");
        }
    }

    /// <summary>Takes the admitted candidate, including on refusal, and creates only separately selected private initial ownership.</summary>
    internal static async Task<DaprLogicalReplayInitialAnchor> AdoptAsync(string model, DaprLogicalSnapshotCandidate candidate, DaprLogicalSourceBinding source, RegisteredLogicalReplayBinding reconstruction, EventBufferBudget budget, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        DaprLogicalReplayInitialAnchor? captured = null;
        ImmutablePayload? canonical = null;
        DaprLogicalResponseOwner? image = null;
        EventBufferReservation? metadata = null;
        byte[]? selectionHash = null;
        byte[]? accumulator = null;
        byte[]? effective = null;
        byte[]? transcript = null;
        bool successfulReturn = false;
        try
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(reconstruction);
            ArgumentNullException.ThrowIfNull(budget);
            if (model != DaprLogicalReplayAnchorCodec.ModelId || !ReferenceEquals(candidate.Budget, budget))
            {
                throw new InvalidOperationException("AnchorCapabilityHold: distinct model and exact parent capacity are required.");
            }

            reconstruction.RequireSource(source, token);
            candidate.RequireSourceBinding(source, token);
            await candidate.RequireCurrentAsync(token).ConfigureAwait(false);
            metadata = budget.Reserve(checked(4 * DaprLogicalReplayAnchorCodec.MaximumBytes + 4096));
            DaprLogicalSnapshotWitness witness = DaprLogicalSnapshotCodec.DecodeSnapshot(candidate.WitnessBytes(token));
            var selection = new DaprLogicalReplayAnchorSelection(source.Identity.TenantId, source.Identity.Domain, source.Identity.AggregateId, source.AggregateType, DaprLogicalClaimCodec.ComputeSourceBindingHash(source, budget), witness.RegistryFingerprint, witness.ReconstructionBindingHash, SHA256.HashData(candidate.WitnessBytes(token).Span), witness.FoldedHash, witness.Accumulator, witness.EffectiveChainHash, witness.TranscriptHash, witness.CoveredSequence, source.ActorHead, source.TargetSequence, model);
            if (witness.TenantId != selection.TenantId || witness.Domain != selection.Domain || witness.AggregateId != selection.AggregateId || witness.AggregateType != selection.AggregateType || !witness.RegistryFingerprint.Span.SequenceEqual(Convert.FromHexString(reconstruction.Evolution.RegistryFingerprint)) || !witness.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span) || !witness.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(source with { TargetSequence = witness.CoveredSequence }, budget)))
            {
                throw new InvalidOperationException("AnchorCapabilityHold: private candidate and requested reconstruction/source disagree.");
            }

            int capacity = DaprLogicalReplayAnchorCodec.Measure(selection);
            using (EventBufferReservation encoding = budget.Reserve(checked(2 * capacity + 256)))
            {
                byte[] bytes = DaprLogicalReplayAnchorCodec.Encode(selection);
                try
                {
                    image = DaprLogicalResponseOwner.Capture(bytes, budget);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }

            canonical = ImmutablePayload.CopyFrom(candidate.CanonicalState, budget, token);
            selectionHash = SHA256.HashData(image.Bytes.Span);
            accumulator = DaprLogicalReplayAnchorCodec.AccumulatorSeed(selectionHash, selection.CoveredAccumulator, budget);
            effective = DaprLogicalReplayAnchorCodec.EffectiveSeed(selectionHash, selection.CoveredEffectiveChain, budget);
            transcript = DaprLogicalReplayAnchorCodec.TranscriptSeed(selectionHash, selection.CoveredTranscript, budget);
            captured = new DaprLogicalReplayInitialAnchor(candidate, canonical, image, metadata, reconstruction, token, selectionHash, accumulator, effective, transcript);
            canonical = null;
            image = null;
            metadata = null;
            selectionHash = null;
            accumulator = null;
            effective = null;
            transcript = null;
            using EventBufferBudget partition = budget.CreatePartition(reconstruction.GetCommandCapacity(captured.CanonicalState.Length, captured.SelectionImage.Length, 0));
            using EventBufferReservation graphs = reconstruction.ReserveCommandGraphs(partition);
            _ = await reconstruction.ReadCommandStateAsync(captured.CanonicalState, partition, captured.RequireCurrentAsync, token).ConfigureAwait(false);
            await captured.RequireCurrentAsync(token).ConfigureAwait(false);
            successfulReturn = true;
            return captured;
        }
        finally
        {
            canonical?.Dispose();
            image?.Dispose();
            metadata?.Dispose();
            foreach (byte[]? hash in new[]
            {
                selectionHash,
                accumulator,
                effective,
                transcript
            }

            )
            {
                if (hash is not null)
                {
                    CryptographicOperations.ZeroMemory(hash);
                }
            }

            try
            {
                token.ThrowIfCancellationRequested();
                if (!successfulReturn)
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

    /// <summary>Rechecks actual pair/origin and local canonical/selection/history pins on both sides of an actual await.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins();
        if (token != _token)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: originating token changed.");
        }

        try
        {
            await _candidate!.RequireCurrentAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _token.ThrowIfCancellationRequested();
        }

        RequirePins();
    }

    private void RequirePins()
    {
        _token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_candidate is null || _canonical is null || _selection is null, this);
        _reconstruction.RequireCurrent(_token);
        if (!SHA256.HashData(_selection.Bytes.Span).AsSpan().SequenceEqual(_selectionHash))
        {
            throw new InvalidOperationException("AnchorCapabilityHold: private selection changed.");
        }

        DaprLogicalReplayAnchorSelection fields = DaprLogicalReplayAnchorCodec.Decode(_selection.Bytes);
        if (!_canonical.ComputeSha256().AsSpan().SequenceEqual(fields.CanonicalStateHash.Span) || !DaprLogicalReplayAnchorCodec.AccumulatorSeed(_selectionHash!, fields.CoveredAccumulator, _candidate.Budget).AsSpan().SequenceEqual(_accumulator) || !DaprLogicalReplayAnchorCodec.EffectiveSeed(_selectionHash!, fields.CoveredEffectiveChain, _candidate.Budget).AsSpan().SequenceEqual(_effective) || !DaprLogicalReplayAnchorCodec.TranscriptSeed(_selectionHash!, fields.CoveredTranscript, _candidate.Budget).AsSpan().SequenceEqual(_transcript))
        {
            throw new InvalidOperationException("AnchorCapabilityHold: private canonical state or history seed changed.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Interlocked.Exchange(ref _candidate, null)?.Dispose();
        Interlocked.Exchange(ref _canonical, null)?.Dispose();
        Interlocked.Exchange(ref _selection, null)?.Dispose();
        foreach (byte[]? hash in new[]
        {
            _selectionHash,
            _accumulator,
            _effective,
            _transcript
        }

        )
        {
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }

        _selectionHash = null;
        _accumulator = null;
        _effective = null;
        _transcript = null;
        Interlocked.Exchange(ref _metadataCharge, null)?.Dispose();
    }
}
