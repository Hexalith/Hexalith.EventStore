using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>Gets the bounded identity transport over this coordinator's exact store and recovery graph.</summary>
    public SharedProjectionCapturedIdentityStore CapturedIdentities => new(_store, this);

    /// <summary>Checks the final position of an incrementally validated original owner history.</summary>
    public Task<bool> MatchesCapturedOwnerLastPositionAsync(SharedProjectionScope scope, string operationId,
        string sourceStream, long lastPosition, CancellationToken cancellationToken = default)
        => MatchesCapturedOwnerPositionAsync(scope, operationId, sourceStream, new[] { lastPosition }, cancellationToken);

    /// <summary>Checks every retained owner prefix identity before promotion.</summary>
    public async Task<bool> CanPromoteCapturedOwnerIdentityAsync(SharedProjectionScope scope, string operationId,
        string sourceStream, long lastSequenceNumber, SharedProjectionCapturedIdentityReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        if (!await CanPromoteCapturedOwnerAsync(scope, operationId, sourceStream, lastSequenceNumber, cancellationToken).ConfigureAwait(false)) return false;
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        SharedProjectionCapturedIdentityStore.ValidateBinding(scope, reference, sourceStream, operationId, state.Epoch);
        if (reference.Count != lastSequenceNumber || reference.LastPosition != await GetCapturedPositionAsync(scope, state, sourceStream, cancellationToken).ConfigureAwait(false)) return false;
        await CapturedIdentities.ValidateCompleteAsync(scope, reference, cancellationToken).ConfigureAwait(false);
        SharedProjectionCheckpoint? checkpoint = await ReadOwnerCheckpointAsync(scope, state, sourceStream, cancellationToken).ConfigureAwait(false);
        return checkpoint is null || await CapturedIdentities.MatchesPrefixAsync(scope, reference, checkpoint, cancellationToken).ConfigureAwait(false)
            && await MatchesCheckpointPositionAsync(scope, reference, checkpoint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Proves pending original deliveries against bounded captured evidence.</summary>
    public async Task<bool> CanPromoteCapturedJournalOwnerIdentityAsync(SharedProjectionScope scope, string operationId,
        string sourceStream, long lastSequenceNumber, SharedProjectionCapturedIdentityReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building || state.OperationId != operationId) return false;
        SharedProjectionCapturedIdentityStore.ValidateBinding(scope, reference, sourceStream, operationId, state.Epoch);
        if (reference.Count != lastSequenceNumber) return false;
        foreach (var pending in state.Journal.Where(item => item.SourceStream == sourceStream))
        {
            if (pending.Parked || pending.Failure is not null || !pending.PayloadReady || pending.OwnerSequenceNumber < 1) return false;
            var delivery = await RestoreDeliveryAsync(scope, pending, cancellationToken).ConfigureAwait(false);
            byte[]? digest = await CapturedIdentities.ReadDigestAsync(scope, sourceStream,
                new SharedProjectionCheckpoint(reference.LastPosition, [], reference.Count) { CapturedIdentityReference = reference },
                pending.OwnerSequenceNumber, cancellationToken).ConfigureAwait(false);
            if (digest is null || !digest.AsSpan().SequenceEqual(SharedProjectionCapturedEventIdentity.Compute(sourceStream,
                pending.EnvelopePosition, pending.OwnerSequenceNumber, delivery.CanonicalPayload))) return false;
        }
        return true;
    }

    /// <summary>Seeds sealed paged evidence using recoverable checkpoint and epoch CAS fences.</summary>
    public Task SeedCapturedOwnerIdentityAsync(SharedProjectionScope scope, string operationId,
        string sourceStream, long lastSequenceNumber, SharedProjectionCapturedIdentityReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return SeedCapturedOwnerSequenceCoreAsync(scope, operationId, sourceStream, lastSequenceNumber, null, reference, cancellationToken);
    }

    private async Task SeedCapturedOwnerSequenceCoreAsync(SharedProjectionScope scope, string operationId,
        string sourceStream, long lastSequenceNumber, byte[]? inline,
        SharedProjectionCapturedIdentityReference? reference, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lastSequenceNumber);
        if (inline is not null && inline.LongLength != checked(lastSequenceNumber * 32))
            throw new ArgumentException("Captured event identities must cover the complete owner history.", nameof(inline));
        string key = scope.CheckpointKey(sourceStream);
        // The promotion's own cleanup recovers any pending epoch envelope it observes; overlapping
        // it would co-execute this fenced handoff batch and leave its outcome indeterminate.
        await WaitForPromotedCleanupAsync(scope, token).ConfigureAwait(false);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            // Recovery can change both original participants; always reread after it settles.
            if (await ReconcilePendingScratchAsync(scope, key, token).ConfigureAwait(false)
                || await ReconcilePendingScratchAsync(scope, scope.StateKey, token).ConfigureAwait(false)) continue;
            var epoch = await ReadStateAsync(scope, token).ConfigureAwait(false);
            SharedProjectionEpochState state = epoch.Value ?? throw new InvalidOperationException("The shared projection has no selected rebuild.");
            ValidateState(state); ValidateBoundary(state, scope);
            if (state.ActiveGeneration != state.Epoch || state.OperationId != operationId
                || state.Phase is not (SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Open))
                throw new InvalidOperationException("The requested rebuild has not been promoted.");
            long captured = await GetCapturedPositionAsync(scope, state, sourceStream, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The owner is absent from the rebuild capture.");
            if (reference is not null)
            {
                SharedProjectionCapturedIdentityStore.ValidateBinding(scope, reference, sourceStream, operationId, state.Epoch);
                if (reference.Count != lastSequenceNumber || reference.LastPosition != captured)
                    throw new InvalidOperationException("Captured identity reference does not cover the complete captured owner.");
                await CapturedIdentities.ValidateCompleteAsync(scope, reference, token).ConfigureAwait(false);
            }
            var entry = await _store.GetAsync<SharedProjectionCheckpoint>(scope.StoreName, key, token).ConfigureAwait(false);
            var existing = await ReadOwnerCheckpointAsync(scope, state, sourceStream, token).ConfigureAwait(false);
            if (existing is not null)
            {
                SharedProjectionCapturedIdentityStore.ValidateCheckpoint(existing);
                if (existing.Position > captured || existing.OwnerSequenceNumber > lastSequenceNumber
                    || existing.OwnerSequenceNumber == lastSequenceNumber && existing.Position != captured)
                    throw new InvalidOperationException("The owner checkpoint exceeds or conflicts with its rebuild capture.");
                if (reference is not null)
                {
                    if (!await CapturedIdentities.MatchesPrefixAsync(scope, reference, existing, token).ConfigureAwait(false)
                        || !await MatchesCheckpointPositionAsync(scope, reference, existing, token).ConfigureAwait(false))
                        throw new InvalidOperationException("Captured identity evidence conflicts with the retained owner prefix.");
                }
                else if (existing.CapturedIdentityReference is { } retainedReference)
                {
                    await CapturedIdentities.ValidateCompleteAsync(scope, retainedReference, token).ConfigureAwait(false);
                    if (lastSequenceNumber != retainedReference.Count || lastSequenceNumber != existing.OwnerSequenceNumber)
                        throw new InvalidOperationException("Paged owner evidence can only be extended through bounded identity seeding.");
                    for (long sequence = 1; inline is not null && sequence <= retainedReference.Count; sequence++)
                    {
                        byte[]? digest = await CapturedIdentities.ReadDigestAsync(scope, sourceStream, existing, sequence, token).ConfigureAwait(false);
                        if (digest is null || !digest.AsSpan().SequenceEqual(inline.AsSpan(checked((int)(sequence - 1) * 32), 32)))
                            throw new InvalidOperationException("Captured owner event identities conflict with the checkpoint.");
                    }
                    // A legacy retry may acknowledge the already-validated prefix, but must
                    // retain its paged proof rather than replace it with an inline vector.
                    return;
                }
                else if (existing.CapturedEventDigests is { } known && (inline is null
                    ? existing.OwnerSequenceNumber < lastSequenceNumber
                    : known.Length > inline.Length || !known.AsSpan().SequenceEqual(inline.AsSpan(0, known.Length))))
                    throw new InvalidOperationException("Captured owner event identities conflict with the checkpoint.");
                if (existing.OwnerSequenceNumber == lastSequenceNumber
                    && (reference is not null ? existing.CapturedIdentityReference == reference
                        : inline is null || existing.CapturedEventDigests?.Length == inline.Length)) return;
            }
            if (reference is not null && !state.OwnerSequenceHandoffPending && existing?.OwnerSequenceNumber != lastSequenceNumber)
                throw new InvalidOperationException("The selected generation is not holding its checkpoint handoff fence.");
            var checkpoint = new SharedProjectionCheckpoint(captured, JsonSerializer.SerializeToUtf8Bytes(captured),
                lastSequenceNumber, inline?.ToArray()) { CapturedIdentityReference = reference };
            string identity = "identity-seed:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                scope.ComputeHash() + ":" + operationId + ":" + sourceStream + ":" + epoch.ETag + ":" + entry.ETag + ":"
                + (reference?.RootDigest ?? (inline is null ? "legacy" : Convert.ToHexString(SHA256.HashData(inline)))))));
            if (await TryWriteCapturedIdentityRecordsAsync(scope, operationId, state.Epoch, identity,
                [ReadModelBatchOperation.Write(key, checkpoint, entry.ETag is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(entry.ETag))],
                token, promoted: true, expectedEpochETag: epoch.ETag).ConfigureAwait(false)) return;
        }
        throw RetryExhausted();
    }

    private async Task<bool> MatchesCheckpointPositionAsync(SharedProjectionScope scope,
        SharedProjectionCapturedIdentityReference reference, SharedProjectionCheckpoint checkpoint, CancellationToken token)
    {
        if (checkpoint.OwnerSequenceNumber == 0) return true;
        if (checkpoint.OwnerSequenceNumber > reference.Count) return false;
        var page = await CapturedIdentities.ReadPageAsync(scope, reference, checkpoint.OwnerSequenceNumber, token).ConfigureAwait(false);
        return page.Positions[checked((int)(checkpoint.OwnerSequenceNumber - page.FirstSequence))] == checkpoint.Position;
    }

    /// <summary>Reconciles original identity participants before reloading their exact ETags.</summary>
    internal Task<bool> ReconcileCapturedIdentityKeyAsync(SharedProjectionScope scope, string key, CancellationToken token)
        => ReconcilePendingScratchAsync(scope, key, token);

    /// <summary>Installs bounded identity records against the current epoch and supplied participant fences.</summary>
    internal async Task<bool> TryWriteCapturedIdentityRecordsAsync(SharedProjectionScope scope,
        string operationId, long epoch, string batchId, IReadOnlyList<ReadModelBatchOperation> operations,
        CancellationToken token, bool promoted = false, string? expectedEpochETag = null)
    {
        if (await ReconcileScratchBatchAsync(scope, batchId, token).ConfigureAwait(false)) return false;
        foreach (var operation in operations)
            if (await ReconcilePendingScratchAsync(scope, operation.Key, token).ConfigureAwait(false)) return false;
        if (await ReconcilePendingScratchAsync(scope, scope.StateKey, token).ConfigureAwait(false)) return false;
        string retainedId = await ScratchBatchIdentityAsync(scope, batchId, token).ConfigureAwait(false);
        var entry = await ReadStateAsync(scope, token).ConfigureAwait(false);
        SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The captured identity epoch is missing.");
        ValidateState(state); ValidateBoundary(state, scope);
        if (expectedEpochETag is not null && entry.ETag != expectedEpochETag) return false;
        if (state.Epoch != epoch || state.OperationId != operationId || entry.ETag is null
            || (promoted ? state.ActiveGeneration != state.Epoch
                || state.Phase is not (SharedProjectionEpochPhase.Open or SharedProjectionEpochPhase.CatchingUp)
                : state.Phase != SharedProjectionEpochPhase.Building))
            throw new InvalidOperationException("The captured identity installer lost its epoch fence.");
        var batch = new ReadModelBatch(new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain,
            scope.Family, scope.Family, retainedId),
            // Install the epoch participant first: a plan first prepared after abort/retirement
            // must fail before it can recreate any identity node or message-index evidence.
            [ReadModelBatchOperation.Write(scope.StateKey, state, ReadModelBatchConcurrency.Match(entry.ETag)), .. operations])
            { RetainManifestForRecovery = true };
        var result = await _batches.ExecuteAsync(batch, token).ConfigureAwait(false);
        if (result.IsSuccess) return true;
        if (result.Status == ReadModelBatchStatus.Conflict) return false;
        throw new SharedProjectionWriteUnavailableException(new InvalidOperationException("Captured identity handoff requires durable batch reconciliation."));
    }
}
