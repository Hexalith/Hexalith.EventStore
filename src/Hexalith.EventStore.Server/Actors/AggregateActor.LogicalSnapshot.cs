using System.Security.Cryptography;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>Supplies the dormant actual-actor save boundary for independently qualified logical snapshot pairs.</summary>
public partial class AggregateActor
{
    private DaprLogicalSnapshotWrite? _logicalSnapshotPending;
    /// <summary>Stages both logical images under one actual actor save and classifies exact fresh readback.</summary>
    /// <remarks>The host must qualify a common serialized fence across this actor and the completed-origin owner; this is not registered for serving.</remarks>
    internal Task<DaprReplayCommitOutcome> IssueLogicalSnapshotAsync(string model, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, int maximumStateBytes, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireOrigin, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, EventBufferBudget budget, CancellationToken token)
        => WriteLogicalSnapshotAsync(model, false, binding, source, trust, reconstruction, maximumStateBytes, acquireOrigin, ownerFence, budget, token);

    /// <summary>Replaces an exact strictly older qualified pair under one existing serialized actor decision.</summary>
    /// <remarks>Explicit dormant policy; the common actual pair/source/origin serialization prerequisite must be qualified by the host.</remarks>
    internal Task<DaprReplayCommitOutcome> ReplaceLogicalSnapshotAsync(string model, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, int maximumStateBytes, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireOrigin, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, EventBufferBudget budget, CancellationToken token)
        => WriteLogicalSnapshotAsync(model, true, binding, source, trust, reconstruction, maximumStateBytes, acquireOrigin, ownerFence, budget, token);

    /// <summary>Composes initial issuance or explicit prior admission with the same paired actual-save and recovery boundary.</summary>
    private async Task<DaprReplayCommitOutcome> WriteLogicalSnapshotAsync(string model, bool replacement, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, int maximumStateBytes, Func<long, EventBufferBudget, CancellationToken, Task<DaprLogicalReplayAnchorOrigin>> acquireOrigin, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(acquireOrigin);
        ArgumentNullException.ThrowIfNull(ownerFence);
        ArgumentNullException.ThrowIfNull(budget);
        if (model != (replacement ? DaprLogicalSnapshotPrior.ModelId : DaprLogicalSnapshotCodec.SnapshotModel) || binding.TargetSequence < 1 || binding.Identity.ActorId != Host.Id.GetId() || !source.OwnsStateManager(StateManager))
        {
            throw new InvalidOperationException("SnapshotCapabilityHold: explicit model and exact actual actor/source are required.");
        }

        using EventBufferReservation pins = budget.Reserve(512);
        byte[] sourcePin = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
        int calls = 0;
        Task? running = null;
        bool fenceOpen = true;
        DaprReplayCommitOutcome outcome = DaprReplayCommitOutcome.Indeterminate;
        void RequireDecisionPins()
        {
            token.ThrowIfCancellationRequested();
            if (!fenceOpen || !DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget).AsSpan().SequenceEqual(sourcePin))
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: serialized lifetime or private fixed source changed.");
            }

