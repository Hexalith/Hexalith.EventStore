using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Durable, generation-selected fence for one tenant's shared projection family. All admitted
/// ordinary writers journal through this coordinator before acknowledgement; readers use
/// <see cref="ReadAsync{TValue}"/> rather than the underlying raw state keys.
/// </summary>
public sealed class SharedProjectionEpochCoordinator
{
    private const int MaxJournalEntries = 128;
    private const int MaxDeliveryBytes = 32 * 1024 * 1024;
    private const int MaxJournalBytes = 64 * 1024;
    private const int MaxEpochStateBytes = 512 * 1024;
    private const int MaxCheckpointBytes = 8 * 1024;
    private const int ChunkedDeliveryThresholdBytes = 16 * 1024;
    private const int MaxFailureAttempts = 1000;
    private const int MaxRetries = 64;
    private const int MaxRetainedControlIndexes = 8;
    private const int ChunkedStageThresholdBytes = 16 * 1024;
    private const int ChunkedCaptureThresholdBytes = 16 * 1024;

    private readonly IReadModelStore _store;
    private readonly IReadModelBatchStore _batches;
    private readonly SharedProjectionControlIndexWriter _controlIndexes;

    /// <summary>Creates a coordinator over one durable read-model and batch-store pair.</summary>
    public SharedProjectionEpochCoordinator(IReadModelStore store, IReadModelBatchStore batches)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _batches = batches ?? throw new ArgumentNullException(nameof(batches));
        _controlIndexes = new SharedProjectionControlIndexWriter(store);
    }

    /// <summary>Registers one required writer and returns its current epoch lease.</summary>
    public async Task<SharedProjectionLease> RegisterWriterAsync(
        SharedProjectionScope scope,
        string writerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateWriter(scope, writerId);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState current = entry.Value ?? SharedProjectionEpochState.Empty;
            ValidateState(current);
            ValidateBoundary(current, scope);
            ThrowIfOffboarded(current);
            if (current.RegisteredWriters.Contains(writerId, StringComparer.Ordinal))
            {
                return new SharedProjectionLease(scope.ComputeHash(), writerId, current.Epoch);
            }

            SharedProjectionEpochState updated = current with
            {
                RequiredWriters = entry.Value is null ? [.. scope.RequiredWriters] : current.RequiredWriters,
                RegisteredWriters = [.. current.RegisteredWriters, writerId],
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return new SharedProjectionLease(scope.ComputeHash(), writerId, updated.Epoch);
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Refreshes a registered writer's token after a rebuild increments the epoch.</summary>
    public async Task<SharedProjectionLease> RefreshLeaseAsync(
        SharedProjectionScope scope,
        string writerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateWriter(scope, writerId);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        ThrowIfOffboarded(state);
        if (!state.RegisteredWriters.Contains(writerId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The shared projection writer has not registered its protocol lease.");
        }

        return new SharedProjectionLease(scope.ComputeHash(), writerId, state.Epoch);
    }

    /// <summary>
    /// Seals inventory and source high-watermarks with a single CAS. The call fails while a live
    /// journal entry is still being applied or any required writer lacks protocol registration.
    /// </summary>
    public async Task<long> BeginAsync(
        SharedProjectionScope scope,
        string operationId,
        string inventoryFingerprint,
        IReadOnlyDictionary<string, long> sourceHighWatermarks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryFingerprint);
        if (Encoding.UTF8.GetByteCount(operationId) > ReadModelBatchScope.MaxComponentByteLength
            || Encoding.UTF8.GetByteCount(inventoryFingerprint) > ReadModelBatchScope.MaxComponentByteLength)
        {
            throw new ArgumentException("The shared rebuild capture identity exceeds the store component limit.");
        }

        ArgumentNullException.ThrowIfNull(sourceHighWatermarks);
        if (sourceHighWatermarks.Any(item => string.IsNullOrWhiteSpace(item.Key)
            || Encoding.UTF8.GetByteCount(item.Key) > ReadModelBatchScope.MaxComponentByteLength
            || item.Value < 0))
        {
            throw new ArgumentException("Capture high-watermarks must name nonnegative source positions.", nameof(sourceHighWatermarks));
        }

        var sortedCapture = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> item in sourceHighWatermarks.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            sortedCapture.Add(item.Key, item.Value);
        }

        byte[] captureBytes = JsonSerializer.SerializeToUtf8Bytes(sortedCapture);
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? SharedProjectionEpochState.Empty;
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            if (state.CleanupChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.Phase == SharedProjectionEpochPhase.Open
                && state.ActiveGeneration == state.Epoch
                && string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
            {
                if (!CaptureMatches(state, inventoryFingerprint, sourceHighWatermarks, captureBytes))
                {
                    throw new InvalidOperationException("The shared rebuild capture conflicts with its durable epoch.");
                }

                return state.Epoch;
            }
            if (state.Phase == SharedProjectionEpochPhase.Capturing
                && string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                && CaptureMatches(state, inventoryFingerprint, sourceHighWatermarks, captureBytes))
            {
                SharedProjectionChunkReference reference = state.CaptureChunks
                    ?? throw new InvalidOperationException("The shared rebuild capture has no reserved chunks.");
                await new SharedProjectionChunkStore(_store)
                    .WriteAsync(scope, reference, captureBytes, cancellationToken).ConfigureAwait(false);
                if (await SaveAsync(scope, state with { Phase = SharedProjectionEpochPhase.Building }, entry.ETag, cancellationToken)
                    .ConfigureAwait(false))
                {
                    await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                    return state.Epoch;
                }

                continue;
            }

            if (state.Phase != SharedProjectionEpochPhase.Open)
            {
                if (string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                    && CaptureMatches(state, inventoryFingerprint, sourceHighWatermarks, captureBytes)
                    && state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.CatchingUp)
                {
                    return state.Epoch;
                }

                throw new InvalidOperationException("Another shared rebuild is active for this tenant/family.");
            }

            if (state.Journal.Length != 0
                || scope.RequiredWriters.Any(writer => !state.RegisteredWriters.Contains(writer, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("Shared rebuild cannot begin until all writers are registered and live delivery is drained.");
            }

            bool chunkedCapture = captureBytes.Length > ChunkedCaptureThresholdBytes;
            SharedProjectionChunkReference? captureChunks = chunkedCapture
                ? SharedProjectionChunkStore.Describe(
                    "capture",
                    checked(state.Epoch + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + operationId,
                    captureBytes)
                : null;
            SharedProjectionEpochState updated = state with
            {
                Epoch = checked(state.Epoch + 1),
                Phase = chunkedCapture ? SharedProjectionEpochPhase.Capturing : SharedProjectionEpochPhase.Building,
                OperationId = operationId,
                InventoryFingerprint = inventoryFingerprint,
                CaptureHighWatermarks = chunkedCapture ? new Dictionary<string, long>(StringComparer.Ordinal) : sortedCapture,
                CaptureChunks = captureChunks,
                CleanupChunks = state.CaptureChunks,
                StageFingerprint = null,
                StageKeys = [],
                StageMutations = null,
                StageManifestDigest = null,
                StageControlIndexIntent = null,
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                if (!chunkedCapture)
                {
                    await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                    return updated.Epoch;
                }
            }
        }

        throw RetryExhausted();
    }

    /// <summary>
    /// Atomically persists an admitted ordinary delivery and its consumer checkpoint. A stale
    /// lease, conflict, or full journal is never an acknowledgement.
    /// </summary>
    public async Task<SharedProjectionJournalResult> JournalAsync(
        SharedProjectionScope scope,
        SharedProjectionLease lease,
        SharedProjectionDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrWhiteSpace(delivery.SourceStream);
        if (delivery.EnvelopePosition < 0 || delivery.CanonicalPayload is null || delivery.Checkpoint is null
            || Encoding.UTF8.GetByteCount(delivery.SourceStream) > ReadModelBatchScope.MaxComponentByteLength
            || delivery.Checkpoint.Length > MaxCheckpointBytes
            || (long)delivery.CanonicalPayload.Length + delivery.Checkpoint.Length > MaxDeliveryBytes)
        {
            throw new ArgumentException("The shared projection delivery is invalid or over limit.", nameof(delivery));
        }

        if (delivery.ControlIndexIntent is { } controlIntent)
        {
            _ = SharedProjectionControlIndexWriter.StateKey(controlIntent.IndexName);
        }

        string digest = ComputeDeliveryDigest(delivery);
        bool chunked = (long)delivery.CanonicalPayload.Length + delivery.Checkpoint.Length > ChunkedDeliveryThresholdBytes;
        byte[]? payloadBytes = chunked
            ? JsonSerializer.SerializeToUtf8Bytes(new SharedProjectionPayload(delivery.CanonicalPayload, delivery.Checkpoint))
            : null;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The shared projection has no registered writers.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            if (!string.Equals(lease.ScopeHash, scope.ComputeHash(), StringComparison.Ordinal)
                || !scope.RequiredWriters.Contains(lease.WriterId, StringComparer.Ordinal)
                || !state.RegisteredWriters.Contains(lease.WriterId, StringComparer.Ordinal)
                || lease.Epoch != state.Epoch)
            {
                return SharedProjectionJournalResult.StaleLease;
            }

            if (state.Phase is SharedProjectionEpochPhase.Capturing or SharedProjectionEpochPhase.Aborting)
            {
                return SharedProjectionJournalResult.Backpressure;
            }

            SharedProjectionJournalEntry? existing = state.Journal.FirstOrDefault(item =>
                string.Equals(item.SourceStream, delivery.SourceStream, StringComparison.Ordinal)
                && item.EnvelopePosition == delivery.EnvelopePosition);
            if (existing is not null)
            {
                if (!string.Equals(existing.Digest, digest, StringComparison.Ordinal))
                {
                    return SharedProjectionJournalResult.IdentityConflict;
                }

                if (!existing.PayloadReady)
                {
                    SharedProjectionChunkReference expected = SharedProjectionChunkStore.Describe(
                        "delivery",
                        DeliveryChunkIdentity(state.Epoch, delivery.SourceStream, delivery.EnvelopePosition),
                        payloadBytes ?? JsonSerializer.SerializeToUtf8Bytes(
                            new SharedProjectionPayload(delivery.CanonicalPayload, delivery.Checkpoint)));
                    if (existing.PayloadChunks != expected)
                    {
                        return SharedProjectionJournalResult.IdentityConflict;
                    }

                    await new SharedProjectionChunkStore(_store)
                        .WriteAsync(scope, expected, payloadBytes!, cancellationToken).ConfigureAwait(false);
                    SharedProjectionEpochState ready = state with
                    {
                        Journal = [.. state.Journal.Select(item => ReferenceEquals(item, existing)
                            ? item with { PayloadReady = true }
                            : item)],
                    };
                    if (await SaveAsync(scope, ready, entry.ETag, cancellationToken).ConfigureAwait(false))
                    {
                        await EnsureCheckpointAsync(scope, delivery, cancellationToken).ConfigureAwait(false);
                        return SharedProjectionJournalResult.Journaled;
                    }

                    continue;
                }

                string[] retained = RetainControlIndexName(state, delivery.ControlIndexIntent);
                if (retained.Length != (state.RetainedControlIndexNames?.Length ?? 0))
                {
                    if (!await SaveAsync(scope, state with { RetainedControlIndexNames = retained }, entry.ETag, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        continue;
                    }
                }

                await ApplyControlIndexAsync(scope, delivery.ControlIndexIntent, cancellationToken).ConfigureAwait(false);
                await EnsureCheckpointAsync(scope, delivery, cancellationToken).ConfigureAwait(false);
                return SharedProjectionJournalResult.AlreadyJournaled;
            }

            ReadModelEntry<SharedProjectionDeliveryReceipt> receipt = await _store
                .GetAsync<SharedProjectionDeliveryReceipt>(
                    scope.StoreName,
                    scope.ReceiptKey(state.Epoch, delivery.SourceStream, delivery.EnvelopePosition),
                    cancellationToken)
                .ConfigureAwait(false);
            if (receipt.Value is not null)
            {
                if (!string.Equals(receipt.Value.Digest, digest, StringComparison.Ordinal))
                {
                    return SharedProjectionJournalResult.IdentityConflict;
                }

                string[] retained = RetainControlIndexName(state, delivery.ControlIndexIntent);
                if (retained.Length != (state.RetainedControlIndexNames?.Length ?? 0))
                {
                    if (!await SaveAsync(scope, state with { RetainedControlIndexNames = retained }, entry.ETag, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        continue;
                    }
                }

                await ApplyControlIndexAsync(scope, delivery.ControlIndexIntent, cancellationToken).ConfigureAwait(false);
                await EnsureCheckpointAsync(scope, delivery, cancellationToken).ConfigureAwait(false);
                return SharedProjectionJournalResult.AlreadyJournaled;
            }

            long? captured = await GetCapturedPositionAsync(scope, state, delivery.SourceStream, cancellationToken)
                .ConfigureAwait(false);
            if (captured is { } watermark && delivery.EnvelopePosition <= watermark)
            {
                // A building generation may still abort. Until it is promoted, the old generation
                // is not proved to contain captured positions, so do not acknowledge their delivery.
                if (state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.Aborting)
                {
                    return SharedProjectionJournalResult.Backpressure;
                }

                if (state.ActiveGeneration == state.Epoch)
                {
                    return SharedProjectionJournalResult.AlreadyCaptured;
                }
            }

            if (state.Journal.Length >= MaxJournalEntries
                || state.Journal.Sum(item => (long)item.CanonicalPayload.Length + item.Checkpoint.Length)
                    + (chunked ? 0 : delivery.CanonicalPayload.Length + delivery.Checkpoint.Length) > MaxJournalBytes)
            {
                return SharedProjectionJournalResult.Backpressure;
            }

            // The global append-only membership goes first. A crash here can leave only an extra
            // discoverable tenant; the reverse order could strand an acknowledged tenant journal
            // that no cross-tenant recovery reader can discover after restart.
            string[] retainedControlIndexes = RetainControlIndexName(state, delivery.ControlIndexIntent);
            await ApplyControlIndexAsync(scope, delivery.ControlIndexIntent, cancellationToken).ConfigureAwait(false);

            SharedProjectionChunkReference? payloadChunks = chunked
                ? SharedProjectionChunkStore.Describe(
                    "delivery",
                    DeliveryChunkIdentity(state.Epoch, delivery.SourceStream, delivery.EnvelopePosition),
                    payloadBytes!)
                : null;
            var journaled = new SharedProjectionJournalEntry(
                delivery.SourceStream,
                delivery.EnvelopePosition,
                digest,
                chunked ? [] : delivery.CanonicalPayload.ToArray(),
                chunked ? [] : delivery.Checkpoint.ToArray(),
                null,
                delivery.ControlIndexIntent,
                PayloadChunks: payloadChunks,
                PayloadReady: !chunked);
            SharedProjectionEpochState updated = state with
            {
                Journal = [.. state.Journal, journaled],
                RetainedControlIndexNames = retainedControlIndexes,
            };
            if (JsonSerializer.SerializeToUtf8Bytes(updated).Length > MaxEpochStateBytes)
            {
                return SharedProjectionJournalResult.Backpressure;
            }

            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                if (!chunked)
                {
                    await EnsureCheckpointAsync(scope, delivery, cancellationToken).ConfigureAwait(false);
                    return SharedProjectionJournalResult.Journaled;
                }
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Writes a complete replacement to new physical keys without changing the reader selector.</summary>
    public async Task<string> StageAsync(
        SharedProjectionScope scope,
        string operationId,
        IReadOnlyList<ReadModelBatchOperation> manifest,
        CancellationToken cancellationToken = default,
        SharedProjectionControlIndexIntent? controlIndexIntent = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Count == 0 || manifest.Any(item => item is null))
        {
            throw new ArgumentException("A shared rebuild stage requires a complete, non-empty manifest.", nameof(manifest));
        }

        if (controlIndexIntent is not null)
        {
            _ = SharedProjectionControlIndexWriter.StateKey(controlIndexIntent.IndexName);
        }

        SharedProjectionMutation[] mutations = [.. manifest.Select(SharedProjectionMutation.FromOperation)];
        if (mutations.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != mutations.Length)
        {
            throw new ArgumentException("A shared rebuild stage contains duplicate logical keys.", nameof(manifest));
        }

        byte[] mutationBytes = JsonSerializer.SerializeToUtf8Bytes(mutations);
        string manifestDigest = Convert.ToHexString(SHA256.HashData(mutationBytes));

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild has not begun.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (state.StageManifestDigest is not null
                && !string.Equals(state.StageManifestDigest, manifestDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The shared rebuild stage identity conflicts with its reserved manifest.");
            }

            if (state.StageMutations is not null
                && !Equals(state.StageControlIndexIntent, controlIndexIntent))
            {
                throw new InvalidOperationException("The shared rebuild stage control-index intent conflicts with its reserved manifest.");
            }

            // A stage can introduce the first pending work for a tenant without an ordinary
            // delivery. Register discovery before reserving or promoting the new generation.
            string[] retainedControlIndexes = RetainControlIndexName(state, controlIndexIntent);
            await ApplyControlIndexAsync(scope, controlIndexIntent, cancellationToken).ConfigureAwait(false);

            if (mutationBytes.Length > ChunkedStageThresholdBytes || state.StageChunks is not null)
            {
                string? fingerprint = await StageChunkedAsync(
                    scope,
                    operationId,
                    state,
                    entry.ETag,
                    mutations,
                    mutationBytes,
                    controlIndexIntent,
                    retainedControlIndexes,
                    cancellationToken).ConfigureAwait(false);
                if (fingerprint is not null)
                {
                    return fingerprint;
                }

                continue;
            }

            if (state.StageMutations is null)
            {
                SharedProjectionEpochState reserved = state with
                {
                    StageMutations = mutations,
                    StageManifestDigest = manifestDigest,
                    StageKeys = [.. mutations.Select(item => item.Key)],
                    StageControlIndexIntent = controlIndexIntent,
                    RetainedControlIndexNames = retainedControlIndexes,
                };
                if (await SaveAsync(scope, reserved, entry.ETag, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                continue;
            }

            ReadModelBatch batch = BuildBatch(
                scope,
                state.Epoch,
                "stage:" + state.Epoch + ":" + operationId,
                state.StageMutations);
            ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("The shared rebuild stage is not durably complete: " + result.Status);
            }

            if (state.StageFingerprint is not null
                && !string.Equals(state.StageFingerprint, result.Fingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The shared rebuild stage identity conflicts with its durable manifest.");
            }

            SharedProjectionEpochState updated = state with
            {
                StageFingerprint = result.Fingerprint,
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return result.Fingerprint;
            }
        }

        throw RetryExhausted();
    }

    private async Task<string?> StageChunkedAsync(
        SharedProjectionScope scope,
        string operationId,
        SharedProjectionEpochState state,
        string? etag,
        SharedProjectionMutation[] mutations,
        byte[] mutationBytes,
        SharedProjectionControlIndexIntent? controlIndexIntent,
        string[] retainedControlIndexes,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SharedProjectionMutation[]> batches = PartitionMutations(mutations);
        SharedProjectionChunkReference reference = SharedProjectionChunkStore.Describe(
            "stage",
            state.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + operationId,
            mutationBytes);
        if (state.StageChunks is { } reserved && reserved != reference)
        {
            throw new InvalidOperationException("The shared rebuild stage conflicts with its reserved chunks.");
        }

        if (state.StageChunks is not null && !Equals(state.StageControlIndexIntent, controlIndexIntent))
        {
            throw new InvalidOperationException("The shared rebuild control-index intent conflicts with its reserved chunks.");
        }

        if (state.StageChunks is null)
        {
            SharedProjectionEpochState reservedState = state with
            {
                StageChunks = reference,
                StageManifestDigest = reference.Digest,
                StageControlIndexIntent = controlIndexIntent,
                RetainedControlIndexNames = retainedControlIndexes,
            };
            _ = await SaveAsync(scope, reservedState, etag, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (state.StageFingerprint is not null)
        {
            return state.StageFingerprint;
        }

        var chunks = new SharedProjectionChunkStore(_store);
        await chunks.WriteAsync(scope, reference, mutationBytes, cancellationToken).ConfigureAwait(false);
        if (!state.StageExecutionStarted)
        {
            _ = await SaveAsync(scope, state with { StageExecutionStarted = true }, etag, cancellationToken)
                .ConfigureAwait(false);
            return null;
        }

        await ExecuteMutationBatchesAsync(
            scope,
            state.Epoch,
            "stage:" + state.Epoch + ":" + operationId,
            batches,
            cancellationToken).ConfigureAwait(false);
        string fingerprint = "chunks:" + reference.Digest;
        return await SaveAsync(scope, state with { StageFingerprint = fingerprint }, etag, cancellationToken)
            .ConfigureAwait(false)
            ? fingerprint
            : null;
    }


    /// <summary>Atomically promotes the staged generation as the sole reader selector.</summary>
    public async Task CommitAsync(
        SharedProjectionScope scope,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild has not begun.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            if (string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                && state.ActiveGeneration == state.Epoch
                && state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Open)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }

            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (state.StageFingerprint is null)
            {
                throw new InvalidOperationException("The shared rebuild manifest has not been durably staged.");
            }

            await ApplyControlIndexAsync(scope, state.StageControlIndexIntent, cancellationToken).ConfigureAwait(false);

            SharedProjectionEpochState updated = state with
            {
                ActiveGeneration = state.Epoch,
                Phase = state.Journal.Length == 0
                    ? SharedProjectionEpochPhase.Open
                    : SharedProjectionEpochPhase.CatchingUp,
                StageKeys = [],
                StageMutations = null,
                StageManifestDigest = null,
                StageChunks = null,
                StageExecutionStarted = false,
                CleanupChunks = state.StageChunks,
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw RetryExhausted();
    }

    /// <summary>
    /// Applies ordered journal entries to the selected generation. The fold sees the current
    /// physical generation and returns logical mutations; its chosen mutations are persisted before
    /// the idempotent batch executes, making a crash/retry use exactly the same manifest.
    /// </summary>
    public async Task<int> CatchUpAsync(
        SharedProjectionScope scope,
        Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> fold,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentNullException.ThrowIfNull(fold);
        _ = await ReconcileControlIndexAsync(scope, cancellationToken).ConfigureAwait(false);
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        int completed = 0;
        for (; ; )
        {
            await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The shared projection has no epoch state.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.Capturing
                || state.Journal.Length == 0)
            {
                if (state.Phase == SharedProjectionEpochPhase.CatchingUp && state.Journal.Length == 0)
                {
                    SharedProjectionEpochState opened = state with { Phase = SharedProjectionEpochPhase.Open };
                    if (!await SaveAsync(scope, opened, entry.ETag, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }
                }

                return completed;
            }

            SharedProjectionJournalEntry head = state.Journal[0];
            if (!head.PayloadReady)
            {
                throw new InvalidOperationException("The shared projection delivery chunks are reserved but not acknowledged; source redelivery must complete them.");
            }

            await ApplyControlIndexAsync(scope, head.PreparedControlIndexIntent, cancellationToken).ConfigureAwait(false);
            if (head.Prepared is null)
            {
                SharedProjectionDelivery delivery = await RestoreDeliveryAsync(scope, head, cancellationToken)
                    .ConfigureAwait(false);
                IReadOnlyList<ReadModelBatchOperation> operations;
                SharedProjectionFailureStatus? failureStatus = head.Failure;
                bool parked = false;
                try
                {
                    operations = await fold(
                        delivery,
                        state.ActiveGeneration,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (SharedProjectionFoldFailureException failure)
                {
                    if (!string.Equals(failure.SourceStream, head.SourceStream, StringComparison.Ordinal)
                        || failure.RetryLimit > MaxFailureAttempts)
                    {
                        throw new InvalidOperationException(
                            "The fold failure is outside the journaled source stream or retry limit.",
                            failure);
                    }

                    int previousCount = head.Failure is { } previous
                        && string.Equals(previous.SourceStream, failure.SourceStream, StringComparison.Ordinal)
                        && previous.SourcePosition == failure.SourcePosition
                        ? previous.FailureCount
                        : 0;
                    int failureCount = checked(previousCount + 1);
                    failureStatus = new SharedProjectionFailureStatus(
                        failure.SourceStream,
                        failure.SourcePosition,
                        failureCount);
                    if (failureCount < failure.RetryLimit)
                    {
                        SharedProjectionEpochState retryable = state with
                        {
                            Journal = [head with { Failure = failureStatus }, .. state.Journal.Skip(1)],
                        };
                        if (!await SaveAsync(scope, retryable, entry.ETag, cancellationToken).ConfigureAwait(false))
                        {
                            continue;
                        }

                        throw new SharedProjectionRetryableFoldException(failureStatus);
                    }

                    operations = failure.CreateParkingMutations(failureCount);
                    if (operations is null || operations.Count == 0
                        || !operations.Any(item => item?.Kind == ReadModelBatchOperationKind.Write))
                    {
                        throw new InvalidOperationException("Terminal parking requires at least one logical write.", failure);
                    }

                    parked = true;
                }

                ArgumentNullException.ThrowIfNull(operations);
                if (operations.Any(item => item is null)
                    || operations.Count > MaxJournalEntries
                    || operations.Sum(item => (long)item.CanonicalValue.Length) > MaxJournalBytes
                    || operations.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != operations.Count)
                {
                    throw new InvalidOperationException("Catch-up fold returned invalid or duplicate logical mutations.");
                }

                SharedProjectionJournalEntry prepared = head with
                {
                    Prepared = [.. operations.Select(SharedProjectionMutation.FromOperation)],
                    Failure = failureStatus,
                    Parked = parked,
                };
                SharedProjectionEpochState updated = state with
                {
                    Journal = [prepared, .. state.Journal.Skip(1)],
                };
                if (!await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                continue;
            }

            if (head.Prepared.Length > 0)
            {
                string batchId = "catch-up:" + state.Epoch + ":" + HashIdentity(head.SourceStream, head.EnvelopePosition);
                ReadModelBatch batch = BuildBatch(scope, state.ActiveGeneration, batchId, head.Prepared);
                ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException("Shared projection catch-up is incomplete: " + result.Status);
                }
            }

            SharedProjectionDelivery checkpointDelivery = await RestoreDeliveryAsync(scope, head, cancellationToken)
                .ConfigureAwait(false);
            await EnsureCheckpointAsync(scope, checkpointDelivery, cancellationToken).ConfigureAwait(false);
            string receiptKey = scope.ReceiptKey(state.Epoch, head.SourceStream, head.EnvelopePosition);
            var receipt = new SharedProjectionDeliveryReceipt(head.Digest, head.Parked, head.Failure);
            if (!await _store.TrySaveAsync(scope.StoreName, receiptKey, receipt, string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                ReadModelEntry<SharedProjectionDeliveryReceipt> saved = await _store
                    .GetAsync<SharedProjectionDeliveryReceipt>(scope.StoreName, receiptKey, cancellationToken)
                    .ConfigureAwait(false);
                if (saved.Value is not { } existingReceipt
                    || !string.Equals(existingReceipt.Digest, head.Digest, StringComparison.Ordinal)
                    || existingReceipt.Parked != head.Parked
                    || existingReceipt.Failure != head.Failure)
                {
                    throw new InvalidOperationException("The shared projection receipt conflicts with its journaled digest.");
                }
            }

            SharedProjectionEpochState dequeued = state with
            {
                Journal = [.. state.Journal.Skip(1)],
                CleanupChunks = head.PayloadChunks,
            };
            if (dequeued.Journal.Length == 0 && dequeued.Phase == SharedProjectionEpochPhase.CatchingUp)
            {
                dequeued = dequeued with { Phase = SharedProjectionEpochPhase.Open };
            }

            if (await SaveAsync(scope, dequeued, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                completed++;
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Drains accepted updates into the old generation, deletes staging, and opens the fence.</summary>
    public async Task AbortAsync(
        SharedProjectionScope scope,
        string operationId,
        Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> fold,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(fold);
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild has not begun.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.Phase == SharedProjectionEpochPhase.Open
                && string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                && state.ActiveGeneration != state.Epoch)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (state.Phase == SharedProjectionEpochPhase.Aborting
                && !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The shared rebuild operation does not match the durable epoch.");
            }

            if (state.Phase != SharedProjectionEpochPhase.Aborting)
            {
                if (state.Phase is not (SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.Capturing)
                    || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The shared rebuild operation or phase does not match the durable epoch.");
                }

                if (!await SaveAsync(scope, state with { Phase = SharedProjectionEpochPhase.Aborting }, entry.ETag, cancellationToken)
                    .ConfigureAwait(false))
                {
                    continue;
                }
            }

            break;
        }

        _ = await CatchUpAsync(scope, fold, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild state disappeared.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Aborting);
            if (state.CleanupChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.Journal.Length != 0)
            {
                _ = await CatchUpAsync(scope, fold, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.StageMutations is { Length: > 0 })
            {
                ReadModelBatch staged = BuildBatch(
                    scope,
                    state.Epoch,
                    "stage:" + state.Epoch + ":" + operationId,
                    state.StageMutations);
                ReadModelBatchResult stagedResult = await _batches.ExecuteAsync(staged, cancellationToken).ConfigureAwait(false);
                if (!stagedResult.IsSuccess)
                {
                    throw new InvalidOperationException("Shared rebuild staged write cannot be reconciled before abort: " + stagedResult.Status);
                }

                ReadModelBatch cleanup = BuildBatch(
                    scope,
                    state.Epoch,
                    "abort:" + state.Epoch + ":" + operationId,
                    state.StageKeys.Select(key => new SharedProjectionMutation(
                        key,
                        ReadModelBatchOperationKind.Delete,
                        string.Empty,
                        [])));
                ReadModelBatchResult result = await _batches.ExecuteAsync(cleanup, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException("Shared rebuild staging cleanup is incomplete: " + result.Status);
                }
            }

            if (state.StageChunks is { } stageChunks && state.StageExecutionStarted)
            {
                byte[] bytes = await new SharedProjectionChunkStore(_store)
                    .ReadAsync(scope, stageChunks, cancellationToken).ConfigureAwait(false);
                SharedProjectionMutation[] mutations = JsonSerializer.Deserialize<SharedProjectionMutation[]>(bytes)
                    ?? throw new InvalidOperationException("The shared rebuild chunks contain no manifest.");
                await ExecuteMutationBatchesAsync(
                    scope,
                    state.Epoch,
                    "stage:" + state.Epoch + ":" + operationId,
                    PartitionMutations(mutations),
                    cancellationToken).ConfigureAwait(false);
                SharedProjectionMutation[] deletes = [.. mutations.Select(item => new SharedProjectionMutation(
                    item.Key,
                    ReadModelBatchOperationKind.Delete,
                    string.Empty,
                    []))];
                await ExecuteMutationBatchesAsync(
                    scope,
                    state.Epoch,
                    "abort:" + state.Epoch + ":" + operationId,
                    PartitionMutations(deletes),
                    cancellationToken).ConfigureAwait(false);
                if (await SaveAsync(scope, state with { StageExecutionStarted = false }, entry.ETag, cancellationToken)
                    .ConfigureAwait(false))
                {
                    continue;
                }

                continue;
            }

            if (state.StageChunks is { } abandonedChunks)
            {
                await new SharedProjectionChunkStore(_store)
                    .TombstoneAsync(scope, abandonedChunks, cancellationToken).ConfigureAwait(false);
            }


            SharedProjectionEpochState opened = state with
            {
                Phase = SharedProjectionEpochPhase.Open,
                StageKeys = [],
                StageFingerprint = null,
                StageMutations = null,
                StageManifestDigest = null,
                StageChunks = null,
                StageExecutionStarted = false,
                CaptureHighWatermarks = new Dictionary<string, long>(StringComparer.Ordinal),
                CaptureChunks = null,
                CleanupChunks = state.CaptureChunks,
            };
            if (await SaveAsync(scope, opened, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Reads only the generation selected by the durable manifest.</summary>
    public async Task<SharedProjectionReadResult<TValue>> ReadAsync<TValue>(
        SharedProjectionScope scope,
        string logicalKey,
        CancellationToken cancellationToken = default)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (state.OffboardingAuditId is not null)
        {
            return new SharedProjectionReadResult<TValue>(null, state.ActiveGeneration, false, false);
        }

        bool stale = state.Journal.Length > 0 || state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting;
        if (state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting)
        {
            return new SharedProjectionReadResult<TValue>(null, state.ActiveGeneration, true, false);
        }

        ReadModelEntry<TValue> entry = await ReadGenerationAsync<TValue>(
            scope,
            state.ActiveGeneration,
            logicalKey,
            cancellationToken).ConfigureAwait(false);
        return new SharedProjectionReadResult<TValue>(entry.Value, state.ActiveGeneration, stale, true);
    }

    /// <summary>Returns the checkpoint accepted atomically with the latest source journal entry.</summary>
    public async Task<SharedProjectionCheckpoint?> GetCheckpointAsync(
        SharedProjectionScope scope,
        string sourceStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        ReadModelEntry<SharedProjectionCheckpoint> indexed = await _store
            .GetAsync<SharedProjectionCheckpoint>(scope.StoreName, scope.CheckpointKey(sourceStream), cancellationToken)
            .ConfigureAwait(false);
        state.ConsumerCheckpoints.TryGetValue(sourceStream, out SharedProjectionCheckpoint? legacy);
        SharedProjectionCheckpoint? checkpoint = indexed.Value is { } current
            && (legacy is null || current.Position >= legacy.Position)
            ? current
            : legacy;
        return checkpoint is null ? null : checkpoint with { Token = checkpoint.Token.ToArray() };
    }

    /// <summary>Reads durable stage, promotion, and pending-delivery evidence.</summary>
    public async Task<SharedProjectionEpochStatus> GetStatusAsync(
        SharedProjectionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        _ = await ReconcileControlIndexAsync(scope, cancellationToken).ConfigureAwait(false);
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        return new SharedProjectionEpochStatus(
            state.Epoch,
            state.ActiveGeneration,
            state.OperationId,
            state.StageFingerprint,
            state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.Capturing,
            state.Phase == SharedProjectionEpochPhase.Aborting,
            state.Journal.Length,
            state.OffboardingAuditId is not null);
    }

    /// <summary>
    /// Reconciles every durable control-index intent in a tenant's journal after a crash. A
    /// successful return proves each pending intent has an atomic membership and scope receipt.
    /// </summary>
    public async Task<int> ReconcileControlIndexAsync(
        SharedProjectionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        var names = new HashSet<string>(state.RetainedControlIndexNames ?? [], StringComparer.Ordinal);
        if (state.StageControlIndexIntent is { } staged)
        {
            _ = names.Add(staged.IndexName);
        }

        foreach (SharedProjectionJournalEntry entry in state.Journal)
        {
            if (entry.PreparedControlIndexIntent is { } prepared)
            {
                _ = names.Add(prepared.IndexName);
            }
        }

        foreach (string name in names.Order(StringComparer.Ordinal))
        {
            if (state.OffboardingAuditId is { } auditId)
            {
                await _controlIndexes.TombstoneAndPruneAsync(
                    scope.StoreName,
                    name,
                    scope.TenantId,
                    auditId,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _controlIndexes.ApplyAsync(scope, new SharedProjectionControlIndexIntent(name), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return names.Count;
    }

    /// <summary>
    /// Fences a drained tenant, then writes audited tombstones and prunes its discovery entries.
    /// The caller must authorize offboarding and satisfy its legal-hold/retention policy first.
    /// A crash after the tenant fence is repaired by <see cref="GetStatusAsync"/>.
    /// </summary>
    public async Task OffboardTenantAsync(
        SharedProjectionScope scope,
        string auditId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(auditId);
        if (Encoding.UTF8.GetByteCount(auditId) > ReadModelBatchScope.MaxComponentByteLength)
        {
            throw new ArgumentException("The offboarding audit id exceeds the store component limit.", nameof(auditId));
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The tenant has no shared projection epoch.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.OffboardingAuditId is not null)
            {
                if (!string.Equals(state.OffboardingAuditId, auditId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The tenant offboarding audit id conflicts with its durable fence.");
                }

                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                _ = await ReconcileControlIndexAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (state.CleanupChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.Phase != SharedProjectionEpochPhase.Open || state.Journal.Length != 0)
            {
                throw new InvalidOperationException("Tenant offboarding requires an open, fully drained projection epoch.");
            }

            SharedProjectionEpochState offboarded = state with
            {
                OffboardingAuditId = auditId,
                CaptureChunks = null,
                CaptureHighWatermarks = new Dictionary<string, long>(StringComparer.Ordinal),
                CleanupChunks = state.CaptureChunks,
            };
            if (await SaveAsync(scope, offboarded, entry.ETag, cancellationToken)
                .ConfigureAwait(false))
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                _ = await ReconcileControlIndexAsync(scope, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw RetryExhausted();
    }

    /// <summary>
    /// Rebuilds control-index membership from authoritative tenant scopes after a global-index
    /// restore. The caller supplies the authoritative scope inventory and must withhold readiness
    /// if any scope fails reconciliation or the bounded index reaches capacity.
    /// </summary>
    public async Task<int> ReconcileControlIndexInventoryAsync(
        IReadOnlyList<SharedProjectionScope> authoritativeScopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authoritativeScopes);
        if (authoritativeScopes.Any(static scope => scope is null))
        {
            throw new ArgumentException("The control-index recovery inventory contains a null scope.", nameof(authoritativeScopes));
        }

        string[] hashes = [.. authoritativeScopes.Select(scope => scope.ComputeHash())];
        if (hashes.Distinct(StringComparer.Ordinal).Count() != hashes.Length)
        {
            throw new ArgumentException("The control-index recovery inventory contains duplicate tenant scopes.", nameof(authoritativeScopes));
        }

        int reconciled = 0;
        foreach (SharedProjectionScope scope in authoritativeScopes)
        {
            reconciled = checked(reconciled + await ReconcileControlIndexAsync(scope, cancellationToken)
                .ConfigureAwait(false));
        }

        return reconciled;
    }

    /// <summary>Reads the tenants durably registered in a store-local discovery index.</summary>
    public Task<IReadOnlyList<string>> GetControlIndexTenantsAsync(
        string storeName,
        string indexName,
        CancellationToken cancellationToken = default)
        => _controlIndexes.ReadTenantsAsync(storeName, indexName, cancellationToken);

    /// <summary>Reads several keys from one pinned committed generation.</summary>
    public async Task<SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>> ReadManyAsync<TValue>(
        SharedProjectionScope scope,
        IReadOnlyList<string> logicalKeys,
        CancellationToken cancellationToken = default)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentNullException.ThrowIfNull(logicalKeys);
        if (logicalKeys.Any(string.IsNullOrWhiteSpace)
            || logicalKeys.Distinct(StringComparer.Ordinal).Count() != logicalKeys.Count)
        {
            throw new ArgumentException("Shared projection read keys must be distinct and non-empty.", nameof(logicalKeys));
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> before = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = before.Value ?? throw new InvalidOperationException("The shared projection has no epoch state.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.OffboardingAuditId is not null)
            {
                return new SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>(null, state.ActiveGeneration, false, false);
            }

            bool stale = state.Journal.Length > 0
                || state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting;
            if (state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting
                || (state.Phase == SharedProjectionEpochPhase.Open && state.Journal.Length > 0))
            {
                return new SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>(null, state.ActiveGeneration, true, false);
            }

            var values = new Dictionary<string, TValue?>(StringComparer.Ordinal);
            foreach (string key in logicalKeys)
            {
                ReadModelEntry<TValue> entry = await ReadGenerationAsync<TValue>(
                    scope,
                    state.ActiveGeneration,
                    key,
                    cancellationToken).ConfigureAwait(false);
                values.Add(key, entry.Value);
            }

            ReadModelEntry<SharedProjectionEpochState> after = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            if (string.Equals(before.ETag, after.ETag, StringComparison.Ordinal))
            {
                return new SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>(values, state.ActiveGeneration, stale, true);
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Reads a specific physical generation for deterministic catch-up folding.</summary>
    public Task<ReadModelEntry<TValue>> ReadGenerationAsync<TValue>(
        SharedProjectionScope scope,
        long generation,
        string logicalKey,
        CancellationToken cancellationToken = default)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        return _store.GetAsync<TValue>(scope.StoreName, scope.PhysicalKey(generation, logicalKey), cancellationToken);
    }

    private static ReadModelBatch BuildBatch(
        SharedProjectionScope scope,
        long generation,
        string batchId,
        IEnumerable<SharedProjectionMutation> mutations)
    {
        string stableBatchId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(batchId)));
        var identity = new ReadModelBatchScope(
            scope.StoreName,
            scope.TenantId,
            scope.Domain,
            "shared-" + scope.Family,
            scope.Family,
            stableBatchId);
        return new ReadModelBatch(identity, mutations.Select(item => item.ToOperation(scope.PhysicalKey(generation, item.Key))));
    }

    private static IReadOnlyList<SharedProjectionMutation[]> PartitionMutations(
        IReadOnlyList<SharedProjectionMutation> mutations)
    {
        var batches = new List<SharedProjectionMutation[]>();
        var current = new List<SharedProjectionMutation>(32);
        int bytes = 0;
        foreach (SharedProjectionMutation mutation in mutations)
        {
            if (mutation is null || mutation.CanonicalValue is null || string.IsNullOrWhiteSpace(mutation.Key))
            {
                throw new InvalidOperationException("The shared projection manifest contains an invalid mutation.");
            }

            int size = checked(mutation.CanonicalValue.Length
                + Encoding.UTF8.GetByteCount(mutation.Key)
                + Encoding.UTF8.GetByteCount(mutation.ValueTypeName)
                + 1024);
            if (size > 256 * 1024)
            {
                throw new InvalidOperationException("One shared projection mutation exceeds the bounded batch limit.");
            }

            if (current.Count == 32 || bytes + size > 256 * 1024)
            {
                batches.Add([.. current]);
                current.Clear();
                bytes = 0;
            }

            current.Add(mutation);
            bytes += size;
        }

        if (current.Count > 0)
        {
            batches.Add([.. current]);
        }

        return batches;
    }

    private async Task ExecuteMutationBatchesAsync(
        SharedProjectionScope scope,
        long generation,
        string batchPrefix,
        IReadOnlyList<SharedProjectionMutation[]> batches,
        CancellationToken cancellationToken)
    {
        for (int ordinal = 0; ordinal < batches.Count; ordinal++)
        {
            ReadModelBatch batch = BuildBatch(scope, generation, batchPrefix + ":" + ordinal, batches[ordinal]);
            ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("Shared projection chunk batch is incomplete: " + result.Status);
            }
        }
    }

    private async Task ReconcileChunkCleanupAsync(SharedProjectionScope scope, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            if (entry.Value?.CleanupChunks is not { } reference)
            {
                return;
            }

            await new SharedProjectionChunkStore(_store)
                .TombstoneAsync(scope, reference, cancellationToken).ConfigureAwait(false);
            if (await SaveAsync(scope, entry.Value with { CleanupChunks = null }, entry.ETag, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw RetryExhausted();
    }

    private async Task<SharedProjectionDelivery> RestoreDeliveryAsync(
        SharedProjectionScope scope,
        SharedProjectionJournalEntry head,
        CancellationToken cancellationToken)
    {
        SharedProjectionPayload payload;
        if (head.PayloadChunks is { } reference)
        {
            byte[] bytes = await new SharedProjectionChunkStore(_store)
                .ReadAsync(scope, reference, cancellationToken).ConfigureAwait(false);
            payload = JsonSerializer.Deserialize<SharedProjectionPayload>(bytes)
                ?? throw new InvalidOperationException("The shared projection delivery chunks contain no payload.");
        }
        else
        {
            payload = new SharedProjectionPayload(head.CanonicalPayload, head.Checkpoint);
        }

        if (payload.CanonicalPayload is null || payload.Checkpoint is null)
        {
            throw new InvalidOperationException("The shared projection delivery payload is incomplete.");
        }

        var delivery = new SharedProjectionDelivery(
            head.SourceStream,
            head.EnvelopePosition,
            payload.CanonicalPayload.ToArray(),
            payload.Checkpoint.ToArray(),
            head.PreparedControlIndexIntent);
        if (!string.Equals(ComputeDeliveryDigest(delivery), head.Digest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared projection delivery payload conflicts with its journaled digest.");
        }

        return delivery;
    }

    private async Task EnsureCheckpointAsync(
        SharedProjectionScope scope,
        SharedProjectionDelivery delivery,
        CancellationToken cancellationToken)
    {
        string key = scope.CheckpointKey(delivery.SourceStream);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionCheckpoint> entry = await _store
                .GetAsync<SharedProjectionCheckpoint>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value is { } existing)
            {
                if (existing.Position > delivery.EnvelopePosition)
                {
                    return;
                }

                if (existing.Position == delivery.EnvelopePosition)
                {
                    if (!existing.Token.AsSpan().SequenceEqual(delivery.Checkpoint))
                    {
                        throw new InvalidOperationException("The shared projection checkpoint conflicts at the same source position.");
                    }

                    return;
                }
            }

            var checkpoint = new SharedProjectionCheckpoint(
                delivery.EnvelopePosition,
                delivery.Checkpoint.ToArray());
            if (await _store.TrySaveAsync(scope.StoreName, key, checkpoint, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw RetryExhausted();
    }

    private static string DeliveryChunkIdentity(long epoch, string sourceStream, long position)
        => epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + HashIdentity(sourceStream, position);


    private Task ApplyControlIndexAsync(
        SharedProjectionScope scope,
        SharedProjectionControlIndexIntent? intent,
        CancellationToken cancellationToken)
        => intent is null ? Task.CompletedTask : _controlIndexes.ApplyAsync(scope, intent, cancellationToken);

    private static string ComputeDeliveryDigest(SharedProjectionDelivery delivery)
    {
        byte[] stream = Encoding.UTF8.GetBytes(delivery.SourceStream);
        byte[] bytes = new byte[checked(20 + stream.Length + delivery.CanonicalPayload.Length + delivery.Checkpoint.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(), stream.Length);
        stream.CopyTo(bytes.AsSpan(4));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4 + stream.Length), delivery.EnvelopePosition);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12 + stream.Length), delivery.CanonicalPayload.Length);
        delivery.CanonicalPayload.CopyTo(bytes.AsSpan(16 + stream.Length));
        BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(16 + stream.Length + delivery.CanonicalPayload.Length),
            delivery.Checkpoint.Length);
        delivery.Checkpoint.CopyTo(bytes.AsSpan(20 + stream.Length + delivery.CanonicalPayload.Length));
        if (delivery.ControlIndexIntent is { } intent)
        {
            byte[] name = Encoding.UTF8.GetBytes(intent.IndexName);
            int offset = bytes.Length;
            Array.Resize(ref bytes, checked(offset + 1 + sizeof(int) + name.Length));
            bytes[offset] = 1;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 1), name.Length);
            name.CopyTo(bytes.AsSpan(offset + 1 + sizeof(int)));
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool CaptureMatches(
        SharedProjectionEpochState state,
        string inventoryFingerprint,
        IReadOnlyDictionary<string, long> sourceHighWatermarks,
        byte[] captureBytes)
        => string.Equals(state.InventoryFingerprint, inventoryFingerprint, StringComparison.Ordinal)
            && (state.CaptureChunks is { } reference
                ? reference == SharedProjectionChunkStore.Describe(
                    "capture",
                    state.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + state.OperationId,
                    captureBytes)
                : state.CaptureHighWatermarks.Count == sourceHighWatermarks.Count
                    && sourceHighWatermarks.All(item => state.CaptureHighWatermarks.TryGetValue(item.Key, out long position)
                        && position == item.Value));

    private async Task<long?> GetCapturedPositionAsync(
        SharedProjectionScope scope,
        SharedProjectionEpochState state,
        string sourceStream,
        CancellationToken cancellationToken)
    {
        if (state.CaptureChunks is not { } reference)
        {
            return state.CaptureHighWatermarks.TryGetValue(sourceStream, out long position) ? position : null;
        }

        byte[] bytes = await new SharedProjectionChunkStore(_store)
            .ReadAsync(scope, reference, cancellationToken).ConfigureAwait(false);
        Dictionary<string, long> positions = JsonSerializer.Deserialize<Dictionary<string, long>>(bytes)
            ?? throw new InvalidOperationException("The shared rebuild capture chunks contain no high-watermarks.");
        return positions.TryGetValue(sourceStream, out long captured) ? captured : null;
    }

    private static string HashIdentity(string sourceStream, long position)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceStream + "\0" + position)));

    private static string[] RetainControlIndexName(
        SharedProjectionEpochState state,
        SharedProjectionControlIndexIntent? intent)
    {
        string[] names = state.RetainedControlIndexNames ?? [];
        if (intent is null || names.Contains(intent.IndexName, StringComparer.Ordinal))
        {
            return names;
        }

        if (names.Length >= MaxRetainedControlIndexes)
        {
            throw new InvalidOperationException("The tenant control-index intent limit was reached; reconcile or offboard before admitting more indexes.");
        }

        return [.. names.Append(intent.IndexName).Order(StringComparer.Ordinal)];
    }

    private static void ThrowIfOffboarded(SharedProjectionEpochState state)
    {
        if (state.OffboardingAuditId is not null)
        {
            throw new InvalidOperationException("The tenant's shared projection epoch has been offboarded.");
        }
    }

    private static void RequireOperation(
        SharedProjectionEpochState state,
        string operationId,
        SharedProjectionEpochPhase phase)
    {
        if (state.Phase != phase || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared rebuild operation or phase does not match the durable epoch.");
        }
    }

    private static InvalidOperationException RetryExhausted()
        => new("The shared projection epoch CAS retry limit was exhausted.");

    private async Task<SharedProjectionEpochState> RequireStateAsync(
        SharedProjectionScope scope,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
        SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The shared projection has no epoch state.");
        ValidateState(state);
        return state;
    }

    private Task<ReadModelEntry<SharedProjectionEpochState>> ReadStateAsync(
        SharedProjectionScope scope,
        CancellationToken cancellationToken)
        => _store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey, cancellationToken);

    private Task<bool> SaveAsync(
        SharedProjectionScope scope,
        SharedProjectionEpochState state,
        string? etag,
        CancellationToken cancellationToken)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(state).Length > MaxEpochStateBytes)
        {
            throw new InvalidOperationException("The shared projection epoch exceeds its bounded state-store limit.");
        }

        return _store.TrySaveAsync(scope.StoreName, scope.StateKey, state, etag ?? string.Empty, cancellationToken);
    }

    private static void ValidateState(SharedProjectionEpochState state)
    {
        if (state.Version != 1 || state.Epoch < 0 || state.ActiveGeneration < 0
            || state.ActiveGeneration > state.Epoch || !Enum.IsDefined(state.Phase)
            || state.CaptureHighWatermarks is null || state.ConsumerCheckpoints is null
            || state.RequiredWriters is null || state.RegisteredWriters is null
            || state.Journal is null || state.StageKeys is null
            || (state.RetainedControlIndexNames?.Length ?? 0) > MaxRetainedControlIndexes
            || (state.RetainedControlIndexNames?.Any(string.IsNullOrWhiteSpace) ?? false)
            || (state.RetainedControlIndexNames?.Distinct(StringComparer.Ordinal).Count() ?? 0)
                != (state.RetainedControlIndexNames?.Length ?? 0)
            || (state.OffboardingAuditId is not null && string.IsNullOrWhiteSpace(state.OffboardingAuditId))
            || state.ConsumerCheckpoints.Any(item => item.Value is null || item.Value.Position < 0 || item.Value.Token is null)
            || (state.Phase == SharedProjectionEpochPhase.Capturing && state.CaptureChunks is null)
            || (state.CaptureChunks is not null && state.CaptureHighWatermarks.Count != 0)
            || (state.StageChunks is not null && state.StageMutations is not null)
            || state.Journal.Any(item => !item.PayloadReady && item.PayloadChunks is null)
            || state.Journal.Any(item => item.PayloadChunks is not null
                && (item.CanonicalPayload.Length != 0 || item.Checkpoint.Length != 0))
            || state.Journal.Any(item => item.Failure is { } failure
                && (string.IsNullOrWhiteSpace(failure.SourceStream)
                    || !string.Equals(failure.SourceStream, item.SourceStream, StringComparison.Ordinal)
                    || failure.SourcePosition < 0
                    || failure.FailureCount < 1
                    || failure.FailureCount > MaxFailureAttempts))
            || state.Journal.Any(item => item.Parked && (item.Prepared is null || item.Failure is null)))
        {
            throw new InvalidOperationException("The shared projection epoch state is invalid.");
        }
    }

    private static void ValidateBoundary(SharedProjectionEpochState state, SharedProjectionScope scope)
    {
        if (state.RequiredWriters.Length > 0
            && !state.RequiredWriters.Order(StringComparer.Ordinal)
                .SequenceEqual(scope.RequiredWriters.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The shared projection writer set differs from its durable fence boundary.");
        }
    }

    private static void ValidateWriter(SharedProjectionScope scope, string writerId)
    {
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(writerId);
        if (!scope.RequiredWriters.Contains(writerId, StringComparer.Ordinal))
        {
            throw new ArgumentException("The writer is outside this shared projection family.", nameof(writerId));
        }
    }
}
