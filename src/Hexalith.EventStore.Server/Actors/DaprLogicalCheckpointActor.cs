using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>Owns dormant single canonical projection checkpoint rows with no other mutation or serving routes.</summary>
/// <param name = "host">The dedicated unregistered actor host.</param>
internal sealed partial class DaprLogicalCheckpointActor(ActorHost host) : Actor(host), IDaprLogicalCheckpointActor
{
    private DaprLogicalCheckpointWrite? _logicalCheckpointPending;
    /// <summary>Issues three immutable/current checkpoint participants through the dedicated checkpoint actor save boundary.</summary>
    /// <remarks>All managers must be covered by one qualified common serialized decision; no production registration is supplied.</remarks>
    internal Task<DaprReplayCommitOutcome> IssueLogicalCheckpointAsync(string model, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, DaprReplayOperationOwner operation, int maximumStateBytes, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, CancellationToken token) => LogicalCheckpointAsync(model, false, fold, binding, source, trust, operation, maximumStateBytes, ownerFence, token);
    /// <summary>Checks exact actual checkpoint participants against a fresh completed pure fold without staging or saving.</summary>
    internal Task<DaprReplayCommitOutcome> ReadLogicalCheckpointAsync(string model, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, DaprReplayOperationOwner operation, int maximumStateBytes, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, CancellationToken token) => LogicalCheckpointAsync(model, true, fold, binding, source, trust, operation, maximumStateBytes, ownerFence, token);
    private async Task<DaprReplayCommitOutcome> LogicalCheckpointAsync(string model, bool readOnly, DaprLogicalProjectionFold fold, DaprLogicalSourceBinding binding, DaprLogicalReplaySource source, DaprLogicalClaimTrust trust, DaprReplayOperationOwner operation, int maximumStateBytes, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, CancellationToken token, Action<DaprLogicalCheckpointWrite>? capture = null)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(fold);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(ownerFence);
        if (model != DaprLogicalCheckpointCodec.ModelId || fold.ActorId != Id.GetId() || binding.TargetSequence < 1 || binding.RetainedFloor != 1 || source.OwnsStateManager(StateManager))
        {
            throw new InvalidOperationException("CheckpointHold: explicit single complete-prefix model and separate projection owner required.");
        }