            reconstruction.RequireCurrent(token);
            trust.RequireCurrent(token);
            _logicalSnapshotPending?.RequireDesiredPins();
        }

        void RequireOriginScope(DaprLogicalReplayAnchorOrigin origin)
        {
            RequireDecisionPins();
            DaprLogicalSnapshotWitness fields = origin.CreateWitness(binding.Identity.SnapshotKey + ":logical-v1", binding.Identity.SnapshotKey + ":logical-v1:evolution-witness");
            if (fields.CoveredSequence != binding.TargetSequence || fields.TenantId != binding.Identity.TenantId || fields.Domain != binding.Identity.Domain || fields.AggregateId != binding.Identity.AggregateId || fields.AggregateType != binding.AggregateType || fields.SerializerId != reconstruction.SerializerId || !fields.SourceBindingHash.Span.SequenceEqual(sourcePin) || !fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) || !fields.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span))
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: actual completed origin differs from the exact issuer scope.");
            }
        }

        async Task RequireFinalServingAsync(DaprLogicalReplayAnchorOrigin current, DaprLogicalSnapshotPrior? currentPrior)
        {
            RequireDecisionPins();
            await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
            RequireDecisionPins();
            await current.RequireCurrentAsync(token).ConfigureAwait(false);
            RequireDecisionPins();
            if (currentPrior is not null)
            {
                await currentPrior.RequireCurrentAsync(token).ConfigureAwait(false);
                RequireDecisionPins();
            }
        }

        Task Decision(CancellationToken decisionToken)
        {
            token.ThrowIfCancellationRequested();
            if (!fenceOpen || Interlocked.Increment(ref calls) != 1 || decisionToken != token)
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: serialized owner must run exactly one original-token decision.");
            }

            running = DecideAsync();
            return running;
        }

        async Task DecideAsync()
        {
            token.ThrowIfCancellationRequested();
            if (_logicalSnapshotPending is not null)
            {
                DaprLogicalSnapshotWrite pending = _logicalSnapshotPending;
                if (pending.PolicyId != model)
                {
                    throw new InvalidOperationException("SnapshotRecoveryHold: pending write belongs to another policy.");
                }

                if (!ReferenceEquals(pending.Budget, budget))
                {
                    throw new InvalidOperationException("SnapshotRecoveryHold: pending ownership requires its exact retained parent.");
                }

                // Durable classification and cache release remain independent of current trust/callback authority.
                outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);
                try
                {
                    RequireDecisionPins();
                    if (outcome == DaprReplayCommitOutcome.Proven)
                    {
                        await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                        RequireDecisionPins();
                        using DaprLogicalReplayAnchorOrigin current = await acquireOrigin(binding.TargetSequence, budget, token).ConfigureAwait(false) ?? throw new InvalidOperationException("SnapshotCapabilityHold: actual completed origin is missing.");
                        RequireDecisionPins();
                        await current.RequireCurrentAsync(token).ConfigureAwait(false);
                        RequireDecisionPins();
                        RequireOriginScope(current);
                        if (!ReferenceEquals(current.Budget, budget) || !pending.MatchesOrigin(current))
                        {
                            throw new InvalidOperationException("SnapshotCapabilityHold: pending pair differs from the fresh actual origin.");
                        }

                        async Task PendingSourceFenceAsync(CancellationToken boundary)
                        {
                            RequireDecisionPins();
                            await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, boundary).ConfigureAwait(false);
                            RequireDecisionPins();
                            await current.RequireCurrentAsync(boundary).ConfigureAwait(false);
                            RequireDecisionPins();
                        }

                        using DaprLogicalSnapshotPrior? currentPrior = pending.ReplacementAdmitted
                            ? await DaprLogicalSnapshotPrior.AcquireAsync(pending.PriorState, pending.PriorWitness, pending, binding, trust, reconstruction, acquireOrigin, PendingSourceFenceAsync, RequireDecisionPins, token).ConfigureAwait(false)
                            : null;
                        RequireDecisionPins();

                        outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);
                        RequireDecisionPins();
                        if (currentPrior is not null)
                        {
                            await currentPrior.RequireSourceAndOriginAsync(PendingSourceFenceAsync, token).ConfigureAwait(false);
                            RequireDecisionPins();
                            outcome = await InspectLogicalSnapshotAsync(pending).ConfigureAwait(false);
                            RequireDecisionPins();
                        }

                        if (outcome == DaprReplayCommitOutcome.Proven)
                        {
                            await RequireFinalServingAsync(current, currentPrior).ConfigureAwait(false);
                            RequireDecisionPins();
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

            RequireDecisionPins();
            var write = new DaprLogicalSnapshotWrite(budget, maximumStateBytes, binding.Identity.SnapshotKey + ":logical-v1", binding.Identity.SnapshotKey + ":logical-v1:evolution-witness", model);
            _logicalSnapshotPending = write;
            bool saveEntered = false;
            DaprLogicalSnapshotPrior? prior = null;
            try
            {
                await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                RequireDecisionPins();
                using DaprLogicalReplayAnchorOrigin origin = await acquireOrigin(binding.TargetSequence, budget, token).ConfigureAwait(false) ?? throw new InvalidOperationException("SnapshotCapabilityHold: actual completed origin is missing.");
                RequireDecisionPins();
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(origin.Budget, budget))
                {
                    throw new InvalidOperationException("SnapshotCapabilityHold: completed origin uses a different parent.");
                }

                await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                RequireDecisionPins();
                RequireOriginScope(origin);
                byte[] witness = DaprLogicalSnapshotCodec.EncodeSnapshot(origin.CreateWitness(write.StorageKey, write.WitnessKey));
                try
                {
                    write.SetDesired(origin.State.Span, witness);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(witness);
                    token.ThrowIfCancellationRequested();
                }

                (DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? priorWitness) = await ReadLogicalSnapshotPairAsync(write, token, RequireDecisionPins).ConfigureAwait(false);
                RequireDecisionPins();
                write.SetPrior(state, priorWitness);
                await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                RequireDecisionPins();
                await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                RequireDecisionPins();
                write.RequireDesiredPins();
                if (!write.MatchesOrigin(origin))
                {
                    throw new InvalidOperationException("SnapshotCapabilityHold: desired pair differs from the actual origin.");
                }

                if (write.Classify(state, priorWitness) == DaprReplayCommitOutcome.Proven)
                {
                    async Task DesiredFenceAsync(CancellationToken boundary)
                    {
                        token.ThrowIfCancellationRequested();
                        if (boundary != token)
                        {
                            throw new InvalidOperationException("SnapshotCapabilityHold: independent desired admission requires the original token.");
                        }

                        RequireDecisionPins();
                        await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, boundary).ConfigureAwait(false);
                        RequireDecisionPins();
                        await origin.RequireCurrentAsync(boundary).ConfigureAwait(false);
                        RequireDecisionPins();
                    }

                    await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, write.WitnessArray.Length, reconstruction, budget, DesiredFenceAsync, token).ConfigureAwait(false);
                    RequireDecisionPins();
                    outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);
                    RequireDecisionPins();
                    if (outcome == DaprReplayCommitOutcome.Proven)
                    {
                        await RequireFinalServingAsync(origin, null).ConfigureAwait(false);
                        RequireDecisionPins();
                    }

                    return;
                }

                if (state is not null || priorWitness is not null)
                {
                    if (!replacement || state is null || priorWitness is null)
                    {
                        throw new InvalidOperationException("SnapshotPriorHold: existing nonidentical or torn pair cannot be replaced by this initial issuer.");
                    }

                    async Task SourceFenceAsync(CancellationToken boundary)
                    {
                        RequireDecisionPins();
                        await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, boundary).ConfigureAwait(false);
                        RequireDecisionPins();
                        await origin.RequireCurrentAsync(boundary).ConfigureAwait(false);
                        RequireDecisionPins();
                    }

                    prior = await DaprLogicalSnapshotPrior.AcquireAsync(state, priorWitness, write, binding, trust, reconstruction, acquireOrigin, SourceFenceAsync, RequireDecisionPins, token).ConfigureAwait(false);
                    await prior.RequireCurrentAsync(token).ConfigureAwait(false);
                    await SourceFenceAsync(token).ConfigureAwait(false);
                    (DaprLogicalResponseOwner? freshState, DaprLogicalResponseOwner? freshWitness) = await ReadLogicalSnapshotPairAsync(write, token, RequireDecisionPins).ConfigureAwait(false);
                    RequireDecisionPins();
                    if (!write.MatchesPrior(freshState, freshWitness))
                    {
                        throw new InvalidOperationException("SnapshotPriorHold: actual predecessor pair changed before staging.");
                    }

                    write.ReleaseReads();
                    await prior.RequireCurrentAsync(token).ConfigureAwait(false);
                    RequireDecisionPins();
                    (freshState, freshWitness) = await ReadLogicalSnapshotPairAsync(write, token, RequireDecisionPins).ConfigureAwait(false);
                    RequireDecisionPins();
                    if (!write.MatchesPrior(freshState, freshWitness))
                    {
                        throw new InvalidOperationException("SnapshotPriorHold: actual predecessor pair changed at the final origin fence.");
                    }

                    write.ReleaseReads();
                    write.MarkReplacementAdmitted();
                }
                else if (replacement)
                {
                    throw new InvalidOperationException("SnapshotPriorHold: replacement requires a complete older pair.");
                }

                try
                {
                    await StateManager.SetStateAsync(write.StorageKey, write.StateArray, token).ConfigureAwait(false);
                    RequireDecisionPins();
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                RequireDecisionPins();
                await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                RequireDecisionPins();
                if (prior is not null)
                {
                    await prior.RequireCurrentAsync(token).ConfigureAwait(false);
                    RequireDecisionPins();
                }

                try
                {
                    await StateManager.SetStateAsync(write.WitnessKey, write.WitnessArray, token).ConfigureAwait(false);
                    RequireDecisionPins();
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                RequireDecisionPins();
                await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                RequireDecisionPins();
                if (prior is not null)
                {
                    await prior.RequireCurrentAsync(token).ConfigureAwait(false);
                    RequireDecisionPins();
                }

                write.RequireDesiredPins();
                saveEntered = true;
                try
                {
                    await StateManager.SaveStateAsync(token).ConfigureAwait(false);
                }
                catch
                {
                // Acknowledgement cannot classify durable truth; use an independent bounded readback.
                }

                outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                RequireDecisionPins();
                if (outcome == DaprReplayCommitOutcome.Proven)
                {
                    await RequireLogicalSnapshotSourceAsync(binding, source, trust, reconstruction, token).ConfigureAwait(false);
                    RequireDecisionPins();
                    await origin.RequireCurrentAsync(token).ConfigureAwait(false);
                    RequireDecisionPins();
                    if (prior is not null)
                    {
                        await prior.RequireCurrentAsync(token).ConfigureAwait(false);
                        RequireDecisionPins();
                    }

                    if (!write.MatchesOrigin(origin))
                    {
                        throw new InvalidOperationException("SnapshotCapabilityHold: saved pair differs from the current actual origin.");
                    }

                    outcome = await InspectLogicalSnapshotAsync(write).ConfigureAwait(false);
                    RequireDecisionPins();
                    if (outcome == DaprReplayCommitOutcome.Proven)
                    {
                        await RequireFinalServingAsync(origin, prior).ConfigureAwait(false);
                        RequireDecisionPins();
                    }

                    token.ThrowIfCancellationRequested();
                    reconstruction.RequireCurrent(token);
                    trust.RequireCurrent(token);
                    write.RequireDesiredPins();
                }
            }
            finally
            {
                prior?.Dispose();
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
                        // Retain charged staging/cache aliases until a later independent reconciliation succeeds.
                        _stateCacheUnsafe = true;
                        outcome = DaprReplayCommitOutcome.Indeterminate;
                    }
                }

                token.ThrowIfCancellationRequested();
            }
        }

        try
        {
            await ownerFence(Decision, token).ConfigureAwait(false);
            fenceOpen = false;
            token.ThrowIfCancellationRequested();
            if (calls != 1 || running is null || !running.IsCompleted)
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: serialized decision was skipped or returned early.");
            }

            await running.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            reconstruction.RequireCurrent(token);
            trust.RequireCurrent(token);
            if (!DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget).AsSpan().SequenceEqual(sourcePin))
            {
                throw new InvalidOperationException("SnapshotCapabilityHold: fixed source changed at owner return.");
            }

            return outcome;
        }
        finally
        {
            fenceOpen = false;
            CryptographicOperations.ZeroMemory(sourcePin);
            token.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Checks local registry/reconstruction pins on both sides of actual addressed metadata readback.</summary>
    private static async Task RequireLogicalSnapshotSourceAsync(DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        reconstruction.RequireSource(binding, token);
        trust.RequireCurrent(token);
        try
        {
            await source.RequireCurrentAsync(binding, trust, token).ConfigureAwait(false);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        reconstruction.RequireSource(binding, token);
        trust.RequireCurrent(token);
    }

    /// <summary>Captures detached bounded pair images before confirming release of actor-cache aliases.</summary>
    private async Task<(DaprLogicalResponseOwner? State, DaprLogicalResponseOwner? Witness)> ReadLogicalSnapshotPairAsync(DaprLogicalSnapshotWrite write, CancellationToken token, Action? localFence = null)
    {
        await StateManager.ClearCacheAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        ConditionalValue<byte[]> state = await StateManager.TryGetStateAsync<byte[]>(write.StorageKey, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        DaprLogicalResponseOwner? privateState = state.HasValue ? write.CaptureRead(state.Value ?? throw new InvalidOperationException("SnapshotReadbackHold: present state has no typed value."), false) : null;
        ConditionalValue<byte[]> witness = await StateManager.TryGetStateAsync<byte[]>(write.WitnessKey, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        DaprLogicalResponseOwner? privateWitness = witness.HasValue ? write.CaptureRead(witness.Value ?? throw new InvalidOperationException("SnapshotReadbackHold: present witness has no typed value."), true) : null;
        await StateManager.ClearCacheAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        return (privateState, privateWitness);
    }

    /// <summary>Classifies exact actual bytes under the existing independent bounded durable-boundary policy.</summary>
    private async Task<DaprReplayCommitOutcome> InspectLogicalSnapshotAsync(DaprLogicalSnapshotWrite write)
    {
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            (DaprLogicalResponseOwner? state, DaprLogicalResponseOwner? witness) = await ReadLogicalSnapshotPairAsync(write, recovery.Token).ConfigureAwait(false);
            DaprReplayCommitOutcome result = write.Classify(state, witness);
            write.ReleaseReads();
            return result;
        }
        catch
        {
            _stateCacheUnsafe = true;
            return DaprReplayCommitOutcome.Indeterminate;
        }
    }
}
