using System.Security.Cryptography;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Admits separate read-only snapshot candidates through actual pair and completed-origin readback.</summary>
/// <remarks>Production must qualify the supplied common serialized owner fence and SDK materialization bounds.</remarks>
internal sealed class DaprLogicalSnapshotOwner
{
    private readonly IActorStateManager _stateManager;
    private readonly AggregateIdentity _identity;
    private readonly DaprLogicalReplaySource _source;
    private readonly DaprLogicalClaimTrust _trust;
    private readonly RegisteredLogicalReplayBinding _reconstruction;
    private readonly Func<Func<CancellationToken, Task>, CancellationToken, Task> _ownerFence;
    private readonly int _maximumStateBytes;
    /// <summary>Selects the separate dormant model, actual owner, exact supplied codec and serialized decision fence.</summary>
    internal DaprLogicalSnapshotOwner(string model, IActorStateManager stateManager, AggregateIdentity identity, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, int maximumStateBytes, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence)
    {
        if (model != DaprLogicalSnapshotCodec.SnapshotModel)
        {
            throw new ArgumentException("SnapshotCapabilityHold: explicit distinct snapshot model is required.", nameof(model));
        }

        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(ownerFence);
        if (maximumStateBytes is < 1 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        }

        if (!source.OwnsStateManager(stateManager))
        {
            throw new ArgumentException("SnapshotCapabilityHold: snapshot and source must use the same actual actor manager.", nameof(stateManager));
        }

        _stateManager = stateManager;
        _identity = identity;
        _source = source;
        _trust = trust;
        _reconstruction = reconstruction;
        _maximumStateBytes = maximumStateBytes;
        _ownerFence = ownerFence;
    }

    /// <summary>Gets the separate logical application key, preserving legacy snapshot storage.</summary>
    internal string StorageKey => _identity.SnapshotKey + ":logical-v1";
    /// <summary>Gets the exact paired logical witness key.</summary>
    internal string WitnessKey => StorageKey + ":evolution-witness";