        operation.RequireCheckpointOwner(StateManager, fold.Reconstruction);
        fold.RequireCurrent(binding, token);
        trust.RequireCurrent(token);
        using EventBufferReservation pins = fold.Budget.Reserve(16384);
        byte[] sourcePin = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, fold.Budget);
        int calls = 0;
        Task? running = null;
        bool open = true;
        DaprReplayCommitOutcome outcome = DaprReplayCommitOutcome.Indeterminate;
        void RequirePins()
        {
            token.ThrowIfCancellationRequested();
            if (!open)
            {
                throw new InvalidOperationException("CheckpointHold: serialized owner decision expired.");
            }

            fold.RequireCurrent(binding, token);
            trust.RequireCurrent(token);
            operation.RequireCheckpointOwner(StateManager, fold.Reconstruction);
            byte[] current = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, fold.Budget);
            try
            {
                if (!current.AsSpan().SequenceEqual(sourcePin))
                {
                    throw new InvalidOperationException("CheckpointHold: fixed source changed.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(current);
            }

            _logicalCheckpointPending?.RequireAdmission(fold, sourcePin, maximumStateBytes);
        }

        async Task SourceFenceAsync(CancellationToken boundary)
        {
            RequirePins();
            if (boundary != token)
            {
                throw new InvalidOperationException("CheckpointHold: originating token changed before source callbacks.");
            }

            try
            {
                await source.RequireCurrentAsync(binding, trust, boundary).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            RequirePins();
        }

        async Task FinalFenceAsync(DaprLogicalReplayAnchorOrigin origin, CancellationToken boundary)
        {
            await SourceFenceAsync(boundary).ConfigureAwait(false);
            try
            {
                await origin.RequireCurrentAsync(boundary).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            RequirePins();
        }

        void PrepareDesired(DaprLogicalCheckpointWrite write, DaprLogicalReplayAnchorOrigin origin)
        {
            RequirePins();
            if (!ReferenceEquals(origin.Budget, fold.Budget))
            {
                throw new InvalidOperationException("CheckpointHold: actual origin uses another capacity scope.");
            }

            DaprLogicalSnapshotWitness fields = origin.CreateWitness("pending", "pending");
            byte[]? version = null;
            byte[]? scope = null;
            byte[]? originImage = null;
            byte[]? root = null;
            byte[]? rootHash = null;
            byte[]? witness = null;
            try
            {
                if (!origin.Matches(fields) || fields.TenantId != binding.Identity.TenantId || fields.Domain != binding.Identity.Domain || fields.AggregateId != binding.Identity.AggregateId || fields.AggregateType != binding.AggregateType || fields.CoveredSequence != binding.TargetSequence || fields.SerializerId != fold.Reconstruction.SerializerId || !fields.SourceBindingHash.Span.SequenceEqual(sourcePin) || !fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) || !fields.ReconstructionBindingHash.Span.SequenceEqual(fold.Reconstruction.Fingerprint.Span))
                {
                    throw new InvalidOperationException("CheckpointHold: actual origin differs from the exact projection fold/source.");
                }

                version = DaprLogicalCheckpointCodec.Version(fields.OperationId, fields.Generation);
                string prefix = "logical-checkpoint-v1:" + Convert.ToHexStringLower(fold.NamespaceHash.Span);
                string stateKey = prefix + ":state:" + Convert.ToHexStringLower(version);
                string currentKey = prefix + ":current";
                string witnessKey = prefix + ":witness";
                fields = fields with
                {
                    StorageKey = stateKey,
                    WitnessKey = witnessKey
                };
                scope = DaprLogicalCheckpointCodec.Scope(fold.NamespaceHash.Span, sourcePin, fold.Fingerprint.Span);
                originImage = DaprLogicalSnapshotCodec.EncodeSnapshot(fields);
                root = DaprLogicalCheckpointCodec.Root(binding, fold.Route, fold.ActorId, fold.Store, fold.Backend.Span, scope, fold.Fingerprint.Span, stateKey, originImage);
                rootHash = SHA256.HashData(root);
                witness = DaprLogicalSnapshotCodec.EncodeCheckpoint(new DaprLogicalCheckpointWitness(fields.TenantId, fields.Domain, fold.Route, scope, fields.CoveredSequence, fields.Accumulator, fields.RegistryFingerprint, rootHash, DaprLogicalSnapshotCodec.CheckpointModel));
                if (write.HasDesired)
                {
                    if (!write.Matches(origin.State.Span, root, witness))
                    {
                        throw new InvalidOperationException("CheckpointRecoveryHold: retained checkpoint differs from the fresh actual completed operation.");
                    }
                }
                else
                {
                    write.SetDesired([stateKey, currentKey, witnessKey], origin.State.Span, root, witness);
                }
            }
            finally
            {
                foreach (byte[]? image in new[]
                {
                    version,
                    scope,
                    originImage,
                    root,
                    rootHash,
                    witness
                }

                )
                {
                    if (image is not null)
                    {
                        CryptographicOperations.ZeroMemory(image);
                    }
                }

                foreach (ReadOnlyMemory<byte> hash in new[]
                {
                    fields.SourceBindingHash,
                    fields.StorageHash,
                    fields.FoldedHash,
                    fields.RegistryFingerprint,
                    fields.ReconstructionBindingHash,
                    fields.Accumulator,
                    fields.TranscriptHash,
                    fields.EffectiveChainHash
                }

                )
                {
                    if (MemoryMarshal.TryGetArray(hash, out ArraySegment<byte> array))
                    {
                        CryptographicOperations.ZeroMemory(array.AsSpan());
                    }
                }

                token.ThrowIfCancellationRequested();
            }
        }

        Task Decision(CancellationToken boundary)
        {
            token.ThrowIfCancellationRequested();
            if (!open || Interlocked.Increment(ref calls) != 1 || boundary != token)
            {
                throw new InvalidOperationException("CheckpointHold: exactly one completed original-token decision required.");
            }

            running = DecideAsync();
            return running;
        }

        async Task DecideAsync()
        {
            RequirePins();
            bool pending = _logicalCheckpointPending is not null;
            DaprLogicalCheckpointWrite write = _logicalCheckpointPending ?? new DaprLogicalCheckpointWrite(fold, sourcePin, maximumStateBytes);
            _logicalCheckpointPending = write;
            bool saveEntered = pending;
            try
            {
                if (pending)
                {
                    outcome = await InspectLogicalCheckpointAsync(write).ConfigureAwait(false);
                    RequirePins();
                    if (outcome != DaprReplayCommitOutcome.Proven)
                    {
                        return;
                    }
                }

                await SourceFenceAsync(token).ConfigureAwait(false);
                using DaprLogicalReplayAnchorOrigin origin = await operation.CaptureCompletedAnchorOriginAsync(source, binding, trust, fold.Budget, token).ConfigureAwait(false);
                RequirePins();
                await FinalFenceAsync(origin, token).ConfigureAwait(false);
                PrepareDesired(write, origin);
                await DaprLogicalSnapshotCanonical.RequireAsync(origin.State, write.Array(1).Length + write.Array(2).Length, fold.Reconstruction, fold.Budget, boundary => FinalFenceAsync(origin, boundary), token).ConfigureAwait(false);
                RequirePins();
                if (!pending)
                {
                    DaprLogicalResponseOwner? [] prior = await ReadLogicalCheckpointRowsAsync(write, token, RequirePins).ConfigureAwait(false);
                    outcome = write.Classify(prior);
                    write.ReleaseReads();
                    RequirePins();
                    await FinalFenceAsync(origin, token).ConfigureAwait(false);
                    if (outcome == DaprReplayCommitOutcome.Indeterminate)
                    {
                        throw new InvalidOperationException("CheckpointPriorHold: existing nonidentical/torn rows cannot be overwritten.");
                    }

                    if (outcome == DaprReplayCommitOutcome.NoCommit && !readOnly)
                    {
                        // Recheck actual absence immediately before staging under the same common serialized decision.
                        prior = await ReadLogicalCheckpointRowsAsync(write, token, RequirePins).ConfigureAwait(false);
                        if (write.Classify(prior) != DaprReplayCommitOutcome.NoCommit)
                        {
                            throw new InvalidOperationException("CheckpointPriorHold: actual prior changed before staging.");
                        }

                        write.ReleaseReads();
                        await FinalFenceAsync(origin, token).ConfigureAwait(false);
                        for (int index = 0; index < 3; index++)
                        {
                            try
                            {
                                await StateManager.SetStateAsync(write.Key(index), write.Array(index), token).ConfigureAwait(false);
                            }
                            finally
                            {
                                token.ThrowIfCancellationRequested();
                            }

                            RequirePins();
                            await FinalFenceAsync(origin, token).ConfigureAwait(false);
                        }

                        saveEntered = true;
                        try
                        {
                            await StateManager.SaveStateAsync(token).ConfigureAwait(false);
                        }
                        catch
                        {
                        // Independent readback, not acknowledgement, classifies durable truth.
                        }
                    }
                }

                outcome = await InspectLogicalCheckpointAsync(write).ConfigureAwait(false);
                RequirePins();
                if (outcome == DaprReplayCommitOutcome.Proven)
                {
                    await FinalFenceAsync(origin, token).ConfigureAwait(false);
                    PrepareDesired(write, origin);
                    RequirePins();
                    capture?.Invoke(write);
                    RequirePins();
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
                        _logicalCheckpointPending = null;
                    }
                    catch
                    {
                        outcome = DaprReplayCommitOutcome.Indeterminate;
                    }
                }

                token.ThrowIfCancellationRequested();
            }
        }

        try
        {
            try
            {
                await ownerFence(Decision, token).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            if (calls != 1 || running is null || !running.IsCompleted)
            {
                throw new InvalidOperationException("CheckpointHold: owner skipped or returned before its decision completed.");
            }

            await running.ConfigureAwait(false);
            RequirePins();
            return outcome;
        }
        finally
        {
            open = false;
            CryptographicOperations.ZeroMemory(sourcePin);
            token.ThrowIfCancellationRequested();
        }
    }

    private async Task<DaprLogicalResponseOwner? []> ReadLogicalCheckpointRowsAsync(DaprLogicalCheckpointWrite write, CancellationToken token, Action? localFence = null)
    {
        await StateManager.ClearCacheAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        var rows = new DaprLogicalResponseOwner? [3];
        for (int index = 0; index < 3; index++)
        {
            ConditionalValue<byte[]> value = await StateManager.TryGetStateAsync<byte[]>(write.Key(index), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            localFence?.Invoke();
            rows[index] = value.HasValue ? write.CaptureRead(value.Value ?? throw new InvalidOperationException("CheckpointReadbackHold: present row has no typed value."), index) : null;
        }

        await StateManager.ClearCacheAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        localFence?.Invoke();
        return rows;
    }

    private async Task<DaprReplayCommitOutcome> InspectLogicalCheckpointAsync(DaprLogicalCheckpointWrite write)
    {
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            DaprLogicalResponseOwner? [] rows = await ReadLogicalCheckpointRowsAsync(write, recovery.Token).ConfigureAwait(false);
            DaprReplayCommitOutcome result = write.Classify(rows);
            write.ReleaseReads();
            return result;
        }
        catch
        {
            return DaprReplayCommitOutcome.Indeterminate;
        }
    }
}
