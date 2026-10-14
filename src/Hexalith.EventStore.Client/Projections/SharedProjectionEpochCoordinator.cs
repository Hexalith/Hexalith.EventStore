using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr;

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
    private const int ConflictEvidenceReserveBytes = 8 * 1024;
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
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? erasedOwnerIds = null,
        bool allowCoveredJournal = false,
        string? operationTenantId = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inventoryFingerprint);
        if (operationTenantId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(operationTenantId);
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

        HashSet<string> erased = new(erasedOwnerIds ?? [], StringComparer.Ordinal);
        if (erased.Count != (erasedOwnerIds?.Count ?? 0)
            || erased.Any(owner => !sourceHighWatermarks.ContainsKey(owner)))
        {
            throw new ArgumentException("Every erased owner must occur once in the sealed capture.", nameof(erasedOwnerIds));
        }

        var sortedCapture = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> item in sourceHighWatermarks.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            // A complemented position seals an erased tombstone without increasing the
            // bounded capture size. Legacy nonnegative positions remain active owners.
            sortedCapture.Add(item.Key, erased.Contains(item.Key) ? ~item.Value : item.Value);
        }

        byte[] captureBytes = JsonSerializer.SerializeToUtf8Bytes(sortedCapture);
        string requestedOperationTenantId = operationTenantId ?? scope.TenantId;
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        await ReconcileStreamMetadataCleanupAsync(scope, cancellationToken).ConfigureAwait(false);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? SharedProjectionEpochState.Empty;
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            if (scope.CopyOnWrite)
                await EnsureCopyOnWriteCommitMarkerAsync(scope, state.ActiveGeneration, cancellationToken)
                    .ConfigureAwait(false);
            if (state.CleanupChunks is not null || state.CleanupPreparedChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.Phase == SharedProjectionEpochPhase.Open
                && state.ActiveGeneration == state.Epoch
                && string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                && string.Equals(state.OperationTenantId, requestedOperationTenantId, StringComparison.Ordinal))
            {
                if (!CaptureMatches(state, inventoryFingerprint, sortedCapture, captureBytes))
                {
                    throw new InvalidOperationException("The shared rebuild capture conflicts with its durable epoch.");
                }

                return state.Epoch;
            }
            if (state.Phase == SharedProjectionEpochPhase.Capturing
                && string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                && string.Equals(state.OperationTenantId, requestedOperationTenantId, StringComparison.Ordinal)
                && CaptureMatches(state, inventoryFingerprint, sortedCapture, captureBytes))
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

            if (state.Phase != SharedProjectionEpochPhase.Open || state.OwnerSequenceHandoffPending)
            {
                if (string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                    && string.Equals(state.OperationTenantId, requestedOperationTenantId, StringComparison.Ordinal)
                    && CaptureMatches(state, inventoryFingerprint, sortedCapture, captureBytes)
                    && state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.CatchingUp)
                {
                    return state.Epoch;
                }

                throw new InvalidOperationException("Another shared rebuild is active for this tenant/family.");
            }

            if (scope.RequiredWriters.Any(writer => !state.RegisteredWriters.Contains(writer, StringComparer.Ordinal))
                || state.Journal.Length != 0 && !allowCoveredJournal
                || state.Journal.Any(item => item.Parked || item.Failure is not null || !item.PayloadReady
                    || item.OwnerSequenceNumber <= 0
                    || erased.Contains(item.SourceStream)
                    || !sortedCapture.TryGetValue(item.SourceStream, out long captured)
                    || item.EnvelopePosition > (captured < 0 ? ~captured : captured)))
            {
                throw new InvalidOperationException("Shared rebuild requires registered writers and a capture covering every pending delivery.");
            }

            foreach (KeyValuePair<string, long> owner in sortedCapture)
            {
                SharedProjectionCheckpoint? checkpoint = await ReadOwnerCheckpointAsync(
                    scope, state, owner.Key, cancellationToken).ConfigureAwait(false);
                if (checkpoint is { Position: > 0 }
                    && checkpoint.Position > (owner.Value < 0 ? ~owner.Value : owner.Value))
                {
                    throw new InvalidOperationException("The shared rebuild capture is behind an accepted owner checkpoint.");
                }
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
                OperationTenantId = requestedOperationTenantId,
                InventoryFingerprint = inventoryFingerprint,
                CaptureHighWatermarks = chunkedCapture ? new Dictionary<string, long>(StringComparer.Ordinal) : sortedCapture,
                CaptureChunks = captureChunks,
                CleanupChunks = state.CaptureChunks,
                StageFingerprint = null,
                StageKeys = [],
                StageMutations = null,
                StageManifestDigest = null,
                StageControlIndexIntent = null,
                OwnerSequenceHandoffPending = false,
                StageReservedPageCount = 0,
                CopyOnWriteAnchorGeneration = scope.CopyOnWrite
                    ? state.CopyOnWriteAnchorGeneration ?? state.ActiveGeneration
                    : null,
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
    /// Checks that every event in an owner history is within the sealed capture and that the
    /// history ends at its captured position.
    /// </summary>
    public async Task<bool> MatchesCapturedOwnerPositionAsync(
        SharedProjectionScope scope,
        string operationId,
        string sourceStream,
        IReadOnlyList<long> eventPositions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        if (eventPositions is null || eventPositions.Count == 0)
        {
            return false;
        }

        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (!string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
            || state.Phase != SharedProjectionEpochPhase.Building)
        {
            return false;
        }

        long? captured = await GetCapturedPositionAsync(scope, state, sourceStream, cancellationToken)
            .ConfigureAwait(false);
        long precedingPosition = 0;
        foreach (long position in eventPositions)
        {
            if (position <= precedingPosition || position > captured)
            {
                return false;
            }

            precedingPosition = position;
        }

        return captured == precedingPosition;
    }

    /// <summary>Checks whether the active sealed capture marked an owner as erased.</summary>
    /// <param name="scope">The tenant and projection family.</param>
    /// <param name="operationId">The active rebuild operation.</param>
    /// <param name="sourceStream">The captured owner stream.</param>
    /// <param name="isErased">The erasure state supplied for accumulation.</param>
    /// <param name="cancellationToken">Cancels the capture read.</param>
    /// <returns>Whether the owner exists and its sealed erasure state matches.</returns>
    public async Task<bool> MatchesCapturedOwnerErasureAsync(
        SharedProjectionScope scope,
        string operationId,
        string sourceStream,
        bool isErased,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            return false;
        }

        long? marker = await GetCapturedMarkerAsync(scope, state, sourceStream, cancellationToken)
            .ConfigureAwait(false);
        return marker is { } encoded && (encoded < 0) == isErased;
    }

    /// <summary>Returns every source owner sealed into the named rebuild capture.</summary>
    /// <param name="scope">The selected tenant and projection family.</param>
    /// <param name="operationId">The active rebuild operation.</param>
    /// <param name="cancellationToken">Cancels the capture read.</param>
    /// <returns>The captured owner identifiers in ordinal sort order.</returns>
    public async Task<IReadOnlyList<string>> GetCapturedOwnerIdsAsync(
        SharedProjectionScope scope,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested rebuild capture is not active.");
        }

        if (state.CaptureChunks is not { } reference)
        {
            return [.. state.CaptureHighWatermarks.Keys.Order(StringComparer.Ordinal)];
        }

        byte[] bytes = await new SharedProjectionChunkStore(_store)
            .ReadAsync(scope, reference, cancellationToken).ConfigureAwait(false);
        Dictionary<string, long> positions = JsonSerializer.Deserialize<Dictionary<string, long>>(bytes)
            ?? throw new InvalidOperationException("The shared rebuild capture chunks contain no high-watermarks.");
        return [.. positions.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>Seeds an owner sequence checkpoint after its captured rebuild generation is promoted.</summary>
    /// <param name="scope">The selected tenant and projection family.</param>
    /// <param name="operationId">The promoted rebuild operation.</param>
    /// <param name="sourceStream">The captured owner stream.</param>
    /// <param name="lastSequenceNumber">The final owner sequence in the captured history.</param>
    /// <param name="cancellationToken">Cancels the checkpoint write.</param>
    /// <param name="capturedEventDigests">Optional ordinal SHA-256 identities for captured event envelopes and payloads.</param>
    /// <returns>A task that completes after the checkpoint is durable.</returns>
    public async Task SeedCapturedOwnerSequenceAsync(
        SharedProjectionScope scope,
        string operationId,
        string sourceStream,
        long lastSequenceNumber,
        CancellationToken cancellationToken = default,
        byte[]? capturedEventDigests = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        if (lastSequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastSequenceNumber));
        }
        if (capturedEventDigests is not null
            && (lastSequenceNumber > 10_000 || capturedEventDigests.Length != checked(lastSequenceNumber * 32)))
        {
            throw new ArgumentException("Captured event identities must cover the complete owner history.", nameof(capturedEventDigests));
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            SharedProjectionEpochState state = (await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false)).Value
                ?? throw new InvalidOperationException("The shared projection has no selected rebuild.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.ActiveGeneration != state.Epoch
                || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The requested rebuild has not been promoted.");
            }

            long capturedPosition = await GetCapturedPositionAsync(scope, state, sourceStream, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The owner is absent from the rebuild capture.");
            string key = scope.CheckpointKey(sourceStream);
            ReadModelEntry<SharedProjectionCheckpoint> entry = await _store
                .GetAsync<SharedProjectionCheckpoint>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
            if (await ReadOwnerCheckpointAsync(scope, state, sourceStream, cancellationToken)
                .ConfigureAwait(false) is { } existing)
            {
                if (existing.Position > capturedPosition)
                {
                    throw new InvalidOperationException("The owner checkpoint is ahead of its rebuild capture without sequence continuity.");
                }

                if (existing.OwnerSequenceNumber > lastSequenceNumber)
                {
                    throw new InvalidOperationException("The owner checkpoint sequence exceeds its rebuild history.");
                }

                if (existing.OwnerSequenceNumber == lastSequenceNumber)
                {
                    if (existing.Position != capturedPosition)
                    {
                        throw new InvalidOperationException("The owner checkpoint does not match its rebuild capture.");
                    }

                    if (capturedEventDigests is null || existing.CapturedEventDigests is not null
                        && existing.CapturedEventDigests.AsSpan().SequenceEqual(capturedEventDigests))
                    {
                        return;
                    }
                    if (existing.CapturedEventDigests is not null)
                    {
                        throw new InvalidOperationException("The captured owner event identities conflict with the checkpoint.");
                    }
                }
            }

            var checkpoint = new SharedProjectionCheckpoint(
                capturedPosition,
                JsonSerializer.SerializeToUtf8Bytes(capturedPosition),
                lastSequenceNumber,
                capturedEventDigests?.ToArray());
            if (await _store.TrySaveAsync(scope.StoreName, key, checkpoint, entry.ETag ?? string.Empty,
                cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Lists owners with deliveries that must be proved by the sealed rebuild history.</summary>
    public async Task<IReadOnlySet<string>> GetPendingJournalOwnerIdsAsync(
        SharedProjectionScope scope,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        SharedProjectionEpochState state = (await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("The shared projection has no rebuild epoch.");
        ValidateState(state);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The pending journal belongs to another rebuild epoch.");
        }

        return new HashSet<string>(state.Journal.Select(item => item.SourceStream), StringComparer.Ordinal);
    }

    /// <summary>Proves that pending deliveries for one owner are present in its sealed source history.</summary>
    public async Task<bool> CanPromoteCapturedJournalOwnerAsync(
        SharedProjectionScope scope,
        string operationId,
        string sourceStream,
        long lastSequenceNumber,
        byte[]? capturedEventDigests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        SharedProjectionEpochState state = (await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("The shared projection has no rebuild epoch.");
        ValidateState(state);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (SharedProjectionJournalEntry pending in state.Journal.Where(item =>
            string.Equals(item.SourceStream, sourceStream, StringComparison.Ordinal)))
        {
            if (pending.Parked || pending.Failure is not null || !pending.PayloadReady
                || lastSequenceNumber is < 1 or > 10_000
                || pending.OwnerSequenceNumber <= 0 || pending.OwnerSequenceNumber > lastSequenceNumber
                || capturedEventDigests is null || capturedEventDigests.Length != lastSequenceNumber * 32)
            {
                return false;
            }

            SharedProjectionDelivery delivery = await RestoreDeliveryAsync(scope, pending, cancellationToken)
                .ConfigureAwait(false);
            if (!CapturedIdentityMatches(pending, delivery.CanonicalPayload, capturedEventDigests))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CapturedIdentityMatches(
        SharedProjectionJournalEntry pending,
        byte[] canonicalPayload,
        byte[] capturedEventDigests)
    {
        long offset = (pending.OwnerSequenceNumber - 1) * 32;
        return offset >= 0 && offset + 32 <= capturedEventDigests.Length
            && SharedProjectionCapturedEventIdentity.Compute(pending.SourceStream,
                pending.EnvelopePosition, pending.OwnerSequenceNumber, canonicalPayload).AsSpan()
                .SequenceEqual(capturedEventDigests.AsSpan((int)offset, 32));
    }

    /// <summary>Checks owner checkpoint continuity before a captured generation is promoted.</summary>
    public async Task<bool> CanPromoteCapturedOwnerAsync(
        SharedProjectionScope scope,
        string operationId,
        string sourceStream,
        long lastSequenceNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        if (lastSequenceNumber <= 0) return false;
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal)) return false;
        long? captured = await GetCapturedPositionAsync(scope, state, sourceStream, cancellationToken)
            .ConfigureAwait(false);
        if (captured is null) return false;
        SharedProjectionCheckpoint? checkpoint = await ReadOwnerCheckpointAsync(
            scope, state, sourceStream, cancellationToken).ConfigureAwait(false);
        return checkpoint is null || checkpoint.Position <= captured
            && (checkpoint.OwnerSequenceNumber < lastSequenceNumber
                || checkpoint.OwnerSequenceNumber == lastSequenceNumber && checkpoint.Position == captured);
    }

    /// <summary>Writes private rebuild scratch only while the named epoch is still building.</summary>
    public async Task<bool> TryWriteScratchAsync<TValue>(
        SharedProjectionScope scope,
        string operationId,
        string key,
        TValue value,
        string batchId,
        ReadModelBatchConcurrency concurrency,
        CancellationToken cancellationToken = default) where TValue : class
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentNullException.ThrowIfNull(concurrency);
        ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        SharedProjectionEpochState state = entry.Value
            ?? throw new InvalidOperationException("The shared projection has no rebuild epoch.");
        if (state.Phase != SharedProjectionEpochPhase.Building
            || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
        {
            return false;
        }

        var batch = new ReadModelBatch(
            new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain, scope.Family,
                scope.Family, batchId),
            [
                ReadModelBatchOperation.Write(key, value, concurrency),
                ReadModelBatchOperation.Write(scope.StateKey, state,
                    ReadModelBatchConcurrency.Match(entry.ETag
                        ?? throw new InvalidOperationException("The shared rebuild fence has no ETag.")))
            ]);
        ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) return true;
        if (result.Status == ReadModelBatchStatus.Conflict) return false;
        throw new SharedProjectionWriteUnavailableException(
            new InvalidOperationException("Private rebuild scratch did not reach a complete state."));
    }

    /// <summary>Reclaims a completed private scratch batch receipt after its keys are erased.</summary>
    public Task ReclaimScratchReceiptAsync(SharedProjectionScope scope, string batchId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        return EraseTerminalBatchMarkerAsync(scope, batchId, cancellationToken);
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
        if (delivery.EnvelopePosition < 0 || delivery.OwnerSequenceNumber < 0
            || delivery.CanonicalPayload is null || delivery.Checkpoint is null
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

            if (state.OwnerSequenceHandoffPending
                || state.Phase is SharedProjectionEpochPhase.Capturing or SharedProjectionEpochPhase.Aborting)
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
                    if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery, existing.Digest,
                        digest, cancellationToken).ConfigureAwait(false)) return SharedProjectionJournalResult.IdentityConflict;
                    continue;
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
                        if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery, existing.Digest,
                            digest, cancellationToken).ConfigureAwait(false)) return SharedProjectionJournalResult.IdentityConflict;
                        continue;
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
                    if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery, receipt.Value.Digest,
                        digest, cancellationToken).ConfigureAwait(false)) return SharedProjectionJournalResult.IdentityConflict;
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

            // An owner-sequenced write cannot advance its checkpoint between capture validation
            // and promotion. The caller retries after the captured owner sequence is seeded.
            if (state.Phase == SharedProjectionEpochPhase.Building && delivery.OwnerSequenceNumber > 0)
            {
                return SharedProjectionJournalResult.Backpressure;
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
                    if (delivery.OwnerSequenceNumber > 0)
                    {
                        SharedProjectionCheckpoint? capturedOwner = await ReadOwnerCheckpointAsync(
                            scope, state, delivery.SourceStream, cancellationToken).ConfigureAwait(false);
                        byte[]? identities = capturedOwner?.CapturedEventDigests;
                        if (identities is null)
                        {
                            // The older nonpartitioned rebuild contract did not seed event identities.
                            // A receipt from the previously selected epoch can still prove an event
                            // that ordinary delivery had already accepted before that rebuild.
                            ReadModelEntry<SharedProjectionDeliveryReceipt> previous = await _store
                                .GetAsync<SharedProjectionDeliveryReceipt>(scope.StoreName,
                                    scope.ReceiptKey(state.Epoch - 1, delivery.SourceStream,
                                        delivery.EnvelopePosition), cancellationToken).ConfigureAwait(false);
                            if (capturedOwner is null
                                || capturedOwner.OwnerSequenceNumber < delivery.OwnerSequenceNumber
                                || previous.Value is null || previous.Value.Parked)
                            {
                                return SharedProjectionJournalResult.RebuildRequired;
                            }
                            if (string.Equals(previous.Value.Digest, digest, StringComparison.Ordinal))
                                return SharedProjectionJournalResult.AlreadyCaptured;
                            if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery, previous.Value.Digest,
                                digest, cancellationToken).ConfigureAwait(false)) return SharedProjectionJournalResult.IdentityConflict;
                            continue;
                        }
                        if (delivery.OwnerSequenceNumber > identities.Length / 32)
                        {
                            return SharedProjectionJournalResult.RebuildRequired;
                        }
                        int offset = checked((int)(delivery.OwnerSequenceNumber - 1) * 32);
                        if (!SharedProjectionCapturedEventIdentity.Compute(
                                delivery.SourceStream, delivery.EnvelopePosition,
                                delivery.OwnerSequenceNumber, delivery.CanonicalPayload).AsSpan()
                            .SequenceEqual(identities.AsSpan(offset, 32)))
                        {
                            if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery,
                                Convert.ToHexString(identities.AsSpan(offset, 32)), digest, cancellationToken)
                                .ConfigureAwait(false)) return SharedProjectionJournalResult.IdentityConflict;
                            continue;
                        }
                    }
                    return SharedProjectionJournalResult.AlreadyCaptured;
                }
            }

            if (delivery.OwnerSequenceNumber > 0)
            {
                SharedProjectionCheckpoint? owner = await ReadOwnerCheckpointAsync(
                    scope, state, delivery.SourceStream, cancellationToken).ConfigureAwait(false);
                if (owner is { Position: > 0, OwnerSequenceNumber: 0 })
                {
                    // A pre-sequence checkpoint cannot prove the next aggregate ordinal.
                    // A captured full rebuild seeds the canonical sequence before live delivery resumes.
                    return SharedProjectionJournalResult.RebuildRequired;
                }

                long acceptedSequence = Math.Max(
                    owner?.OwnerSequenceNumber ?? 0,
                    state.Journal.Where(item => string.Equals(item.SourceStream, delivery.SourceStream, StringComparison.Ordinal))
                        .Select(static item => item.OwnerSequenceNumber).DefaultIfEmpty().Max());
                if (delivery.OwnerSequenceNumber != acceptedSequence + 1)
                {
                    string acceptedDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(acceptedSequence)));
                    bool futureGap = delivery.OwnerSequenceNumber > acceptedSequence + 1;
                    if (await RecordDeliveryConflictAsync(scope, state, entry.ETag, delivery, acceptedDigest,
                        digest, cancellationToken, futureGap ? delivery.OwnerSequenceNumber : null).ConfigureAwait(false))
                        return futureGap ? SharedProjectionJournalResult.DeliveryGap : SharedProjectionJournalResult.IdentityConflict;
                    continue;
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
                PayloadReady: !chunked,
                OwnerSequenceNumber: delivery.OwnerSequenceNumber);
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
            if (state.StageStreaming)
            {
                throw new InvalidOperationException("The shared rebuild already reserved a streamed stage.");
            }
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

    /// <summary>Stages a deterministic, bounded stream without materializing the complete manifest.</summary>
    public async Task<string> StageStreamingAsync(
        SharedProjectionScope scope,
        string operationId,
        IAsyncEnumerable<ReadModelBatchOperation> operations,
        CancellationToken cancellationToken = default,
        SharedProjectionControlIndexIntent? controlIndexIntent = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(operations);
        if (controlIndexIntent is not null)
        {
            _ = SharedProjectionControlIndexWriter.StateKey(controlIndexIntent.IndexName);
        }

        SharedProjectionEpochState reserved = await ReserveStreamingAsync(scope, operationId,
            controlIndexIntent, cancellationToken).ConfigureAwait(false);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var page = new List<SharedProjectionMutation>(32);
        int pageBytes = 0;
        int pageOrdinal = 0;
        await foreach (ReadModelBatchOperation operation in operations.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            SharedProjectionMutation mutation = SharedProjectionMutation.FromOperation(operation);
            int size = MutationBatchSize(mutation);
            if (page.Count == 32 || pageBytes + size > 256 * 1024)
            {
                await StageStreamPageAsync(scope, operationId, reserved.Epoch, pageOrdinal, [.. page], cancellationToken)
                    .ConfigureAwait(false);
                AppendPageDigest(hash, page);
                pageOrdinal++;
                page.Clear();
                pageBytes = 0;
            }

            page.Add(mutation);
            pageBytes += size;
        }

        if (page.Count > 0)
        {
            await StageStreamPageAsync(scope, operationId, reserved.Epoch, pageOrdinal, [.. page], cancellationToken)
                .ConfigureAwait(false);
            AppendPageDigest(hash, page);
            pageOrdinal++;
        }

        if (pageOrdinal == 0) throw new InvalidOperationException("A streamed stage requires at least one mutation.");
        string fingerprint = "stream:" + Convert.ToHexString(hash.GetHashAndReset()) + ":" + pageOrdinal;
        return await SealStreamingFingerprintAsync(scope, operationId, pageOrdinal, fingerprint,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the durable append cursor for one private streamed rebuild.</summary>
    public async Task<SharedProjectionStreamingStageStatus> GetStreamingStageStatusAsync(
        SharedProjectionScope scope, string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope,
            cancellationToken).ConfigureAwait(false);
        SharedProjectionEpochState state = entry.Value
            ?? throw new InvalidOperationException("Shared rebuild has not begun.");
        ValidateState(state);
        RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
        if (!state.StageStreaming)
            throw new InvalidOperationException("The streamed stage has not been reserved.");
        return new SharedProjectionStreamingStageStatus(state.Epoch, state.StageCompletedPageCount,
            state.StageReservedPageCount,
            state.StageFingerprint is not null, state.StageFingerprint);
    }

    /// <summary>Appends one bounded page to a private streamed rebuild without selecting it.</summary>
    public async Task AppendStreamingPageAsync(SharedProjectionScope scope, string operationId,
        int pageOrdinal, IReadOnlyList<ReadModelBatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(operations);
        if (pageOrdinal < 0 || operations.Count is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(pageOrdinal));
        SharedProjectionMutation[] mutations = [.. operations.Select(SharedProjectionMutation.FromOperation)];
        if (mutations.Sum(MutationBatchSize) > 256 * 1024)
            throw new ArgumentException("The streamed page exceeds the bounded byte limit.", nameof(operations));
        SharedProjectionEpochState reserved = await ReserveStreamingAsync(scope, operationId, null,
            cancellationToken).ConfigureAwait(false);
        if (reserved.StageFingerprint is not null)
            throw new InvalidOperationException("The streamed stage has already been sealed.");
        await StageStreamPageAsync(scope, operationId, reserved.Epoch, pageOrdinal, mutations,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Seals all durably appended pages by their immutable page digests.</summary>
    public async Task<string> SealStreamingAsync(SharedProjectionScope scope, string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope,
            cancellationToken).ConfigureAwait(false);
        SharedProjectionEpochState state = entry.Value
            ?? throw new InvalidOperationException("Shared rebuild has not begun.");
        ValidateState(state);
        RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
        int count = state.StageCompletedPageCount;
        if (!state.StageStreaming || count == 0 || count != state.StageReservedPageCount)
            throw new InvalidOperationException("The streamed stage has an incomplete page.");
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            ReadModelEntry<SharedProjectionStreamPage> page = await _store
                .GetAsync<SharedProjectionStreamPage>(scope.StoreName,
                    StreamPageKey(scope, state.Epoch, operationId, ordinal), cancellationToken)
                .ConfigureAwait(false);
            if (page.Value is not { } value || value.Ordinal != ordinal
                || value.Digest is not { Length: 64 } digest)
                throw new InvalidOperationException("A streamed stage page is missing or invalid.");
            hash.AppendData(Convert.FromHexString(digest));
        }
        string fingerprint = "stream:" + Convert.ToHexString(hash.GetHashAndReset()) + ":" + count;
        return await SealStreamingFingerprintAsync(scope, operationId, count, fingerprint,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SharedProjectionEpochState> ReserveStreamingAsync(SharedProjectionScope scope,
        string operationId, SharedProjectionControlIndexIntent? controlIndexIntent,
        CancellationToken cancellationToken)
    {
        SharedProjectionEpochState reserved;
        for (int attempt = 0; ; attempt++)
        {
            if (attempt >= MaxRetries) throw RetryExhausted();
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild has not begun.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (state.StageMutations is not null || state.StageChunks is not null
                || state.StageManifestDigest is not null || state.StageKeys.Length != 0
                || state.StageStreaming && !Equals(state.StageControlIndexIntent, controlIndexIntent))
            {
                throw new InvalidOperationException("The shared rebuild stage conflicts with its reserved manifest.");
            }

            string[] names = RetainControlIndexName(state, controlIndexIntent);
            await ApplyControlIndexAsync(scope, controlIndexIntent, cancellationToken).ConfigureAwait(false);
            if (state.StageStreaming)
            {
                reserved = state;
                break;
            }

            reserved = state with
            {
                StageStreaming = true,
                StageControlIndexIntent = controlIndexIntent,
                RetainedControlIndexNames = names,
            };
            if (await SaveAsync(scope, reserved, entry.ETag, cancellationToken).ConfigureAwait(false)) break;
        }
        return reserved;
    }

    private async Task<string> SealStreamingFingerprintAsync(SharedProjectionScope scope,
        string operationId, int pageOrdinal, string fingerprint, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild state disappeared.");
            ValidateState(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (!state.StageStreaming || state.StageCompletedPageCount != pageOrdinal
                || state.StageFingerprint is not null && !string.Equals(state.StageFingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The streamed stage conflicts with its durable pages.");
            }

            if (state.StageFingerprint is not null) return fingerprint;
            if (await SaveAsync(scope, state with { StageFingerprint = fingerprint, StageSealedPageCount = pageOrdinal },
                entry.ETag, cancellationToken).ConfigureAwait(false)) return fingerprint;
        }

        throw RetryExhausted();
    }

    private async Task StageStreamPageAsync(SharedProjectionScope scope, string operationId, long epoch,
        int ordinal, SharedProjectionMutation[] mutations, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> reservation = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionEpochState state = reservation.Value
                ?? throw new InvalidOperationException("Shared rebuild state disappeared.");
            ValidateState(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (!state.StageStreaming || state.Epoch != epoch || state.StageCompletedPageCount < ordinal)
            {
                throw new InvalidOperationException("The streamed stage page is outside its reserved ordinal.");
            }

            if (state.StageCompletedPageCount > ordinal)
            {
                break;
            }

            if (state.StageReservedPageCount == ordinal + 1)
            {
                break;
            }

            if (state.StageReservedPageCount != ordinal)
            {
                throw new InvalidOperationException("The streamed stage reservation has a gap.");
            }

            if (await SaveAsync(scope, state with { StageReservedPageCount = ordinal + 1 },
                reservation.ETag, cancellationToken).ConfigureAwait(false))
            {
                break;
            }

            if (attempt == MaxRetries - 1) throw RetryExhausted();
        }

        string digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(mutations)));
        SharedProjectionRetiredValue[] retirements = [.. mutations
            .Where(item => item.Key.StartsWith(SharedProjectionRetiredValue.MarkerPrefix,
                StringComparison.Ordinal))
            .Select(item => ParseRetirement(item, epoch, allowCurrent: true))];
        if (retirements.Length > 0 && !scope.CopyOnWrite)
            throw new InvalidOperationException("Retirement markers require copy-on-write staging.");
        var page = new SharedProjectionStreamPage(ordinal, digest, [.. mutations.Select(item => item.Key)])
        {
            RetiredValues = retirements
        };
        string pageKey = StreamPageKey(scope, epoch, operationId, ordinal);
        ReadModelEntry<SharedProjectionStreamPage> existingPage = await _store
            .GetAsync<SharedProjectionStreamPage>(scope.StoreName, pageKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingPage.Value is { } priorPage)
        {
            if (priorPage.Ordinal != ordinal || priorPage.Digest != digest
                || !priorPage.Keys.SequenceEqual(page.Keys, StringComparer.Ordinal)
                || priorPage.RetiredValues is null
                || !priorPage.RetiredValues.SequenceEqual(page.RetiredValues))
                throw new InvalidOperationException("A streamed stage page conflicts with its durable identity.");
        }

        if (existingPage.Value is null)
        {
            ReadModelEntry<SharedProjectionEpochState> fence = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionEpochState reserved = fence.Value
                ?? throw new InvalidOperationException("Shared rebuild state disappeared.");
            RequireOperation(reserved, operationId, SharedProjectionEpochPhase.Building);
            if (reserved.Epoch != epoch || !reserved.StageStreaming || reserved.StageReservedPageCount < ordinal + 1)
                throw new InvalidOperationException("The streamed stage page is no longer reserved.");
            var metadata = new List<ReadModelBatchOperation>(mutations.Length + 2)
            {
                ReadModelBatchOperation.Write(pageKey, page, ReadModelBatchConcurrency.CreateOnly)
            };
            for (int index = 0; index < mutations.Length; index++)
            {
                var marker = new SharedProjectionStreamKey(mutations[index].Key, ordinal, index);
                metadata.Add(ReadModelBatchOperation.Write(
                    StreamKeyMarker(scope, epoch, operationId, marker.Key), marker,
                    ReadModelBatchConcurrency.CreateOnly));
            }

            // The epoch CAS is the final operation. If abort wins after validation, the batch
            // compensates every earlier metadata write; if this CAS wins, abort observes them.
            metadata.Add(ReadModelBatchOperation.Write(scope.StateKey, reserved,
                ReadModelBatchConcurrency.Match(fence.ETag
                    ?? throw new InvalidOperationException("The shared rebuild fence has no ETag."))));
            var metadataBatch = new ReadModelBatch(
                new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family,
                    StreamMetadataBatchId(epoch, operationId, ordinal)), metadata);
            ReadModelBatchResult metadataResult = await _batches.ExecuteAsync(metadataBatch, cancellationToken)
                .ConfigureAwait(false);
            if (!metadataResult.IsSuccess)
            {
                throw metadataResult.Status == ReadModelBatchStatus.Conflict
                    ? new InvalidOperationException("The streamed stage metadata lost its epoch fence.")
                    : new SharedProjectionWriteUnavailableException(
                        new InvalidOperationException("Streamed stage metadata did not reach a complete state."));
            }
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("Shared rebuild state disappeared.");
            ValidateState(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (!state.StageStreaming || state.Epoch != epoch || state.StageCompletedPageCount < ordinal
                || state.StageFingerprint is not null && ordinal >= state.StageSealedPageCount)
                throw new InvalidOperationException("The streamed stage page is outside the reserved epoch.");
            if (state.StageCompletedPageCount > ordinal) return;
            ReadModelBatch batch = await BuildCopyOnWriteBatchAsync(scope, epoch,
                "stage:" + epoch + ":" + operationId + ":" + ordinal, mutations,
                state.ActiveGeneration, state.CopyOnWriteAnchorGeneration, cancellationToken)
                .ConfigureAwait(false);
            ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) throw new InvalidOperationException("The streamed stage page is incomplete: " + result.Status);
            if (await SaveAsync(scope, state with { StageCompletedPageCount = ordinal + 1 }, entry.ETag,
                cancellationToken).ConfigureAwait(false)) return;
        }

        throw RetryExhausted();
    }

    private static void AppendPageDigest(IncrementalHash hash, IReadOnlyList<SharedProjectionMutation> page)
        => hash.AppendData(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)));

    private static string StreamPageKey(SharedProjectionScope scope, long epoch, string operationId, int ordinal)
        => scope.StateKey + ":stage-page:" + epoch + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId))) + ":" + ordinal;

    private static string StreamKeyMarker(SharedProjectionScope scope, long epoch, string operationId, string key)
        => scope.StateKey + ":stage-key:" + epoch + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId + "\0" + key)));

    private static string StreamMetadataBatchId(long epoch, string operationId, int ordinal)
        => "stage-metadata:" + epoch + ":"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId))) + ":" + ordinal;

    private async Task CleanupStreamPagesAsync(SharedProjectionScope scope, string operationId, long epoch,
        CancellationToken cancellationToken)
    {
        for (int ordinal = 0; ; ordinal++)
        {
            ReadModelEntry<SharedProjectionStreamPage> entry = await _store.GetAsync<SharedProjectionStreamPage>(
                scope.StoreName, StreamPageKey(scope, epoch, operationId, ordinal), cancellationToken).ConfigureAwait(false);
            SharedProjectionStreamPage? page = entry.Value;
            if (page is null) return;
            if (page.Ordinal != ordinal || page.Keys is null || page.Keys.Length is 0 or > 32
                || page.Keys.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("A streamed stage cleanup page is invalid.");

            SharedProjectionMutation[] deletes = [.. page.Keys.Distinct(StringComparer.Ordinal).Select(key => new SharedProjectionMutation(
                key, ReadModelBatchOperationKind.Delete, string.Empty, []))];
            ReadModelBatch cleanup = BuildBatch(scope, epoch, "abort:" + epoch + ":" + operationId + ":" + ordinal,
                deletes);
            ReadModelBatchResult result = await _batches.ExecuteAsync(cleanup, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
                throw new InvalidOperationException("Streamed stage cleanup is incomplete: " + result.Status);
        }
    }

    private async Task CleanupStreamMetadataAsync(SharedProjectionScope scope, string operationId, long epoch,
        int pageCount, CancellationToken cancellationToken, bool promotedRetirements = false)
    {
        for (int ordinal = 0; ordinal < pageCount; ordinal++)
        {
            string pageKey = StreamPageKey(scope, epoch, operationId, ordinal);
            string cleanupBatchId = "stage-metadata-cleanup:" + epoch + ":"
                + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId)))
                + ":" + ordinal;
            ReadModelEntry<SharedProjectionStreamPage> page = await _store
                .GetAsync<SharedProjectionStreamPage>(scope.StoreName, pageKey, cancellationToken)
                .ConfigureAwait(false);
            if (page.Value is null)
            {
                await EraseTerminalBatchMarkerAsync(scope, StreamMetadataBatchId(epoch, operationId, ordinal),
                    cancellationToken).ConfigureAwait(false);
                await EraseTerminalBatchMarkerAsync(scope, cleanupBatchId, cancellationToken).ConfigureAwait(false);
                await EraseTerminalBatchMarkerAsync(scope, "stage:" + epoch + ":" + operationId + ":" + ordinal,
                    cancellationToken).ConfigureAwait(false);
                await EraseTerminalBatchMarkerAsync(scope, "abort:" + epoch + ":" + operationId + ":" + ordinal,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (page.Value.Ordinal != ordinal || page.Value.Keys is null
                || page.Value.Keys.Length is 0 or > 32
                || page.Value.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("A streamed stage cleanup page is invalid.");
            }

            if (promotedRetirements)
            {
                var retired = new List<ReadModelBatchOperation>();
                var retiredKeys = new HashSet<string>(StringComparer.Ordinal);
                SharedProjectionRetiredValue[] recorded = page.Value.RetiredValues
                    ?? throw new InvalidOperationException("A streamed stage retirement record is invalid.");
                int markerCount = page.Value.Keys.Count(static key => key.StartsWith(
                    SharedProjectionRetiredValue.MarkerPrefix, StringComparison.Ordinal));
                if (recorded.Length != markerCount)
                    throw new InvalidOperationException("A streamed stage retirement record is incomplete.");
                foreach (SharedProjectionRetiredValue value in recorded)
                {
                    if (value.Generation < 0 || value.Generation > epoch
                        || string.IsNullOrWhiteSpace(value.LogicalKey))
                        throw new InvalidOperationException("A streamed stage retirement record is invalid.");
                    string oldKey = scope.PhysicalKey(value.Generation, value.LogicalKey);
                    if (retiredKeys.Add(oldKey))
                        retired.Add(ReadModelBatchOperation.Delete(oldKey,
                            ReadModelBatchConcurrency.IdempotentAbsent));
                }
                if (retired.Count > 0)
                {
                    string retireBatchId = "retired:" + epoch + ":"
                        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId)))
                        + ":" + ordinal;
                    var retirementBatch = new ReadModelBatch(new ReadModelBatchScope(scope.StoreName,
                        scope.TenantId, scope.Domain, scope.Family, scope.Family, retireBatchId), retired);
                    ReadModelBatchResult retiredResult = await _batches.ExecuteAsync(retirementBatch,
                        cancellationToken).ConfigureAwait(false);
                    if (!retiredResult.IsSuccess)
                        throw new SharedProjectionWriteUnavailableException(
                            new InvalidOperationException(
                                $"Promoted value retirement is incomplete: {retiredResult.Status}, "
                                + $"{retiredResult.ConflictKind}, {retiredResult.RecoveryReason}."));
                    await EraseTerminalBatchMarkerAsync(scope, retireBatchId, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            var deletes = new List<ReadModelBatchOperation>(page.Value.Keys.Length + 1);
            foreach (string key in page.Value.Keys.Distinct(StringComparer.Ordinal))
            {
                if (promotedRetirements && key.StartsWith(SharedProjectionRetiredValue.MarkerPrefix,
                    StringComparison.Ordinal))
                    deletes.Add(ReadModelBatchOperation.Delete(scope.PhysicalKey(epoch, key),
                        ReadModelBatchConcurrency.IdempotentAbsent));
                string markerKey = StreamKeyMarker(scope, epoch, operationId, key);
                ReadModelEntry<SharedProjectionStreamKey> marker = await _store
                    .GetAsync<SharedProjectionStreamKey>(scope.StoreName, markerKey, cancellationToken)
                    .ConfigureAwait(false);
                if (marker.Value is not null)
                {
                    if (marker.Value.Key != key)
                    {
                        throw new InvalidOperationException("A streamed stage key marker is invalid.");
                    }

                    if (marker.Value.PageOrdinal == ordinal)
                    {
                        deletes.Add(ReadModelBatchOperation.Delete(markerKey,
                            ReadModelBatchConcurrency.IdempotentAbsent));
                    }
                }
            }

            deletes.Add(ReadModelBatchOperation.Delete(pageKey,
                ReadModelBatchConcurrency.IdempotentAbsent));
            var batch = new ReadModelBatch(
                new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family,
                    cleanupBatchId), deletes);
            ReadModelBatchResult result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new SharedProjectionWriteUnavailableException(
                    new InvalidOperationException("Streamed stage metadata cleanup is incomplete."));
            }

            await EraseTerminalBatchMarkerAsync(scope, StreamMetadataBatchId(epoch, operationId, ordinal),
                cancellationToken).ConfigureAwait(false);
            await EraseTerminalBatchMarkerAsync(scope, cleanupBatchId, cancellationToken).ConfigureAwait(false);
            await EraseTerminalBatchMarkerAsync(scope, "stage:" + epoch + ":" + operationId + ":" + ordinal,
                cancellationToken).ConfigureAwait(false);
            await EraseTerminalBatchMarkerAsync(scope, "abort:" + epoch + ":" + operationId + ":" + ordinal,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EraseTerminalBatchMarkerAsync(SharedProjectionScope scope, string batchId,
        CancellationToken cancellationToken)
    {
        IReadModelStore markerStore = _batches as IReadModelStore ?? _store;
        IReadModelConditionalEraser? eraser = _batches as IReadModelConditionalEraser
            ?? _store as IReadModelConditionalEraser;
        if (eraser is null)
            throw new InvalidOperationException("Private scratch cleanup requires conditional read-model erasure.");
        string markerKey = new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain,
            scope.Family, scope.Family, batchId).ComputeMarkerKey();
        ReadModelEntry<ReadModelBatchMarker> marker = await markerStore.GetAsync<ReadModelBatchMarker>(
            scope.StoreName, markerKey, cancellationToken).ConfigureAwait(false);
        if (marker.Value is null) return;
        if (marker.Value.Status is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
            || string.IsNullOrEmpty(marker.ETag)
            || !await eraser.TryEraseAsync(scope.StoreName, markerKey, marker.ETag, cancellationToken)
                .ConfigureAwait(false))
            throw new SharedProjectionWriteUnavailableException(
                new InvalidOperationException("Private scratch marker cleanup is incomplete."));
    }

    private async Task ReconcileStreamMetadataCleanupAsync(SharedProjectionScope scope,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionEpochState? state = entry.Value;
            if (state is null || state.CleanupStreamPageCount == 0)
            {
                return;
            }

            string operationId = state.CleanupStreamOperationId
                ?? throw new InvalidOperationException("Streamed stage cleanup has no operation identity.");
            try
            {
                await CleanupStreamMetadataAsync(scope, operationId, state.CleanupStreamEpoch,
                    state.CleanupStreamPageCount, cancellationToken, state.CleanupPromotedRetirements)
                    .ConfigureAwait(false);
            }
            catch (SharedProjectionWriteUnavailableException) when (attempt + 1 < MaxRetries)
            {
                // Another replica may already own the same idempotent cleanup batch.
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                continue;
            }
            SharedProjectionEpochState cleaned = state with
            {
                CleanupStreamOperationId = null,
                CleanupStreamEpoch = 0,
                CleanupStreamPageCount = 0,
                CleanupPromotedRetirements = false,
            };
            if (await SaveAsync(scope, cleaned, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw RetryExhausted();
    }

    private static int MutationBatchSize(SharedProjectionMutation mutation)
    {
        if (mutation.CanonicalValue is null || string.IsNullOrWhiteSpace(mutation.Key))
            throw new InvalidOperationException("The shared projection manifest contains an invalid mutation.");
        int size = checked(mutation.CanonicalValue.Length + Encoding.UTF8.GetByteCount(mutation.Key)
            + Encoding.UTF8.GetByteCount(mutation.ValueTypeName) + 1024);
        if (size > 256 * 1024)
            throw new InvalidOperationException("One shared projection mutation exceeds the bounded batch limit.");
        return size;
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
        CancellationToken cancellationToken = default,
        bool requireOwnerSequenceHandoff = false)
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
                if (scope.CopyOnWrite)
                    await EnsureCopyOnWriteCommitMarkerAsync(scope, state.ActiveGeneration, cancellationToken)
                        .ConfigureAwait(false);
                SchedulePromotedCleanup(scope);
                return;
            }

            RequireOperation(state, operationId, SharedProjectionEpochPhase.Building);
            if (state.StageFingerprint is null)
            {
                throw new InvalidOperationException("The shared rebuild manifest has not been durably staged.");
            }

            if (state.StageStreaming && (state.StageSealedPageCount == 0
                || state.StageCompletedPageCount != state.StageSealedPageCount))
            {
                throw new InvalidOperationException("The streamed rebuild manifest is incomplete.");
            }

            await ApplyControlIndexAsync(scope, state.StageControlIndexIntent, cancellationToken).ConfigureAwait(false);

            SharedProjectionEpochState updated = state with
            {
                ActiveGeneration = state.Epoch,
                Phase = state.Journal.Length == 0 && !requireOwnerSequenceHandoff
                    ? SharedProjectionEpochPhase.Open
                    : SharedProjectionEpochPhase.CatchingUp,
                OwnerSequenceHandoffPending = requireOwnerSequenceHandoff,
                StageKeys = [],
                StageMutations = null,
                StageManifestDigest = null,
                StageChunks = null,
                StageExecutionStarted = false,
                StageStreaming = false,
                StageCompletedPageCount = 0,
                StageSealedPageCount = 0,
                StageReservedPageCount = 0,
                CleanupChunks = state.StageChunks,
                CleanupStreamOperationId = state.StageStreaming ? operationId : null,
                CleanupStreamEpoch = state.StageStreaming ? state.Epoch : 0,
                CleanupStreamPageCount = state.StageStreaming ? state.StageSealedPageCount : 0,
                CleanupPromotedRetirements = state.StageStreaming && scope.CopyOnWrite,
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
                if (scope.CopyOnWrite)
                    await EnsureCopyOnWriteCommitMarkerAsync(scope, updated.ActiveGeneration, cancellationToken)
                        .ConfigureAwait(false);
                SchedulePromotedCleanup(scope);
                return;
            }
        }

        throw RetryExhausted();
    }

    private void SchedulePromotedCleanup(SharedProjectionScope scope)
    {
        // The selected generation and any required owner-sequence handoff are durable.
        // Scratch cleanup is recoverable from the epoch state on the next Begin.
        _ = Task.Run(async () =>
        {
            try
            {
                await ReconcileChunkCleanupAsync(scope, CancellationToken.None).ConfigureAwait(false);
                await ReconcileStreamMetadataCleanupAsync(scope, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The cleanup intent remains durable for a later reconciliation.
            }
        });
    }

    /// <summary>Releases the promoted epoch after every verified owner checkpoint is seeded.</summary>
    public async Task CompleteOwnerSequenceHandoffAsync(
        SharedProjectionScope scope,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value
                ?? throw new InvalidOperationException("The shared projection has no selected rebuild.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.ActiveGeneration != state.Epoch
                || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The requested rebuild has not been promoted.");
            }

            if (!state.OwnerSequenceHandoffPending)
            {
                return;
            }

            SharedProjectionEpochState updated = state with
            {
                OwnerSequenceHandoffPending = false,
                Phase = state.Journal.Length == 0 ? SharedProjectionEpochPhase.Open : SharedProjectionEpochPhase.CatchingUp,
            };
            if (await SaveAsync(scope, updated, entry.ETag, cancellationToken).ConfigureAwait(false))
            {
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
        int completed = 0;
        for (; ; )
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The shared projection has no epoch state.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (state.OwnerSequenceHandoffPending
                || state.Phase is SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.Capturing
                || state.Journal.Length == 0)
            {
                if (state.Phase == SharedProjectionEpochPhase.CatchingUp && state.Journal.Length == 0
                    && !state.OwnerSequenceHandoffPending)
                {
                    SharedProjectionEpochState opened = state with { Phase = SharedProjectionEpochPhase.Open };
                    if (!await SaveAsync(scope, opened, entry.ETag, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }
                }

                return completed;
            }

            // A promoted epoch with no journal can return before private stage cleanup.
            // An actual journal entry must first clear any prior cleanup pointer so its
            // own payload can safely take that durable slot after processing.
            if (state.CleanupChunks is not null || state.CleanupPreparedChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            SharedProjectionJournalEntry head = state.Journal[0];
            if (!head.PayloadReady)
            {
                throw new InvalidOperationException("The shared projection delivery chunks are reserved but not acknowledged; source redelivery must complete them.");
            }

            if (state.ActiveGeneration == state.Epoch && head.OwnerSequenceNumber > 0
                && await GetCapturedPositionAsync(scope, state, head.SourceStream, cancellationToken)
                    .ConfigureAwait(false) is { } capturedPosition
                && head.EnvelopePosition <= capturedPosition)
            {
                SharedProjectionCheckpoint? capturedOwner = await ReadOwnerCheckpointAsync(
                    scope, state, head.SourceStream, cancellationToken).ConfigureAwait(false);
                SharedProjectionDelivery covered = await RestoreDeliveryAsync(scope, head, cancellationToken)
                    .ConfigureAwait(false);
                if (capturedOwner?.CapturedEventDigests is not { } identities
                    || !CapturedIdentityMatches(head, covered.CanonicalPayload, identities))
                {
                    throw new InvalidOperationException("The journaled delivery conflicts with captured owner history.");
                }

                await ApplyControlIndexAsync(scope, head.PreparedControlIndexIntent, cancellationToken)
                    .ConfigureAwait(false);
                string coveredReceiptKey = scope.ReceiptKey(state.Epoch, head.SourceStream, head.EnvelopePosition);
                if (!await _store.TrySaveAsync(scope.StoreName, coveredReceiptKey,
                    new SharedProjectionDeliveryReceipt(head.Digest, false, null), string.Empty,
                    cancellationToken).ConfigureAwait(false))
                {
                    ReadModelEntry<SharedProjectionDeliveryReceipt> priorReceipt = await _store
                        .GetAsync<SharedProjectionDeliveryReceipt>(scope.StoreName, coveredReceiptKey,
                            cancellationToken).ConfigureAwait(false);
                    if (priorReceipt.Value is not { Parked: false, Failure: null } prior
                        || !string.Equals(prior.Digest, head.Digest, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("The captured delivery receipt conflicts with its journal identity.");
                    }
                }

                SharedProjectionEpochState coveredState = state with
                {
                    Journal = [.. state.Journal.Skip(1)],
                    CleanupChunks = head.PayloadChunks,
                    CleanupPreparedChunks = head.PreparedChunks,
                };
                if (coveredState.Journal.Length == 0 && coveredState.Phase == SharedProjectionEpochPhase.CatchingUp)
                {
                    coveredState = coveredState with { Phase = SharedProjectionEpochPhase.Open };
                }

                if (await SaveAsync(scope, coveredState, entry.ETag, cancellationToken).ConfigureAwait(false))
                {
                    completed++;
                    await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            await ApplyControlIndexAsync(scope, head.PreparedControlIndexIntent, cancellationToken).ConfigureAwait(false);
            if (head.Prepared is null && head.PreparedChunks is null)
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
                catch (SharedProjectionCapacityExceededException)
                {
                    var capacityFailure = new SharedProjectionFailureStatus(head.SourceStream, head.EnvelopePosition,
                        Math.Min(MaxFailureAttempts, (head.Failure is { ReasonCode: "capacity" } priorCapacity ? priorCapacity.FailureCount : 0) + 1)) { ReasonCode = "capacity" };
                    if (!await SaveAsync(scope, state with
                    {
                        Journal = [head with { Failure = capacityFailure }, .. state.Journal.Skip(1)],
                    }, entry.ETag, cancellationToken).ConfigureAwait(false)) continue;
                    throw new SharedProjectionRetryableFoldException(capacityFailure);
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

                    int previousCount = head.Failure is { ReasonCode: "fold-failure" } previous
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
                    || operations.Sum(item => (long)item.CanonicalValue.Length) > SharedProjectionChunkStore.MaxPayloadBytes
                    || operations.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != operations.Count)
                {
                    throw new InvalidOperationException("Catch-up fold returned invalid or duplicate logical mutations.");
                }

                SharedProjectionMutation[] mutations = [.. operations.Select(SharedProjectionMutation.FromOperation)];
                byte[]? preparedBytes = mutations.Sum(item => (long)item.CanonicalValue.Length) > MaxJournalBytes
                    ? JsonSerializer.SerializeToUtf8Bytes(mutations)
                    : null;
                SharedProjectionChunkReference? preparedChunks = preparedBytes is null
                    ? null
                    : SharedProjectionChunkStore.Describe(
                        "prepared", state.Epoch + ":" + HashIdentity(head.SourceStream, head.EnvelopePosition), preparedBytes);
                SharedProjectionJournalEntry prepared = head with
                {
                    Prepared = preparedChunks is null ? mutations : null,
                    PreparedChunks = preparedChunks,
                    PreparedReady = preparedChunks is null,
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

                if (preparedChunks is not null)
                {
                    await WritePreparedChunksAsync(scope, preparedChunks, preparedBytes!, cancellationToken)
                        .ConfigureAwait(false);
                }

                continue;
            }

            SharedProjectionMutation[] preparedMutations;
            if (head.PreparedChunks is { } preparedReference)
            {
                var chunkStore = new SharedProjectionChunkStore(_store);
                if (!head.PreparedReady)
                {
                    // A crash may leave only a reserved reference or a prefix of immutable chunks.
                    // Re-fold the same journaled delivery and compare its digest before completing it.
                    SharedProjectionDelivery replay = await RestoreDeliveryAsync(scope, head, cancellationToken)
                        .ConfigureAwait(false);
                    IReadOnlyList<ReadModelBatchOperation> replayOperations;
                    try
                    {
                        replayOperations = await fold(replay, state.ActiveGeneration, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (SharedProjectionFoldFailureException failure) when (
                        head.Parked && head.Failure is { } parkedFailure
                        && string.Equals(failure.SourceStream, parkedFailure.SourceStream, StringComparison.Ordinal)
                        && failure.SourcePosition == parkedFailure.SourcePosition)
                    {
                        replayOperations = failure.CreateParkingMutations(parkedFailure.FailureCount);
                    }
                    byte[] replayBytes = JsonSerializer.SerializeToUtf8Bytes(
                        replayOperations.Select(SharedProjectionMutation.FromOperation).ToArray());
                    if (SharedProjectionChunkStore.Describe(preparedReference.Purpose, preparedReference.Identity, replayBytes)
                        != preparedReference)
                    {
                        throw new InvalidOperationException("The prepared shared projection delivery changed on replay.");
                    }

                    await WritePreparedChunksAsync(scope, preparedReference, replayBytes, cancellationToken)
                        .ConfigureAwait(false);
                    SharedProjectionEpochState ready = state with
                    {
                        Journal = [head with { PreparedReady = true }, .. state.Journal.Skip(1)],
                    };
                    if (!await SaveAsync(scope, ready, entry.ETag, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    continue;
                }

                byte[] bytes;
                try
                {
                    bytes = await chunkStore.ReadAsync(scope, preparedReference, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
                {
                    throw new SharedProjectionReadUnavailableException(exception);
                }
                preparedMutations = JsonSerializer.Deserialize<SharedProjectionMutation[]>(bytes)
                    ?? throw new InvalidOperationException("The prepared shared projection chunks are empty.");
            }
            else
            {
                preparedMutations = head.Prepared ?? throw new InvalidOperationException("The prepared journal is empty.");
            }

            if (preparedMutations.Length > 0)
            {
                string batchId = "catch-up:" + state.Epoch + ":" + HashIdentity(head.SourceStream, head.EnvelopePosition);
                IReadOnlyList<SharedProjectionMutation[]> batches = PartitionMutations(preparedMutations);
                for (int ordinal = 0; ordinal < batches.Count; ordinal++)
                {
                    string partId = batches.Count == 1 ? batchId : batchId + ":" + ordinal;
                    ReadModelBatch batch = await BuildCopyOnWriteBatchAsync(scope,
                        state.ActiveGeneration, partId, batches[ordinal], state.ActiveGeneration,
                        state.CopyOnWriteAnchorGeneration, cancellationToken, retireImmediately: true)
                        .ConfigureAwait(false);
                    ReadModelBatchResult result;
                    try
                    {
                        result = await _batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
                    {
                        throw new SharedProjectionWriteUnavailableException(exception);
                    }

                    if (result.Status is ReadModelBatchStatus.Incomplete or ReadModelBatchStatus.Indeterminate)
                    {
                        throw new SharedProjectionWriteUnavailableException(
                            new InvalidOperationException("The prepared shared projection batch needs a retry."));
                    }

                    if (!result.IsSuccess)
                    {
                        throw new InvalidOperationException("Shared projection catch-up is incomplete: " + result.Status);
                    }
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
                CleanupPreparedChunks = head.PreparedChunks,
                DeliveryConflicts = [.. state.DeliveryConflicts.Select(conflict => !head.Parked
                    && !conflict.IsResolved && conflict.RequiredOwnerSequenceNumber == head.OwnerSequenceNumber
                    && conflict.SourceStream == head.SourceStream && conflict.EnvelopePosition == head.EnvelopePosition
                    && conflict.ConflictingDigest == head.Digest
                        ? conflict with { ResolvedByOwnerSequenceNumber = head.OwnerSequenceNumber } : conflict)],
            };
            if (head.Parked)
            {
                var evidence = new SharedProjectionParkingEvidence(head.SourceStream, head.EnvelopePosition,
                    state.Epoch, head.Failure!.FailureCount);
                if (!state.ParkingEvidence.Any(item => item.SourceStream == head.SourceStream
                    && item.EnvelopePosition == head.EnvelopePosition))
                {
                    var recorded = dequeued with { ParkingEvidence = [.. state.ParkingEvidence, evidence] };
                    if (recorded.ParkingEvidence.Length > MaxJournalEntries || IsConflictEvidenceCapacityLimited(recorded))
                    {
                        if (state.ParkingEvidenceOverflow is { } overflow
                            && (overflow.SourceStream != head.SourceStream || overflow.EnvelopePosition != head.EnvelopePosition))
                            throw new InvalidOperationException("The parking evidence capacity is exhausted; restore or offboard the scope.");
                        dequeued = dequeued with { ParkingEvidenceOverflow = state.ParkingEvidenceOverflow ?? evidence };
                    }
                    else dequeued = recorded;
                }
            }
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
        CancellationToken cancellationToken = default,
        bool deferOpen = false)
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
            if (state.CleanupChunks is not null || state.CleanupPreparedChunks is not null)
            {
                await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.Journal.Length != 0)
            {
                _ = await CatchUpAsync(scope, fold, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.StageStreaming)
            {
                await CleanupStreamPagesAsync(scope, operationId, state.Epoch, cancellationToken).ConfigureAwait(false);
                await CleanupStreamMetadataAsync(scope, operationId, state.Epoch,
                    state.StageReservedPageCount, cancellationToken).ConfigureAwait(false);
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
                Phase = deferOpen ? SharedProjectionEpochPhase.Aborting : SharedProjectionEpochPhase.Open,
                StageKeys = [],
                StageFingerprint = null,
                StageMutations = null,
                StageManifestDigest = null,
                StageChunks = null,
                StageExecutionStarted = false,
                StageStreaming = false,
                StageCompletedPageCount = 0,
                StageSealedPageCount = 0,
                StageReservedPageCount = 0,
                CleanupStreamOperationId = state.StageStreaming && state.StageReservedPageCount > 0 ? operationId : null,
                CleanupStreamEpoch = state.StageStreaming && state.StageReservedPageCount > 0 ? state.Epoch : 0,
                CleanupStreamPageCount = state.StageStreaming ? state.StageReservedPageCount : 0,
                CleanupPromotedRetirements = false,
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

    /// <summary>Opens a drained aborted epoch after its owning rebuild session has durably closed.</summary>
    public async Task CompleteAbortAsync(
        SharedProjectionScope scope,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        await ReconcileChunkCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        await ReconcileStreamMetadataCleanupAsync(scope, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value
                ?? throw new InvalidOperationException("Shared rebuild state disappeared during abort closeout.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            if (!string.Equals(state.OperationId, operationId, StringComparison.Ordinal)
                || state.ActiveGeneration == state.Epoch)
            {
                throw new InvalidOperationException("The requested aborted rebuild does not own the epoch.");
            }

            if (state.Phase == SharedProjectionEpochPhase.Open) return;
            if (state.Phase != SharedProjectionEpochPhase.Aborting || state.Journal.Length != 0
                || state.StageStreaming || state.StageMutations is not null || state.StageChunks is not null
                || state.CleanupChunks is not null || state.CleanupPreparedChunks is not null
                || state.CleanupStreamPageCount != 0)
            {
                throw new InvalidOperationException("Aborted rebuild cleanup is incomplete.");
            }

            if (await SaveAsync(scope, state with { Phase = SharedProjectionEpochPhase.Open }, entry.ETag,
                cancellationToken).ConfigureAwait(false)) return;
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
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> before;
            ReadModelEntry<TValue> selected;
            ReadModelEntry<SharedProjectionEpochState> after;
            try
            {
                before = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
                SharedProjectionEpochState state = before.Value
                    ?? throw new InvalidOperationException("The shared projection has no epoch state.");
                ValidateState(state);
                ValidateBoundary(state, scope);
                if (state.OffboardingAuditId is not null)
                {
                    return new SharedProjectionReadResult<TValue>(null, state.ActiveGeneration, false, false);
                }

                if (state.Phase is SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting)
                {
                    return new SharedProjectionReadResult<TValue>(null, state.ActiveGeneration, true, false);
                }

                selected = await ReadGenerationAsync<TValue>(scope, state.ActiveGeneration, logicalKey,
                    cancellationToken).ConfigureAwait(false);
                after = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
                if (string.Equals(before.ETag, after.ETag, StringComparison.Ordinal))
                {
                    return new SharedProjectionReadResult<TValue>(selected.Value, state.ActiveGeneration,
                        state.Journal.Length > 0 || state.Phase is SharedProjectionEpochPhase.Capturing or SharedProjectionEpochPhase.Building, true)
                    {
                        SelectedETag = selected.ETag,
                        IsDegraded = HasConflictReadinessIssue(state),
                    };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is DaprException or HttpRequestException or IOException or TimeoutException or TaskCanceledException)
            {
                throw new SharedProjectionReadUnavailableException(exception);
            }
        }

        throw RetryExhausted();
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
        SharedProjectionCheckpoint? checkpoint = await ReadOwnerCheckpointAsync(
            scope, state, sourceStream, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            return null;
        }

        return new SharedProjectionCheckpoint(checkpoint.Position, checkpoint.Token.ToArray(), checkpoint.OwnerSequenceNumber,
            checkpoint.CapturedEventDigests?.ToArray());
    }

    private async Task<SharedProjectionCheckpoint?> ReadOwnerCheckpointAsync(
        SharedProjectionScope scope,
        SharedProjectionEpochState state,
        string sourceStream,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<SharedProjectionCheckpoint> indexed = await _store
            .GetAsync<SharedProjectionCheckpoint>(scope.StoreName, scope.CheckpointKey(sourceStream), cancellationToken)
            .ConfigureAwait(false);
        state.ConsumerCheckpoints.TryGetValue(sourceStream, out SharedProjectionCheckpoint? legacy);
        return indexed.Value is { } current
            && (legacy is null || current.Position > legacy.Position
                || current.Position == legacy.Position && current.OwnerSequenceNumber >= legacy.OwnerSequenceNumber)
            ? current
            : legacy;
    }

    /// <summary>Reads durable stage, promotion, and pending-delivery evidence.</summary>
    public async Task<SharedProjectionEpochStatus> GetStatusAsync(
        SharedProjectionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        _ = await ReconcileControlIndexAsync(scope, cancellationToken).ConfigureAwait(false);
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
            state.OffboardingAuditId is not null)
        {
            OperationTenantId = state.OperationTenantId,
            DeliveryConflicts = state.DeliveryConflicts.ToArray(),
            DeliveryConflictOverflow = state.DeliveryConflictOverflow,
            ParkingEvidence = state.ParkingEvidence.ToArray(),
            ParkingEvidenceOverflow = state.ParkingEvidenceOverflow,
            PendingFoldFailures = [.. state.Journal.Where(item => item.Failure is not null).Select(item => item.Failure!)],
            IsCapacityLimited = IsConflictEvidenceCapacityLimited(state),
        };
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

            if (state.CleanupChunks is not null || state.CleanupPreparedChunks is not null)
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
                || state.Phase is SharedProjectionEpochPhase.Capturing or SharedProjectionEpochPhase.Building or SharedProjectionEpochPhase.CatchingUp or SharedProjectionEpochPhase.Aborting;
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
                return new SharedProjectionReadResult<IReadOnlyDictionary<string, TValue?>>(values, state.ActiveGeneration, stale, true)
                {
                    IsDegraded = HasConflictReadinessIssue(state),
                };
            }
        }

        throw RetryExhausted();
    }

    /// <summary>Reads a specific physical generation for deterministic catch-up folding.</summary>
    public async Task<ReadModelEntry<TValue>> ReadGenerationAsync<TValue>(
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

        if (!scope.CopyOnWrite)
            return await _store.GetAsync<TValue>(scope.StoreName, scope.PhysicalKey(generation, logicalKey),
                cancellationToken).ConfigureAwait(false);

        ReadModelEntry<SharedProjectionCopyOnWritePointer> pointer = await _store
            .GetAsync<SharedProjectionCopyOnWritePointer>(scope.StoreName,
                scope.CopyOnWritePointerKey(logicalKey), cancellationToken).ConfigureAwait(false);
        if (pointer.Value is { } marker && marker.LatestGeneration == generation && marker.LatestDeleted)
            return new ReadModelEntry<TValue>(null, null);

        ReadModelEntry<TValue> direct = await _store.GetAsync<TValue>(scope.StoreName,
            scope.PhysicalKey(generation, logicalKey), cancellationToken).ConfigureAwait(false);
        if (direct.Value is not null) return direct;

        long? inherited = null;
        bool deleted = false;
        if (pointer.Value is { } selected)
        {
            if (selected.LatestGeneration <= generation
                && (selected.LatestGeneration == generation
                    || await IsCopyOnWriteGenerationCommittedAsync(scope, selected.LatestGeneration,
                        cancellationToken).ConfigureAwait(false)))
            {
                inherited = selected.LatestGeneration;
                deleted = selected.LatestDeleted;
            }
            else if (selected.PreviousGeneration <= generation
                && (selected.PreviousGeneration == generation
                    || await IsCopyOnWriteGenerationCommittedAsync(scope, selected.PreviousGeneration,
                        cancellationToken).ConfigureAwait(false)))
            {
                inherited = selected.PreviousGeneration;
                deleted = selected.PreviousDeleted;
            }
        }
        if (deleted) return new ReadModelEntry<TValue>(null, null);
        if (inherited is { } source)
        {
            if (source == generation)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected copy-on-write value is missing."));
            ReadModelEntry<TValue> inheritedValue = await _store.GetAsync<TValue>(scope.StoreName,
                scope.PhysicalKey(source, logicalKey), cancellationToken).ConfigureAwait(false);
            if (inheritedValue.Value is null)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("An inherited copy-on-write value is missing."));
            return inheritedValue;
        }

        ReadModelEntry<SharedProjectionEpochState> epoch = await ReadStateAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        long? anchor = epoch.Value?.CopyOnWriteAnchorGeneration;
        if (anchor is null || generation == anchor) return direct;
        if (anchor > generation || pointer.Value is { } older && older.PreviousGeneration > generation)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("The requested copy-on-write generation is no longer provable."));
        return await _store.GetAsync<TValue>(scope.StoreName, scope.PhysicalKey(anchor.Value, logicalKey),
            cancellationToken).ConfigureAwait(false);
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

    private async Task<bool> IsCopyOnWriteGenerationCommittedAsync(SharedProjectionScope scope,
        long generation, CancellationToken cancellationToken)
    {
        ReadModelEntry<SharedProjectionCopyOnWriteCommit> marker = await _store
            .GetAsync<SharedProjectionCopyOnWriteCommit>(scope.StoreName,
                scope.CopyOnWriteCommitKey(generation), cancellationToken).ConfigureAwait(false);
        return marker.Value?.Generation == generation;
    }

    private async Task EnsureCopyOnWriteCommitMarkerAsync(SharedProjectionScope scope,
        long generation, CancellationToken cancellationToken)
    {
        if (await IsCopyOnWriteGenerationCommittedAsync(scope, generation, cancellationToken)
            .ConfigureAwait(false)) return;
        await _store.SaveAsync(scope.StoreName, scope.CopyOnWriteCommitKey(generation),
            new SharedProjectionCopyOnWriteCommit(generation), cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReadModelBatch> BuildCopyOnWriteBatchAsync(
        SharedProjectionScope scope,
        long generation,
        string batchId,
        IEnumerable<SharedProjectionMutation> mutations,
        long selectedGeneration,
        long? anchorGeneration,
        CancellationToken cancellationToken,
        bool retireImmediately = false)
    {
        if (!scope.CopyOnWrite) return BuildBatch(scope, generation, batchId, mutations);
        // Before the first rebuild, the selected generation is itself the migration anchor.
        anchorGeneration ??= selectedGeneration;

        var operations = new List<ReadModelBatchOperation>();
        foreach (SharedProjectionMutation mutation in mutations)
        {
            if (mutation.Key.StartsWith(SharedProjectionRetiredValue.MarkerPrefix,
                StringComparison.Ordinal))
            {
                SharedProjectionRetiredValue retired = ParseRetirement(mutation, generation,
                    allowCurrent: true);
                operations.Add(retireImmediately
                    ? ReadModelBatchOperation.Delete(scope.PhysicalKey(retired.Generation,
                        retired.LogicalKey), ReadModelBatchConcurrency.IdempotentAbsent)
                    : mutation.ToOperation(scope.PhysicalKey(generation, mutation.Key)));
                continue;
            }
            ReadModelEntry<SharedProjectionCopyOnWritePointer> existing = await _store
                .GetAsync<SharedProjectionCopyOnWritePointer>(scope.StoreName,
                    scope.CopyOnWritePointerKey(mutation.Key), cancellationToken).ConfigureAwait(false);
            long priorGeneration;
            bool priorDeleted;
            long? retiredGeneration = null;
            if (existing.Value is { } pointer && pointer.LatestGeneration == generation)
            {
                priorGeneration = pointer.PreviousGeneration;
                priorDeleted = pointer.PreviousDeleted;
                retiredGeneration = pointer.RetiredGeneration;
            }
            else if (existing.Value is { } latest && latest.LatestGeneration <= selectedGeneration
                && await IsCopyOnWriteGenerationCommittedAsync(scope, latest.LatestGeneration,
                    cancellationToken).ConfigureAwait(false))
            {
                priorGeneration = latest.LatestGeneration;
                priorDeleted = latest.LatestDeleted;
                if (!latest.PreviousDeleted && latest.PreviousGeneration < latest.LatestGeneration)
                    retiredGeneration = latest.PreviousGeneration;
            }
            else if (existing.Value is { } pending && pending.PreviousGeneration <= selectedGeneration
                && await IsCopyOnWriteGenerationCommittedAsync(scope, pending.PreviousGeneration,
                    cancellationToken).ConfigureAwait(false))
            {
                priorGeneration = pending.PreviousGeneration;
                priorDeleted = pending.PreviousDeleted;
            }
            else if (existing.Value is not null)
            {
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("The inherited copy-on-write value is not provable."));
            }
            else
            {
                priorGeneration = anchorGeneration.Value;
                ReadModelEntry<object> anchorValue = await _store.GetAsync<object>(scope.StoreName,
                    scope.PhysicalKey(priorGeneration, mutation.Key), cancellationToken).ConfigureAwait(false);
                priorDeleted = anchorValue.Value is null;
            }

            var next = new SharedProjectionCopyOnWritePointer(generation,
                mutation.Kind == ReadModelBatchOperationKind.Delete, priorGeneration, priorDeleted)
            {
                RetiredGeneration = retiredGeneration
            };
            operations.Add(mutation.ToOperation(scope.PhysicalKey(generation, mutation.Key)));
            operations.Add(ReadModelBatchOperation.Write(scope.CopyOnWritePointerKey(mutation.Key), next,
                ReadModelBatchConcurrency.LastWrite));
            if (retiredGeneration is { } obsolete)
                operations.Add(ReadModelBatchOperation.Delete(scope.PhysicalKey(obsolete, mutation.Key),
                    ReadModelBatchConcurrency.IdempotentAbsent));
        }

        string stableBatchId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(batchId)));
        var identity = new ReadModelBatchScope(scope.StoreName, scope.TenantId, scope.Domain,
            "shared-" + scope.Family, scope.Family, stableBatchId);
        return new ReadModelBatch(identity, operations);
    }

    private static SharedProjectionRetiredValue ParseRetirement(
        SharedProjectionMutation mutation, long generation, bool allowCurrent = false)
    {
        if (mutation.Kind != ReadModelBatchOperationKind.Write
            || JsonSerializer.Deserialize<SharedProjectionRetiredValue>(mutation.CanonicalValue,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) is not
                { Generation: >= 0, LogicalKey: { Length: > 0 } } retired
            || (allowCurrent ? retired.Generation > generation : retired.Generation >= generation)
            || string.IsNullOrWhiteSpace(retired.LogicalKey))
            throw new InvalidOperationException("A staged copy-on-write retirement is invalid.");
        return retired;
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
            if (entry.Value is not { } current)
            {
                return;
            }

            if (current.CleanupChunks is null && current.CleanupPreparedChunks is null)
            {
                return;
            }

            var chunkStore = new SharedProjectionChunkStore(_store);
            if (current.CleanupChunks is { } payloadReference)
            {
                await chunkStore.TombstoneAsync(scope, payloadReference, cancellationToken).ConfigureAwait(false);
            }

            if (current.CleanupPreparedChunks is { } preparedReference)
            {
                await chunkStore.TombstoneAsync(scope, preparedReference, cancellationToken).ConfigureAwait(false);
            }

            if (await SaveAsync(scope, current with { CleanupChunks = null, CleanupPreparedChunks = null }, entry.ETag, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw RetryExhausted();
    }

    private async Task WritePreparedChunksAsync(
        SharedProjectionScope scope,
        SharedProjectionChunkReference reference,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await new SharedProjectionChunkStore(_store).WriteAsync(scope, reference, bytes, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
        {
            throw new SharedProjectionWriteUnavailableException(exception);
        }
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
            head.PreparedControlIndexIntent,
            head.OwnerSequenceNumber);
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

                    if (delivery.OwnerSequenceNumber > 0
                        && existing.OwnerSequenceNumber != delivery.OwnerSequenceNumber)
                    {
                        throw new InvalidOperationException("The shared projection checkpoint conflicts at the same source position.");
                    }

                    return;
                }
            }

            var checkpoint = new SharedProjectionCheckpoint(
                delivery.EnvelopePosition,
                delivery.Checkpoint.ToArray(),
                delivery.OwnerSequenceNumber,
                entry.Value?.CapturedEventDigests?.ToArray());
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

        if (delivery.OwnerSequenceNumber > 0)
        {
            int offset = bytes.Length;
            Array.Resize(ref bytes, checked(offset + sizeof(long)));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), delivery.OwnerSequenceNumber);
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
        long? marker = await GetCapturedMarkerAsync(scope, state, sourceStream, cancellationToken)
            .ConfigureAwait(false);
        return marker is { } encoded ? encoded < 0 ? ~encoded : encoded : null;
    }

    private async Task<long?> GetCapturedMarkerAsync(
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

    private Task<bool> RecordDeliveryConflictAsync(SharedProjectionScope scope, SharedProjectionEpochState state,
        string? etag, SharedProjectionDelivery delivery, string acceptedDigest, string conflictingDigest,
        CancellationToken cancellationToken, long? requiredOwnerSequenceNumber = null)
    {
        if (state.DeliveryConflicts.Any(conflict => conflict.SourceStream == delivery.SourceStream
            && conflict.EnvelopePosition == delivery.EnvelopePosition
            && conflict.RequiredOwnerSequenceNumber == requiredOwnerSequenceNumber
            && (requiredOwnerSequenceNumber is not null || conflict.AcceptedDigest == acceptedDigest)
            && conflict.ConflictingDigest == conflictingDigest)) return Task.FromResult(true);
        var conflictEvidence = new SharedProjectionDeliveryConflict(delivery.SourceStream, delivery.EnvelopePosition,
            acceptedDigest, conflictingDigest, state.Epoch) { RequiredOwnerSequenceNumber = requiredOwnerSequenceNumber };
        SharedProjectionEpochState updated = state with { DeliveryConflicts = [.. state.DeliveryConflicts, conflictEvidence] };
        if (state.DeliveryConflicts.Length >= MaxJournalEntries || IsConflictEvidenceCapacityLimited(updated))
        {
            if (state.DeliveryConflictOverflow is { } overflow)
            {
                if (overflow.SourceStream == delivery.SourceStream && overflow.EnvelopePosition == delivery.EnvelopePosition
                    && overflow.RequiredOwnerSequenceNumber == requiredOwnerSequenceNumber
                    && (requiredOwnerSequenceNumber is not null || overflow.AcceptedDigest == acceptedDigest)
                    && overflow.ConflictingDigest == conflictingDigest)
                    return Task.FromResult(true);
                throw new InvalidOperationException("The durable delivery conflict capacity is exhausted; restore or offboard the scope.");
            }
            return SaveAsync(scope, state with
            {
                DeliveryConflictOverflow = conflictEvidence,
            }, etag, cancellationToken);
        }
        return SaveAsync(scope, updated, etag, cancellationToken);
    }

    /// <summary>
    /// Explicitly resolves retained identity conflicts after a newer authoritative rebuild covers every
    /// affected source position, has been promoted, and has fully drained. Promotion or abort alone
    /// never clears the evidence. Resolved identities remain durable for idempotent rejected redelivery.
    /// </summary>
    public async Task ResolveDeliveryConflictsAsync(SharedProjectionScope scope, string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The shared projection has no epoch state.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            RequireOperation(state, operationId, SharedProjectionEpochPhase.Open);
            if (state.DeliveryConflictOverflow is not null || state.ParkingEvidenceOverflow is not null)
                throw new InvalidOperationException("The durable delivery conflict capacity is exhausted; restore or offboard the scope.");
            if (state.ActiveGeneration != state.Epoch || state.StageFingerprint is null
                || state.Journal.Length != 0 || state.OwnerSequenceHandoffPending)
                throw new InvalidOperationException("Conflict repair requires a promoted and fully drained authoritative rebuild.");
            SharedProjectionDeliveryConflict[] unresolved = [.. state.DeliveryConflicts.Where(conflict => !conflict.IsResolved)];
            SharedProjectionParkingEvidence[] unresolvedParking = [.. state.ParkingEvidence.Where(item => item.ResolvedByOperationId is null)];
            if (unresolved.Length == 0 && unresolvedParking.Length == 0) return;
            foreach (SharedProjectionParkingEvidence parked in unresolvedParking)
            {
                long? captured = await GetCapturedPositionAsync(scope, state, parked.SourceStream, cancellationToken).ConfigureAwait(false);
                if (parked.ObservedEpoch >= state.Epoch || captured is null || captured < parked.EnvelopePosition)
                    throw new InvalidOperationException("The authoritative rebuild does not cover retained parking evidence.");
            }
            foreach (SharedProjectionDeliveryConflict conflict in unresolved)
            {
                long? captured = await GetCapturedPositionAsync(scope, state, conflict.SourceStream, cancellationToken).ConfigureAwait(false);
                if (conflict.ObservedEpoch >= state.Epoch || captured is null || captured < conflict.EnvelopePosition)
                    throw new InvalidOperationException("The authoritative rebuild does not cover the retained delivery conflict.");
            }
            if (await SaveAsync(scope, state with
            {
                DeliveryConflicts = [.. state.DeliveryConflicts.Select(conflict => !conflict.IsResolved
                    ? conflict with { ResolvedByOperationId = operationId } : conflict)],
                ParkingEvidence = [.. state.ParkingEvidence.Select(parked => parked.ResolvedByOperationId is null
                    ? parked with { ResolvedByOperationId = operationId } : parked)],
            }, entry.ETag, cancellationToken).ConfigureAwait(false)) return;
        }
        throw RetryExhausted();
    }

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
        int limit = state.DeliveryConflictOverflow is null && state.ParkingEvidenceOverflow is null ? MaxEpochStateBytes - ConflictEvidenceReserveBytes : MaxEpochStateBytes;
        if (JsonSerializer.SerializeToUtf8Bytes(state).Length > limit)
        {
            throw new InvalidOperationException("The shared projection epoch exceeds its bounded state-store limit or conflict reserve.");
        }

        return _store.TrySaveAsync(scope.StoreName, scope.StateKey, state, etag ?? string.Empty, cancellationToken);
    }

    private static bool IsConflictEvidenceCapacityLimited(SharedProjectionEpochState state)
        => JsonSerializer.SerializeToUtf8Bytes(state).Length > MaxEpochStateBytes - ConflictEvidenceReserveBytes;

    private static bool HasConflictReadinessIssue(SharedProjectionEpochState state)
        => state.Journal.Any(item => item.Failure is not null)
            || state.ParkingEvidenceOverflow is not null || state.ParkingEvidence.Any(item => item.ResolvedByOperationId is null)
            || state.DeliveryConflictOverflow is not null || IsConflictEvidenceCapacityLimited(state)
            || state.DeliveryConflicts.Any(conflict => !conflict.IsResolved);

    private static bool IsValidParking(SharedProjectionEpochState state, SharedProjectionParkingEvidence? parked)
        => parked is not null && !string.IsNullOrWhiteSpace(parked.SourceStream)
            && Encoding.UTF8.GetByteCount(parked.SourceStream) <= ReadModelBatchScope.MaxComponentByteLength
            && parked.EnvelopePosition >= 0 && parked.ObservedEpoch >= 0 && parked.ObservedEpoch <= state.Epoch
            && parked.FailureCount is >= 1 and <= MaxFailureAttempts
            && (parked.ResolvedByOperationId is null || !string.IsNullOrWhiteSpace(parked.ResolvedByOperationId));

    private static bool IsValidConflict(SharedProjectionEpochState state, SharedProjectionDeliveryConflict? conflict)
        => conflict is not null && !string.IsNullOrWhiteSpace(conflict.SourceStream)
            && Encoding.UTF8.GetByteCount(conflict.SourceStream) <= ReadModelBatchScope.MaxComponentByteLength
            && conflict.EnvelopePosition >= 0 && conflict.ObservedEpoch >= 0 && conflict.ObservedEpoch <= state.Epoch
            && conflict.AcceptedDigest is { Length: 64 } && conflict.AcceptedDigest.All(Uri.IsHexDigit)
            && conflict.ConflictingDigest is { Length: 64 } && conflict.ConflictingDigest.All(Uri.IsHexDigit)
            && (conflict.ResolvedByOperationId is null || !string.IsNullOrWhiteSpace(conflict.ResolvedByOperationId))
            && conflict.RequiredOwnerSequenceNumber is not <= 0
            && (conflict.ResolvedByOwnerSequenceNumber is null || conflict.RequiredOwnerSequenceNumber is > 0
                && conflict.ResolvedByOwnerSequenceNumber == conflict.RequiredOwnerSequenceNumber);

    private static void ValidateState(SharedProjectionEpochState state)
    {
        if (state.Version != 1 || state.Epoch < 0 || state.ActiveGeneration < 0
            || state.ActiveGeneration > state.Epoch || !Enum.IsDefined(state.Phase)
            || state.CopyOnWriteAnchorGeneration is < 0
            || state.CopyOnWriteAnchorGeneration > state.ActiveGeneration
            || state.CaptureHighWatermarks is null || state.ConsumerCheckpoints is null
            || state.RequiredWriters is null || state.RegisteredWriters is null
            || state.Journal is null || state.StageKeys is null || state.DeliveryConflicts is null
            || state.ParkingEvidence is null || state.ParkingEvidence.Length > MaxJournalEntries
            || state.ParkingEvidence.Any(item => !IsValidParking(state, item))
            || state.ParkingEvidenceOverflow is not null && (!IsValidParking(state, state.ParkingEvidenceOverflow)
                || state.ParkingEvidenceOverflow.ResolvedByOperationId is not null)
            || state.DeliveryConflicts.Length > MaxJournalEntries
            || state.DeliveryConflicts.Any(conflict => !IsValidConflict(state, conflict))
            || state.DeliveryConflictOverflow is { } overflow
                && (!IsValidConflict(state, overflow) || overflow.IsResolved)
            || (state.RetainedControlIndexNames?.Length ?? 0) > MaxRetainedControlIndexes
            || (state.RetainedControlIndexNames?.Any(string.IsNullOrWhiteSpace) ?? false)
            || (state.RetainedControlIndexNames?.Distinct(StringComparer.Ordinal).Count() ?? 0)
                != (state.RetainedControlIndexNames?.Length ?? 0)
            || (state.OffboardingAuditId is not null && string.IsNullOrWhiteSpace(state.OffboardingAuditId))
            || state.ConsumerCheckpoints.Any(item => item.Value is null || item.Value.Position < 0 || item.Value.Token is null)
            || (state.OwnerSequenceHandoffPending
                && (state.ActiveGeneration != state.Epoch || state.Phase != SharedProjectionEpochPhase.CatchingUp))
            || (state.Phase == SharedProjectionEpochPhase.Capturing && state.CaptureChunks is null)
            || (state.CaptureChunks is not null && state.CaptureHighWatermarks.Count != 0)
            || (state.StageChunks is not null && state.StageMutations is not null)
            || (state.StageStreaming && (state.StageChunks is not null || state.StageMutations is not null
                || state.StageManifestDigest is not null || state.StageKeys.Length != 0))
            || state.StageCompletedPageCount < 0 || state.StageSealedPageCount < 0
            || state.StageReservedPageCount < 0
            || (state.CleanupPromotedRetirements && state.CleanupStreamPageCount == 0)
            || (state.StageStreaming && (state.StageReservedPageCount < state.StageCompletedPageCount
                || state.StageReservedPageCount > state.StageCompletedPageCount + 1))
            || (state.StageStreaming && state.StageSealedPageCount > state.StageCompletedPageCount)
            || (!state.StageStreaming && (state.StageCompletedPageCount != 0 || state.StageSealedPageCount != 0
                || state.StageReservedPageCount != 0))
            || (state.StageStreaming && state.StageFingerprint is not null
                && (state.StageSealedPageCount == 0 || state.StageCompletedPageCount != state.StageSealedPageCount))
            || state.Journal.Any(item => !item.PayloadReady && item.PayloadChunks is null)
            || state.Journal.Any(item => !item.PreparedReady && item.PreparedChunks is null)
            || state.Journal.Any(item => item.PreparedChunks is not null && item.Prepared is not null)
            || state.Journal.Any(item => item.PayloadChunks is not null
                && (item.CanonicalPayload.Length != 0 || item.Checkpoint.Length != 0))
            || state.Journal.Any(item => item.Failure is { } failure
                && (string.IsNullOrWhiteSpace(failure.SourceStream)
                    || !string.Equals(failure.SourceStream, item.SourceStream, StringComparison.Ordinal)
                    || failure.SourcePosition < 0
                    || failure.ReasonCode is not ("fold-failure" or "capacity")
                    || failure.FailureCount < 1
                    || failure.FailureCount > MaxFailureAttempts))
            || state.Journal.Any(item => item.Parked && (item.Prepared is null && item.PreparedChunks is null || item.Failure is null)))
        {
            throw new InvalidOperationException("The shared projection epoch state is invalid.");
        }
    }

    private static void ValidateBoundary(SharedProjectionEpochState state, SharedProjectionScope scope)
    {
        if (state.CopyOnWriteAnchorGeneration is not null && !scope.CopyOnWrite)
            throw new InvalidOperationException("The shared projection copy-on-write mode differs from its durable fence boundary.");
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