    /// <summary>Returns a private candidate or full replay from one, never saving, deleting or applying a tail.</summary>
    internal async Task<DaprLogicalSnapshotCandidate?> AcquireAsync(DaprLogicalSourceBinding binding, bool includeTimeline, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireOrigin, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(acquireOrigin);
        ArgumentNullException.ThrowIfNull(budget);
        if (binding.Identity != _identity)
        {
            throw new InvalidOperationException("AddressMismatch: snapshot owner differs from the source.");
        }

        using EventBufferReservation initialPins = budget.Reserve(256);
        byte[] sourcePin = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
        void RequireLocal()
        {
            token.ThrowIfCancellationRequested();
            _trust.RequireCurrent(token);
            _reconstruction.RequireSource(binding, token);
            if (!DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget).AsSpan().SequenceEqual(sourcePin))
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: private source binding changed.");
            }
        }

        async Task RequireSourceAsync(CancellationToken cancellation)
        {
            token.ThrowIfCancellationRequested();
            if (cancellation != token)
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: original token changed.");
            }

            RequireLocal();
            try
            {
                await _source.RequireCurrentAsync(binding, _trust, token).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            RequireLocal();
        }

        try
        {
            RequireLocal();
        }
        catch
        {
            CryptographicOperations.ZeroMemory(sourcePin);
            throw;
        }

        if (includeTimeline || binding.TargetSequence == 0)
        {
            try
            {
                await RequireSourceAsync(token).ConfigureAwait(false);
                return null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sourcePin);
            }
        }

        DaprLogicalResponseOwner? stored = null;
        DaprLogicalResponseOwner? witness = null;
        DaprLogicalReplayAnchorOrigin? origin = null;
        ImmutablePayload? canonical = null;
        EventBufferBudget? partition = null;
        EventBufferReservation? decoding = null;
        byte[]? witnessPin = null;
        bool transferred = false;
        bool successfulReturn = false;
        DaprLogicalSnapshotCandidate? captured = null;
        Action? requireCapturedPins = null;
        try
        {
            await InOwnerFenceAsync(async cancellation =>
            {
                await RequireSourceAsync(cancellation).ConfigureAwait(false);
                (stored, witness) = await ReadPairAsync(budget, token).ConfigureAwait(false);
                await RequireSourceAsync(cancellation).ConfigureAwait(false);
                if (stored is null || witness is null)
                {
                    return;
                }

                decoding = budget.Reserve(checked(4 * witness.Bytes.Length + 4096));
                DaprLogicalSnapshotWitness fields;
                try
                {
                    fields = DaprLogicalSnapshotCodec.DecodeSnapshot(witness.Bytes);
                }
                catch (ArgumentException)
                {
                    return;
                }

                witnessPin = SHA256.HashData(witness.Bytes.Span);
                long covered = fields.CoveredSequence;
                if (covered > binding.TargetSequence || covered == binding.TargetSequence && covered < binding.ActorHead || fields.TenantId != _identity.TenantId || fields.Domain != _identity.Domain || fields.AggregateId != _identity.AggregateId || fields.AggregateType != binding.AggregateType || fields.StorageKey != StorageKey || fields.WitnessKey != WitnessKey || fields.SerializerId != _reconstruction.SerializerId || !fields.ReconstructionBindingHash.Span.SequenceEqual(_reconstruction.Fingerprint.Span) || !fields.RegistryFingerprint.Span.SequenceEqual(_trust.RegistryFingerprint.Span) || !fields.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(binding with { TargetSequence = covered }, budget)) || !fields.StorageHash.Span.SequenceEqual(SHA256.HashData(stored.Bytes.Span)) || !fields.FoldedHash.Span.SequenceEqual(fields.StorageHash.Span))
                {
                    return;
                }

                await RequireSourceAsync(cancellation).ConfigureAwait(false);
                try
                {
                    origin = await acquireOrigin(covered, budget, token).ConfigureAwait(false);
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                await RequireSourceAsync(cancellation).ConfigureAwait(false);
                if (origin is null || !ReferenceEquals(origin.Budget, budget))
                {
                    return;
                }

                await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                if (!origin.Matches(fields) || !origin.State.Span.SequenceEqual(stored.Bytes.Span))
                {
                    return;
                }

                partition = budget.CreatePartition(_reconstruction.GetCommandCapacity(stored.Bytes.Length, witness.Bytes.Length, 0));
                using EventBufferReservation graphs = _reconstruction.ReserveCommandGraphs(partition);
                EventBufferReservation copy = partition.Reserve(stored.Bytes.Length);
                try
                {
                    canonical = new ImmutablePayload(stored.Bytes.ToArray(), stored.Bytes.Length, token, copy);
                }
                catch
                {
                    copy.Dispose();
                    throw;
                }

                async Task FenceAsync(CancellationToken boundary)
                {
                    await RequireSourceAsync(boundary).ConfigureAwait(false);
                    await origin.RequireCurrentAsync(boundary).ConfigureAwait(false);
                    RequirePairPins(canonical, witness, fields, witnessPin!);
                }

                try
                {
                    _ = await _reconstruction.ReadCommandStateAsync(canonical, partition, FenceAsync, token).ConfigureAwait(false);
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                await RequirePairCurrentAsync(canonical, witness, origin, fields, witnessPin!, budget, RequireSourceAsync, token).ConfigureAwait(false);
                RequirePairPins(canonical, witness, fields, witnessPin!);
                if (!origin.Matches(fields))
                {
                    return;
                }

                ImmutablePayload ownedState = canonical;
                DaprLogicalResponseOwner ownedWitness = witness;
                DaprLogicalReplayAnchorOrigin ownedOrigin = origin;
                void RequireCapturedPins()
                {
                    RequireLocal();
                    RequirePairPins(ownedState, ownedWitness, fields, witnessPin!);
                    if (!ownedOrigin.Matches(fields))
                    {
                        throw new DaprLogicalAnchorRefusalException("SnapshotCandidateHold: private origin changed.");
                    }
                }

                async Task FenceOwnedAsync(CancellationToken boundaryToken)
                {
                    token.ThrowIfCancellationRequested();
                    if (boundaryToken != token)
                    {
                        throw new InvalidOperationException("SnapshotCapabilityHold: original token changed.");
                    }

                    await InOwnerFenceAsync(boundary => RequirePairCurrentAsync(ownedState, ownedWitness, ownedOrigin, fields, witnessPin!, budget, RequireSourceAsync, boundary), token).ConfigureAwait(false);
                    RequireCapturedPins();
                }

                captured = new DaprLogicalSnapshotCandidate(canonical, witness, origin, partition, decoding, fields.CoveredSequence, binding.TargetSequence, sourcePin, witnessPin!, token, FenceOwnedAsync, budget);
                requireCapturedPins = RequireCapturedPins;
                transferred = true;
                canonical = null;
                witness = null;
                origin = null;
                partition = null;
                decoding = null;
            }, token).ConfigureAwait(false);
            RequireLocal();
            if (captured is null)
            {
                return null;
            }

            requireCapturedPins!();
            successfulReturn = true;
            return captured;
        }
        catch (DaprLogicalAnchorRefusalException)
        {
            return null;
        }
        catch (LogicalReplayCanonicalMismatchException)
        {
            return null;
        }
        finally
        {
            if (!transferred)
            {
                CryptographicOperations.ZeroMemory(sourcePin);
                if (witnessPin is not null)
                {
                    CryptographicOperations.ZeroMemory(witnessPin);
                }
            }

            stored?.Dispose();
            canonical?.Dispose();
            witness?.Dispose();
            origin?.Dispose();
            decoding?.Dispose();
            partition?.Dispose();
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

    private async Task RequirePairCurrentAsync(ImmutablePayload canonical, DaprLogicalResponseOwner witness, DaprLogicalReplayAnchorOrigin origin, DaprLogicalSnapshotWitness fields, byte[] witnessPin, EventBufferBudget budget, Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        RequirePairPins(canonical, witness, fields, witnessPin!);
        await sourceFence(token).ConfigureAwait(false);
        await origin.RequireCurrentAsync(token).ConfigureAwait(false);
        (DaprLogicalResponseOwner? currentState, DaprLogicalResponseOwner? currentWitness) = await ReadPairAsync(budget, token).ConfigureAwait(false);
        using (currentState)
        using (currentWitness)
        {
            if (currentState is null || currentWitness is null || !canonical.ComputeSha256().AsSpan().SequenceEqual(SHA256.HashData(currentState.Bytes.Span)) || !witness.Bytes.Span.SequenceEqual(currentWitness.Bytes.Span))
            {
                throw new DaprLogicalAnchorRefusalException("SnapshotCandidateHold: actual paired bytes changed.");
            }
        }

        await origin.RequireCurrentAsync(token).ConfigureAwait(false);
        await sourceFence(token).ConfigureAwait(false);
        RequirePairPins(canonical, witness, fields, witnessPin!);
    }

    private static void RequirePairPins(ImmutablePayload canonical, DaprLogicalResponseOwner witness, DaprLogicalSnapshotWitness fields, byte[] witnessPin)
    {
        if (!canonical.ComputeSha256().AsSpan().SequenceEqual(fields.FoldedHash.Span) || !SHA256.HashData(witness.Bytes.Span).AsSpan().SequenceEqual(witnessPin))
        {
            throw new DaprLogicalAnchorRefusalException("SnapshotCandidateHold: private pair changed.");
        }
    }

    private async Task<(DaprLogicalResponseOwner? State, DaprLogicalResponseOwner? Witness)> ReadPairAsync(EventBufferBudget budget, CancellationToken token)
    {
        using EventBufferReservation readWorkspace = budget.Reserve(checked(2 * _maximumStateBytes + 4 * DaprLogicalSnapshotCodec.MaximumBytes + 4096));
        DaprLogicalResponseOwner? state = null;
        DaprLogicalResponseOwner? witness = null;
        (DaprLogicalResponseOwner? State, DaprLogicalResponseOwner? Witness) capturedPair = (null, null);
        try
        {
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            ConditionalValue<byte[]> value = await _stateManager.TryGetStateAsync<byte[]>(StorageKey, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!value.HasValue || value.Value is null || value.Value.Length is < 1 || value.Value.Length > _maximumStateBytes)
            {
                return (null, null);
            }

            state = DaprLogicalResponseOwner.Capture(value.Value, budget);
            value = default;
            ConditionalValue<byte[]> pair = await _stateManager.TryGetStateAsync<byte[]>(WitnessKey, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!pair.HasValue || pair.Value is null || pair.Value.Length is < 1 || pair.Value.Length > DaprLogicalSnapshotCodec.MaximumBytes)
            {
                return (null, null);
            }

            witness = DaprLogicalResponseOwner.Capture(pair.Value, budget);
            pair = default;
            await _stateManager.ClearCacheAsync(token).ConfigureAwait(false);
            capturedPair = (state, witness);
            state = null;
            witness = null;
            return capturedPair;
        }
        finally
        {
            state?.Dispose();
            witness?.Dispose();
            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                capturedPair.State?.Dispose();
                capturedPair.Witness?.Dispose();
                throw;
            }
        }
    }

    private async Task InOwnerFenceAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        int calls = 0;
        Task? running = null;
        Task DecisionAsync(CancellationToken boundary)
        {
            token.ThrowIfCancellationRequested();
            if (boundary != token || Interlocked.Increment(ref calls) != 1)
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: serialized decision must run once with the original token.");
            }

            running = decision(boundary);
            return running;
        }

        try
        {
            await _ownerFence(DecisionAsync, token).ConfigureAwait(false);
            if (calls != 1 || running is null || !running.IsCompleted)
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: serialized owner did not complete its decision.");
            }

            await running.ConfigureAwait(false);
        }
        finally
        {
            if (running is not null && !running.IsCompleted)
            {
                try
                {
                    await running.ConfigureAwait(false);
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }
            }

            token.ThrowIfCancellationRequested();
        }
    }
}
