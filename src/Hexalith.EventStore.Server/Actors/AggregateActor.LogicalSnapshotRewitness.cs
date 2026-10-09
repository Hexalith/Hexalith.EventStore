using System.Security.Cryptography;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>Supplies dormant fresh current-prefix re-witnessing without historical-generation authority.</summary>
public partial class AggregateActor
{
    /// <summary>Re-witnesses an exact observed earlier-head state only from a fresh complete current-prefix origin.</summary>
    /// <remarks>The host must qualify one common serialized pair/source/origin owner; this entry is not registered for serving.</remarks>
    internal async Task<DaprReplayCommitOutcome> RewitnessLogicalSnapshotAsync(string model, DaprLogicalSourceBinding priorHint, DaprLogicalSourceBinding current, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, int maximumStateBytes, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireCurrentOrigin, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(acquireCurrentOrigin);
        ArgumentNullException.ThrowIfNull(ownerFence);
        ArgumentNullException.ThrowIfNull(budget);
        DaprLogicalSnapshotRewitnessPolicy.RequireBindings(model, priorHint, current);
        if (current.Identity.ActorId != Host.Id.GetId() || !source.OwnsStateManager(StateManager))
        {
            throw new InvalidOperationException("SnapshotRewitnessHold: exact actual aggregate/source owner is required.");
        }

        using EventBufferReservation pins = budget.Reserve(512);
        byte[] currentPin = DaprLogicalClaimCodec.ComputeSourceBindingHash(current, budget);
        byte[]? priorPin = null;
        bool open = true;
        int calls = 0;
        Task? running = null;
        DaprReplayCommitOutcome outcome = DaprReplayCommitOutcome.Indeterminate;
        void Local()
        {
            token.ThrowIfCancellationRequested();
            if (!open || !DaprLogicalClaimCodec.ComputeSourceBindingHash(current, budget).AsSpan().SequenceEqual(currentPin) || !DaprLogicalClaimCodec.ComputeSourceBindingHash(priorHint, budget).AsSpan().SequenceEqual(priorPin))
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: private binding or owner lifetime changed.");
            }

            DaprLogicalSnapshotRewitnessPolicy.RequireBindings(model, priorHint, current);
            reconstruction.RequireSource(current, token);
            trust.RequireCurrent(token);
            _logicalSnapshotPending?.RequireDesiredPins();
        }

        async Task FenceAsync(DaprLogicalReplayAnchorOrigin origin, CancellationToken boundary)
        {
            token.ThrowIfCancellationRequested();
            if (boundary != token)
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: originating token changed before addressed work.");
            }

            Local();
            await RequireLogicalSnapshotSourceAsync(current, source, trust, reconstruction, token).ConfigureAwait(false);
            Local();
            await origin.RequireCurrentAsync(token).ConfigureAwait(false);
            Local();
        }

        void RequireOrigin(DaprLogicalReplayAnchorOrigin origin, DaprLogicalSnapshotWrite write)
        {
            Local();
            if (!ReferenceEquals(origin.Budget, budget) || !write.MatchesOrigin(origin))
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: exact current completed origin is required.");
            }
        }

        Task Decision(CancellationToken decisionToken)
        {
            token.ThrowIfCancellationRequested();
            if (!open || Interlocked.Increment(ref calls) != 1 || decisionToken != token)
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: one complete originating-token owner decision is required.");
            }

            running = DecideAsync();
            return running;
        }

        async Task DecideAsync()
        {
            Local();
            if (_logicalSnapshotPending is not null)
            {
                DaprLogicalSnapshotWrite pending = _logicalSnapshotPending;
                if (pending.PolicyId != model)
                {
                    throw new InvalidOperationException("SnapshotRewitnessHold: pending write belongs to another policy.");
                }

                if (!ReferenceEquals(pending.Budget, budget))
                {
                    throw new InvalidOperationException("SnapshotRewitnessHold: pending ownership requires its exact parent.");
                }

                outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);
                try
                {
                    Local();
                    if (outcome == DaprReplayCommitOutcome.Proven)
                    {
                        using DaprLogicalReplayAnchorOrigin origin = await acquireCurrentOrigin(current.TargetSequence, budget, token).ConfigureAwait(false) ?? throw new InvalidOperationException("SnapshotRewitnessHold: current completed origin is missing.");
                        Local();
                        RequireOrigin(origin, pending);
                        Task CurrentFenceAsync(CancellationToken boundary) => FenceAsync(origin, boundary);
                        await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, pending.WitnessArray.Length, reconstruction, budget, CurrentFenceAsync, token).ConfigureAwait(false);
                        RequireOrigin(origin, pending);
                        outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);
                        Local();
                        if (outcome == DaprReplayCommitOutcome.Proven)
                        {
                            await FenceAsync(origin, token).ConfigureAwait(false);
                            RequireOrigin(origin, pending);
                        }
                    }
                }
                finally
                {
                    if (outcome != DaprReplayCommitOutcome.Indeterminate)
                    {
                        pending.Dispose();
                        _logicalSnapshotPending = null;
                    }

                    token.ThrowIfCancellationRequested();
                }

                return;
            }

            await RequireLogicalSnapshotSourceAsync(current, source, trust, reconstruction, token).ConfigureAwait(false);
            Local();
            var write = new DaprLogicalSnapshotWrite(budget, maximumStateBytes, current.Identity.SnapshotKey + ":logical-v1", current.Identity.SnapshotKey + ":logical-v1:evolution-witness", model);
            _logicalSnapshotPending = write;
            bool saveEntered = false;
            try
            {
                using DaprLogicalReplayAnchorOrigin origin = await acquireCurrentOrigin(current.TargetSequence, budget, token).ConfigureAwait(false) ?? throw new InvalidOperationException("SnapshotRewitnessHold: current completed origin is missing.");
                Local();
                await FenceAsync(origin, token).ConfigureAwait(false);
                DaprLogicalSnapshotWitness fields = origin.CreateWitness(write.StorageKey, write.WitnessKey);
                if (!ReferenceEquals(origin.Budget, budget) || fields.CoveredSequence != current.TargetSequence || fields.TenantId != current.Identity.TenantId || fields.Domain != current.Identity.Domain || fields.AggregateId != current.Identity.AggregateId || fields.AggregateType != current.AggregateType || fields.SerializerId != reconstruction.SerializerId || !fields.SourceBindingHash.Span.SequenceEqual(currentPin) || !fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) || !fields.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span))
                {
                    throw new InvalidOperationException("SnapshotRewitnessHold: fresh origin differs from current fixed source/codec.");
                }

                byte[] witnessImage = DaprLogicalSnapshotCodec.EncodeSnapshot(fields);
                try
                {
                    write.SetDesired(origin.State.Span, witnessImage);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(witnessImage);
                }

                (DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? witness) = await ReadLogicalSnapshotPairAsync(write, token, Local).ConfigureAwait(false);
                write.SetPrior(state, witness);
                Local();
                bool exactDesired = write.Classify(state, witness) == DaprReplayCommitOutcome.Proven;
                if (!exactDesired)
                {
                    if (state is null || witness is null)
                    {
                        throw new InvalidOperationException("SnapshotRewitnessHold: a complete observed prior pair is required.");
                    }

                    DaprLogicalSnapshotRewitnessPolicy.RequirePrior(write, priorHint, trust, reconstruction);
                }

                Task CurrentFenceAsync(CancellationToken boundary) => FenceAsync(origin, boundary);
                await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, write.WitnessArray.Length, reconstruction, budget, CurrentFenceAsync, token).ConfigureAwait(false);
                RequireOrigin(origin, write);
                if (!exactDesired && !write.PriorState.Bytes.Span.SequenceEqual(origin.State.Span))
                {
                    throw new InvalidOperationException("SnapshotRewitnessHold: complete current prefix differs from the observed prior state.");
                }

                (DaprLogicalResponseOwner? freshState, DaprLogicalResponseOwner? freshWitness) = await ReadLogicalSnapshotPairAsync(write, token, Local).ConfigureAwait(false);
                if (!write.MatchesPrior(freshState, freshWitness) || exactDesired && write.Classify(freshState, freshWitness) != DaprReplayCommitOutcome.Proven)
                {
                    throw new InvalidOperationException("SnapshotRewitnessHold: actual prior pair changed before staging.");
                }

                write.ReleaseReads();
                await FenceAsync(origin, token).ConfigureAwait(false);
                RequireOrigin(origin, write);
                if (exactDesired)
                {
                    outcome = DaprReplayCommitOutcome.Proven;
                    return;
                }

                await StateManager.SetStateAsync(write.StorageKey, write.StateArray, token).ConfigureAwait(false);
                await FenceAsync(origin, token).ConfigureAwait(false);
                RequireOrigin(origin, write);
                await StateManager.SetStateAsync(write.WitnessKey, write.WitnessArray, token).ConfigureAwait(false);
                await FenceAsync(origin, token).ConfigureAwait(false);
                RequireOrigin(origin, write);
                saveEntered = true;
                try
                {
                    await StateManager.SaveStateAsync(token).ConfigureAwait(false);
                }
                catch
                {
                // Independent logical readback classifies truth; acknowledgement is not evidence.
                }

                outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);
                Local();
                if (outcome == DaprReplayCommitOutcome.Proven)
                {
                    RequireOrigin(origin, write);
                    await FenceAsync(origin, token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!saveEntered || outcome != DaprReplayCommitOutcome.Indeterminate)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    try
                    {
                        await StateManager.ClearCacheAsync(cleanup.Token).ConfigureAwait(false);
                        write.Dispose();
                        _logicalSnapshotPending = null;
                    }
                    catch
                    {
                        _stateCacheUnsafe = true;
                        outcome = DaprReplayCommitOutcome.Indeterminate;
                    }
                }

                token.ThrowIfCancellationRequested();
            }
        }

        try
        {
            priorPin = DaprLogicalClaimCodec.ComputeSourceBindingHash(priorHint, budget);
            Local();
            await ownerFence(Decision, token).ConfigureAwait(false);
            open = false;
            token.ThrowIfCancellationRequested();
            if (calls != 1 || running is null || !running.IsCompleted)
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: serialized decision was skipped or returned early.");
            }

            await running.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            reconstruction.RequireSource(current, token);
            trust.RequireCurrent(token);
            if (!DaprLogicalClaimCodec.ComputeSourceBindingHash(current, budget).AsSpan().SequenceEqual(currentPin) || !DaprLogicalClaimCodec.ComputeSourceBindingHash(priorHint, budget).AsSpan().SequenceEqual(priorPin))
            {
                throw new InvalidOperationException("SnapshotRewitnessHold: fixed binding changed at owner return.");
            }

            return outcome;
        }
        finally
        {
            open = false;
            CryptographicOperations.ZeroMemory(currentPin);
            if (priorPin is not null)
            {
                CryptographicOperations.ZeroMemory(priorPin);
            }

            token.ThrowIfCancellationRequested();
        }
    }
}
