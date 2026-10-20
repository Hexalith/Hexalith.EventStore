using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The shared coordinated-batch protocol engine. A fresh instance is created per <c>ExecuteAsync</c> call
/// and runs identically over the DAPR adapter and the in-memory fake, so both expose equivalent observable
/// outcomes. It implements both the transaction-qualified and marker-gated resumable profiles, bounded
/// same-identity reconciliation after ambiguous dispatch, pre-commit compensation, and post-commit
/// compaction, plus the shared read-visibility resolution used by <c>GetAsync</c>.
/// </summary>
internal sealed partial class ReadModelBatchProtocol {
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly IReadModelBatchStateAccessor _accessor;
    private readonly ReadModelBatchOptions _options;
    private readonly ILogger _logger;

    private ReadModelBatch _batch = null!;
    private IReadModelBatchFaultInjector? _injector;
    private string _fingerprint = string.Empty;
    private string _scopeHash = string.Empty;
    private string _markerKey = string.Empty;
    private string _profileName = string.Empty;
    private Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection? _ownerSelection;
    private IReadOnlyList<Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection>? _ownerSelections;

    /// <summary>Initializes a new engine bound to a state accessor.</summary>
    /// <param name="accessor">The raw byte-state accessor.</param>
    /// <param name="options">The batch options and per-store profiles.</param>
    /// <param name="logger">The logger for bounded structured events.</param>
    public ReadModelBatchProtocol(
        IReadModelBatchStateAccessor accessor,
        ReadModelBatchOptions options,
        ILogger logger) {
        _accessor = accessor;
        _options = options;
        _logger = logger;
    }

    /// <summary>Executes a coordinated batch.</summary>
    /// <param name="batch">The batch manifest.</param>
    /// <param name="injector">An optional deterministic fault injector (fake only).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The structured result.</returns>
    public async Task<ReadModelBatchResult> ExecuteAsync(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(batch);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BatchExecutions);

        ReadModelBatchStoreProfile profile = Initialize(batch, injector, cancellationToken);

        // A before-dispatch fault (including simulated pre-dispatch cancellation) propagates.
        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeDispatch, -1, cancellationToken).ConfigureAwait(false);
        }

        try {
            return profile == ReadModelBatchStoreProfile.TransactionQualified
                ? await RunTransactionAsync(cancellationToken).ConfigureAwait(false)
                : await RunResumableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReadModelBatchRaceException) {
            // A concurrent same-identity run advanced the marker past Prepared: resolve to a structured
            // result through bounded reconciliation instead of throwing.
            return await ReconcileAsync("prepare-race").ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // Post-dispatch cancellation is never treated as rollback: reconcile durable state.
            return await ReconcileAsync("cancellation").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException) {
            // Transport/DAPR failure after a request may have been dispatched: reconcile.
            return await ReconcileAsync("dispatch-failure").ConfigureAwait(false);
        }
    }

    /// <summary>Installs a resumable batch without committing its visibility marker.</summary>
    public async Task<ReadModelBatchStagingResult> StageAsync(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BatchExecutions);
        ReadModelBatchStoreProfile profile = Initialize(batch, injector, cancellationToken);
        if (profile != ReadModelBatchStoreProfile.Resumable) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, "transaction-profile-not-stageable");
        }

        try {
            return await RunStageAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ReadModelBatchRaceException) {
            return await ReconcileStagingAsync("prepare-race").ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            return await ReconcileStagingAsync("cancellation").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException) {
            return await ReconcileStagingAsync("dispatch-failure").ConfigureAwait(false);
        }
    }

    /// <summary>Commits and reads back a previously staged resumable batch.</summary>
    public async Task<ReadModelBatchStagingResult> CommitStagedAsync(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        ReadModelBatchStoreProfile profile = Initialize(batch, injector, cancellationToken);
        if (profile != ReadModelBatchStoreProfile.Resumable) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, "transaction-profile-not-stageable");
        }

        try {
            (ReadModelBatchMarker? marker, _, bool exists) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (!exists || marker is null) {
                return Staging(ReadModelBatchStagingStatus.Indeterminate, "marker-lost");
            }

            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return Staging(ReadModelBatchStagingStatus.Conflict, "identity-conflict");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Completed) {
                return await VerifyOperationKeysAsync(cancellationToken).ConfigureAwait(false)
                    && await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "operation-unverified");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Committed) {
                return await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "compaction-race");
            }

            if (marker.Status != ReadModelBatchMarkerStatus.Prepared
                || !await VerifyStagedOperationKeysAsync(cancellationToken).ConfigureAwait(false)) {
                return Staging(ReadModelBatchStagingStatus.Indeterminate, "candidate-unverified");
            }

            ReadModelBatchResult? failure = await CommitAsync(cancellationToken).ConfigureAwait(false);
            if (failure is not null) {
                return Map(failure);
            }

            return await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)
                ? Staging(ReadModelBatchStagingStatus.Committed)
                : Staging(ReadModelBatchStagingStatus.Indeterminate, "compaction-race");
        }
        catch (OperationCanceledException) {
            return await ReconcileStagingAsync("cancellation").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException) {
            return await ReconcileStagingAsync("dispatch-failure").ConfigureAwait(false);
        }
    }

    /// <summary>Compensates an uncommitted staged resumable batch.</summary>
    public async Task<ReadModelBatchStagingResult> AbortStagedAsync(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        ReadModelBatchStoreProfile profile = Initialize(batch, injector, cancellationToken);
        if (profile != ReadModelBatchStoreProfile.Resumable) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, "transaction-profile-not-stageable");
        }

        try {
            (ReadModelBatchMarker? marker, _, bool exists) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (!exists) {
                return Staging(ReadModelBatchStagingStatus.Aborted);
            }

            if (marker is null || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return Staging(ReadModelBatchStagingStatus.Conflict, "identity-conflict");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Aborted) {
                return Staging(ReadModelBatchStagingStatus.Aborted);
            }

            if (marker.Status is ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed) {
                return Staging(ReadModelBatchStagingStatus.Indeterminate, "already-committed");
            }

            return await CompensateAsync(_batch.Operations.Count, false, cancellationToken).ConfigureAwait(false)
                ? Staging(ReadModelBatchStagingStatus.Aborted)
                : Staging(ReadModelBatchStagingStatus.Indeterminate, "compensation-race");
        }
        catch (OperationCanceledException) {
            return await ReconcileStagingAsync("cancellation").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException) {
            return await ReconcileStagingAsync("dispatch-failure").ConfigureAwait(false);
        }
    }

    /// <summary>Reads back the current durable state of a staged manifest.</summary>
    public async Task<ReadModelBatchStagingResult> VerifyStagedAsync(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        ReadModelBatchStoreProfile profile = Initialize(batch, injector, cancellationToken);
        if (profile != ReadModelBatchStoreProfile.Resumable) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, "transaction-profile-not-stageable");
        }

        return await ReconcileStagingAsync("verify").ConfigureAwait(false);
    }

    private ReadModelBatchStoreProfile Initialize(
        ReadModelBatch batch,
        IReadModelBatchFaultInjector? injector,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLimits(batch);
        _batch = batch;
        _ownerSelection = ReadModelOwnerSelectionMetadata.FromBatch(batch);
        _ownerSelections = ReadModelOwnerSelectionMetadata.FromCoordinatedBatch(batch);
        if (batch.RetainManifestForRecovery && _ownerSelections is not null)
            throw new ArgumentException("Pending-key recovery cannot be combined with coordinated owner selections.", nameof(batch));
        _injector = injector;
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        if ((_ownerSelection is not null || _ownerSelections is not null) && manifest.Length > 1_048_576)
            throw new ArgumentException("Guarded read-model batch manifest exceeds its 1048576-byte limit.");
        _fingerprint = ReadModelBatchFingerprint.ComputeFromManifest(manifest);
        _scopeHash = batch.Scope.ComputeScopeHash();
        _markerKey = ReadModelBatchKeys.MarkerKey(_scopeHash);
        ReadModelBatchStoreProfile profile = _options.GetProfile(batch.Scope.StoreName);
        _profileName = profile.ToString();
        return profile;
    }

    private async Task<ReadModelBatchStagingResult> RunStageAsync(CancellationToken cancellationToken) {
        (ReadModelBatchMarker? marker, string markerEtag, bool exists) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (exists && marker is null) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, "unreadable-marker");
        }

        if (marker is not null) {
            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return Staging(ReadModelBatchStagingStatus.Conflict, "identity-conflict");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Completed) {
                return await VerifyOperationKeysAsync(cancellationToken).ConfigureAwait(false)
                    && await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "operation-unverified");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Committed) {
                return await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "compaction-race");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Aborting
                && !await CompensateAsync(_batch.Operations.Count, false, cancellationToken).ConfigureAwait(false)) {
                return Staging(ReadModelBatchStagingStatus.Indeterminate, "compensation-race");
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Prepared) {
                ReadModelBatchResult? resumedConflict = await InstallAsync(cancellationToken).ConfigureAwait(false);
                if (resumedConflict is not null) {
                    return Map(resumedConflict);
                }

                return await VerifyStagedOperationKeysAsync(cancellationToken).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Prepared)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "candidate-unverified");
            }

            (marker, markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = await PrepareMarkerAsync(marker, markerEtag, cancellationToken).ConfigureAwait(false);
        ReadModelBatchResult? conflict = await InstallAsync(cancellationToken).ConfigureAwait(false);
        if (conflict is not null) {
            return Map(conflict);
        }

        return await VerifyStagedOperationKeysAsync(cancellationToken).ConfigureAwait(false)
            ? Staging(ReadModelBatchStagingStatus.Prepared)
            : Staging(ReadModelBatchStagingStatus.Indeterminate, "candidate-unverified");
    }

    /// <summary>
    /// Resolves the value visible through <see cref="IReadModelStore.GetAsync{TValue}"/>, decoding both
    /// legacy raw values and versioned batch envelopes. During a prepared/aborting batch the previous
    /// complete value is returned; the candidate becomes visible only after the marker commits.
    /// </summary>
    /// <param name="accessor">The state accessor.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="timeProvider">The clock enforcing immutable source deadlines.</param>
    /// <returns>The visible raw entry (existence, bytes, ETag).</returns>
    public static async Task<RawStateEntry> ResolveVisibleAsync(
        IReadModelBatchStateAccessor accessor,
        string key,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null) {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        RawStateEntry entry = await accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (!entry.Exists || !ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span)) {
            return ReadModelBatchExpiringValue.Resolve(entry, clock);
        }

        ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(entry.Value);
        if (envelope is null) {
            throw new JsonException("Invalid read-model batch envelope.");
        }

        RawStateEntry markerEntry = await accessor
            .ReadAsync(ReadModelBatchKeys.MarkerKey(envelope.ScopeHash), cancellationToken)
            .ConfigureAwait(false);
        bool committed = false;
        if (markerEntry.Exists) {
            ReadModelBatchMarker? marker = ParseMarker(markerEntry.Value);
            committed = marker is not null
                && string.Equals(marker.Fingerprint, envelope.Fingerprint, StringComparison.Ordinal)
                && marker.Status is ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed;
        }

        // The stored bytes are a batch envelope, not the logical value, so the logical value has no stable
        // compare-and-set ETag until the key is compacted. Surface an empty ETag for any envelope-wrapped
        // read: a consumer's `TrySaveAsync(value, thatETag)` then fails closed (create-only against an
        // existing key) instead of CAS-matching the envelope wrapper and clobbering the in-flight batch.
        if (committed) {
            return envelope.IsDelete || envelope.ExpiresAtUtc <= clock.GetUtcNow()
                ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty) { ExpiresAtUtc = envelope.ExpiresAtUtc }
                : new RawStateEntry(true, envelope.CandidateBytes(), string.Empty) { ExpiresAtUtc = envelope.ExpiresAtUtc };
        }

        return envelope.PreviousBase64 is null
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : ReadModelBatchExpiringValue.Resolve(new RawStateEntry(true, envelope.PreviousBytes(), string.Empty), clock);
    }

    private void ValidateLimits(ReadModelBatch batch) {
        if (batch.Operations.Count > _options.MaxOperations
            || batch.RetainManifestForRecovery && batch.Operations.Count > ReadModelBatchOptions.DefaultMaxOperations) {
            throw new ArgumentException(
                $"Read-model batch has {batch.Operations.Count} operations, exceeding the configured maximum of {_options.MaxOperations}.",
                nameof(batch));
        }

        foreach (ReadModelBatchOperation operation in batch.Operations) {
            int keyBytes = System.Text.Encoding.UTF8.GetByteCount(operation.Key);
            if (keyBytes > _options.MaxKeyByteLength
                || batch.RetainManifestForRecovery && keyBytes > ReadModelBatchOptions.DefaultMaxKeyByteLength) {
                throw new ArgumentException(
                    $"Read-model batch logical key exceeds the configured maximum of {_options.MaxKeyByteLength} UTF-8 bytes.",
                    nameof(batch));
            }

            // Guard against a concurrency policy built through the raw record constructor with a null
            // expected ETag (the factory methods guard, but the public constructor cannot). Fail before
            // any state access rather than dereferencing a null ETag mid-install.
            if (operation.Concurrency.ExpectedETag is null) {
                throw new ArgumentException(
                    "Read-model batch operation has a null expected ETag; use the ReadModelBatchConcurrency factory methods.",
                    nameof(batch));
            }
        }

        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        if (manifest.Length > _options.MaxCanonicalManifestBytes
            || batch.RetainManifestForRecovery && manifest.Length > ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes) {
            throw new ArgumentException(
                $"Read-model batch canonical manifest is {manifest.Length} bytes, exceeding the configured maximum of {_options.MaxCanonicalManifestBytes}.",
                nameof(batch));
        }
    }

    private async Task<ReadModelBatchResult> RunResumableAsync(CancellationToken cancellationToken) {
        (ReadModelBatchMarker? marker, string markerEtag, bool exists) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (exists && marker is null) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "unreadable-marker");
        }

        if (marker is not null) {
            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return ReadModelBatchResult.IdentityConflict(_fingerprint);
            }

            switch (marker.Status) {
                case ReadModelBatchMarkerStatus.Completed:
                    return await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)
                        ? ReadModelBatchResult.AlreadyCompleted(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "manifest-cleanup-pending");
                case ReadModelBatchMarkerStatus.Committed:
                    return await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)
                        ? ReadModelBatchResult.Completed(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compaction-race");
                case ReadModelBatchMarkerStatus.Aborting:
                    if (!await CompensateAsync(_batch.Operations.Count, false, cancellationToken).ConfigureAwait(false)) {
                        return ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
                    }

                    if (_batch.RetainManifestForRecovery) return ReadModelBatchResult.OptimisticConflict(_fingerprint, "compensated");
                    (marker, markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case ReadModelBatchMarkerStatus.Aborted when _batch.RetainManifestForRecovery:
                    return await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)
                        ? ReadModelBatchResult.OptimisticConflict(_fingerprint, "compensated")
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "manifest-cleanup-pending");
                default:
                    break;
            }
        }

        // Prepare (or re-prepare from an aborted attempt) with the same identity.
        markerEtag = await PrepareMarkerAsync(marker, markerEtag, cancellationToken).ConfigureAwait(false);
        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.MarkerPrepared, -1, cancellationToken).ConfigureAwait(false);
        }

        ReadModelBatchLog.Prepared(_logger, _scopeHash, _profileName, _batch.Operations.Count);

        ReadModelBatchResult? conflict = await InstallAsync(cancellationToken).ConfigureAwait(false);
        if (conflict is not null) {
            return conflict;
        }

        ReadModelBatchResult? commitFailure = await CommitAsync(cancellationToken).ConfigureAwait(false);
        if (commitFailure is not null) {
            return commitFailure;
        }

        if (!await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "compaction-race");
        }

        ReadModelBatchLog.Outcome(_logger, _scopeHash, nameof(ReadModelBatchStatus.Completed), _profileName, "resumable");
        return ReadModelBatchResult.Completed(_fingerprint);
    }

    private async Task<string> PrepareMarkerAsync(
        ReadModelBatchMarker? existing,
        string existingEtag,
        CancellationToken cancellationToken) {
        if (await IsRetiredAsync(cancellationToken).ConfigureAwait(false)) throw new ReadModelBatchRaceException();
        var marker = new ReadModelBatchMarker {
            ScopeHash = _scopeHash,
            BatchId = _batch.Scope.BatchId,
            Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Prepared,
            Operations = BuildMarkerOperations(),
            OwnerSelection = _ownerSelection,
            OwnerSelections = _ownerSelections,
            OriginalManifest = _ownerSelection is null && _ownerSelections is null && !_batch.RetainManifestForRecovery ? null : ReadModelBatchFingerprint.BuildCanonicalManifest(_batch),
        };
        ReadOnlyMemory<byte> bytes = SerializeMarker(marker);
        if (marker.OriginalManifest is not null && bytes.Length > 16_384)
        {
            // The manifest is existing batch recovery material, with its own one-MiB ceiling.
            // Keep the control record bounded and reference only this scope's immutable bytes.
            byte[] manifest = marker.OriginalManifest!;
            marker.OriginalManifest = null;
            marker.OriginalManifestKey = ReadModelBatchKeys.ManifestKey(_scopeHash);
            bytes = SerializeMarker(marker);
            if (bytes.Length > 16_384)
                throw new ArgumentException("Guarded read-model batch control record exceeds its 16384-byte limit.");
            if (!await _accessor.TryWriteAsync(marker.OriginalManifestKey, manifest, string.Empty, cancellationToken).ConfigureAwait(false))
            {
                RawStateEntry original = await _accessor.ReadAsync(marker.OriginalManifestKey, cancellationToken).ConfigureAwait(false);
                if (!original.Exists || !original.Value.Span.SequenceEqual(manifest))
                    throw new ArgumentException("The original immutable batch manifest conflicts with this identity.");
            }
        }
        if (await IsRetiredAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = await ReclaimOriginalManifestAsync(_accessor, _scopeHash, _fingerprint, cancellationToken).ConfigureAwait(false);
            throw new ReadModelBatchRaceException();
        }
        bool ok = existing is null
            ? await _accessor.TryWriteAsync(_markerKey, bytes, string.Empty, cancellationToken).ConfigureAwait(false)
            : await _accessor.TryWriteAsync(_markerKey, bytes, existingEtag, cancellationToken).ConfigureAwait(false);
        if (ok && await IsRetiredAsync(cancellationToken).ConfigureAwait(false))
        {
            (ReadModelBatchMarker? retiredMarker, string retiredETag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (retiredMarker?.Fingerprint == _fingerprint && retiredMarker.Status == ReadModelBatchMarkerStatus.Prepared)
            {
                ReadModelBatchTerminalFence? decision = await ReadModelBatchTerminalFence.ReadAsync(_accessor, _batch.Scope, _fingerprint, cancellationToken).ConfigureAwait(false);
                retiredMarker.Status = decision?.Decision ?? ReadModelBatchMarkerStatus.Aborted;
                retiredMarker.RetainedParticipantCount = decision?.Version == 2 ? decision.ParticipantCount : 0;
                retiredMarker.OriginalManifest = null;
                retiredMarker.OriginalManifestKey = null;
                retiredMarker.Operations = null;
                _ = await _accessor.TryWriteAsync(_markerKey, SerializeMarker(retiredMarker), retiredETag, cancellationToken).ConfigureAwait(false);
            }
            _ = await ReclaimOriginalManifestAsync(_accessor, _scopeHash, _fingerprint, cancellationToken).ConfigureAwait(false);
            throw new ReadModelBatchRaceException();
        }
        if (ok && existing is null)
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ManifestOperations, _batch.Operations.Count);
        if (!ok) {
            // Lost the create/transition race; adopt whatever is now durable.
            (ReadModelBatchMarker? current, string currentEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (current is not null
                && string.Equals(current.Fingerprint, _fingerprint, StringComparison.Ordinal)
                && current.Status == ReadModelBatchMarkerStatus.Prepared) {
                return currentEtag;
            }

            // A concurrent same-identity run advanced the marker past Prepared (Committed/Completed/
            // Aborting/Aborted). This is an expected recovery outcome, not a programming error: signal the
            // engine to reconcile to a structured result rather than throwing InvalidOperationException.
            throw new ReadModelBatchRaceException();
        }

        (_, string preparedEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        return preparedEtag;
    }

    private async Task<ReadModelBatchResult?> InstallAsync(CancellationToken cancellationToken) {
        for (int ordinal = 0; ordinal < _batch.Operations.Count; ordinal++) {
            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.BeforeInstallOperation, ordinal, cancellationToken).ConfigureAwait(false);
            }

            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            RawStateEntry current = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);

            if (TryGetOwnEnvelope(current, ordinal, out ReadModelBatchEnvelope? originalEnvelope)) {
                if (_batch.RetainManifestForRecovery && !await ReadModelBatchParticipantProof.SealAsync(_accessor,
                    _batch.Scope, _fingerprint, CreateRecoveryAnchor(ordinal, originalEnvelope!), operation.Concurrency,
                    cancellationToken).ConfigureAwait(false)) return ReadModelBatchResult.Indeterminate(_fingerprint, "participant-proof-race");
                // Already installed on a prior attempt (resume): leave as-is.
                if (_injector is not null) {
                    await _injector.InjectAsync(ReadModelBatchPhase.AfterInstallOperation, ordinal, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            // An expired unconditional candidate has no participating value. Conditional
            // candidates still install their own envelope to durably prove the original CAS.
            if (IsOmittedExpired(operation)) continue;

            bool foreignEnvelope = current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span);
            if (foreignEnvelope || !SatisfiesConcurrency(operation, current)) {
                bool compensated = await CompensateAsync(ordinal, true, cancellationToken).ConfigureAwait(false);
                ReadModelBatchLog.Compensated(_logger, _scopeHash, _profileName);
                return compensated
                    ? ReadModelBatchResult.OptimisticConflict(_fingerprint, "pre-commit-conflict")
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
            }

            var envelope = new ReadModelBatchEnvelope {
                EnvelopeVersion = operation.ExpiresAtUtc is null ? 1 : 2,
                ExpiresAtUtc = operation.ExpiresAtUtc,
                ScopeHash = _scopeHash,
                Fingerprint = _fingerprint,
                Ordinal = ordinal,
                IsDelete = operation.Kind == ReadModelBatchOperationKind.Delete,
                PreviousBase64 = current.Exists ? Convert.ToBase64String(current.Value.Span) : null,
                CandidateBase64 = operation.Kind == ReadModelBatchOperationKind.Write
                    ? Convert.ToBase64String(operation.CanonicalValue.Span)
                    : null,
            };

            ReadModelBatchResult? retired = await CheckInstallerDecisionAsync(cancellationToken).ConfigureAwait(false);
            if (retired is not null) return retired;
            if (_batch.RetainManifestForRecovery
                && !await ReadModelBatchParticipantProof.SealAsync(_accessor, _batch.Scope, _fingerprint,
                    CreateRecoveryAnchor(ordinal, envelope), operation.Concurrency, cancellationToken).ConfigureAwait(false))
                return ReadModelBatchResult.Indeterminate(_fingerprint, "participant-proof-race");
            string installEtag = current.Exists ? current.ETag : string.Empty;
            bool installed = await _accessor
                .TryWriteAsync(operation.Key, envelope.ToBytes(), installEtag, cancellationToken)
                .ConfigureAwait(false);
            if (!installed) {
                bool compensated = await CompensateAsync(ordinal, true, cancellationToken).ConfigureAwait(false);
                ReadModelBatchLog.Compensated(_logger, _scopeHash, _profileName);
                return compensated
                    ? ReadModelBatchResult.OptimisticConflict(_fingerprint, "install-race")
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
            }

            retired = await CheckInstallerDecisionAsync(cancellationToken).ConfigureAwait(false);
            if (retired is not null)
            {
                RawStateEntry installedRow = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
                if (installedRow.Exists && installedRow.Value.Span.SequenceEqual(envelope.ToBytes().Span))
                {
                    bool restored = envelope.PreviousBase64 is null
                        ? await _accessor.TryDeleteAsync(operation.Key, installedRow.ETag, cancellationToken).ConfigureAwait(false)
                        : await ReadModelBatchExpiringValue.RestoreAsync(_accessor, operation.Key, envelope.PreviousBytes(), installedRow.ETag,
                            _options.TimeProvider, cancellationToken).ConfigureAwait(false);
                    if (!restored) return ReadModelBatchResult.Indeterminate(_fingerprint, "retired-installer-race");
                }
                return retired;
            }
            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.AfterInstallOperation, ordinal, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private bool MayHavePrivateFence => _batch.RetainManifestForRecovery
        || _batch.Operations.Count == 2 && _batch.Scope.AggregateId == _batch.Scope.ProjectionType;

    private async Task<bool> IsRetiredAsync(CancellationToken token) => MayHavePrivateFence
        && await ReadModelBatchTerminalFence.ReadAsync(_accessor, _batch.Scope, _fingerprint, token).ConfigureAwait(false) is not null;

    private async Task<ReadModelBatchResult?> CheckInstallerDecisionAsync(CancellationToken token)
    {
        (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(token).ConfigureAwait(false);
        if (marker?.Fingerprint != _fingerprint) return ReadModelBatchResult.IdentityConflict(_fingerprint);
        return marker.Status is ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed
            && !await IsRetiredAsync(token).ConfigureAwait(false) ? null
            : ReadModelBatchResult.OptimisticConflict(_fingerprint, "retired-installer");
    }

    private async Task<ReadModelBatchResult?> CommitAsync(CancellationToken cancellationToken) {
        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        (ReadModelBatchMarker? marker, string markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (marker is null) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "marker-lost");
        }
        if (marker.Fingerprint != _fingerprint)
            return ReadModelBatchResult.IdentityConflict(_fingerprint);
        if (await IsRetiredAsync(cancellationToken).ConfigureAwait(false))
            return ReadModelBatchResult.OptimisticConflict(_fingerprint, "retired-commit");

        if (marker.Status is ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed) {
            return null;
        }

        if (marker.Status != ReadModelBatchMarkerStatus.Prepared) {
            // A concurrent same-identity run moved the marker to Aborting/Aborted and is restoring the
            // previous view. Never force-commit over an in-flight compensation (that would produce a torn
            // committed view); reconcile on retry instead.
            return ReadModelBatchResult.Indeterminate(_fingerprint, "commit-abort-race");
        }

        marker.Status = ReadModelBatchMarkerStatus.Committed;
        bool committed = await _accessor
            .TryWriteAsync(_markerKey, SerializeMarker(marker), markerEtag, cancellationToken)
            .ConfigureAwait(false);
        if (!committed) {
            (ReadModelBatchMarker? reread, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (reread is not null && reread.Fingerprint != _fingerprint)
                return ReadModelBatchResult.IdentityConflict(_fingerprint);
            if (reread is not null && reread.Status is ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed) {
                return null;
            }

            return ReadModelBatchResult.Indeterminate(_fingerprint, "commit-race");
        }

        // Some providers accept a stale nonempty ETag when an erased key is absent.
        // Revalidate the immutable retirement decision after the awaited CAS, before any compaction.
        if (await IsRetiredAsync(cancellationToken).ConfigureAwait(false))
        {
            (ReadModelBatchMarker? retiredMarker, string retiredETag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (retiredMarker?.Fingerprint == _fingerprint)
            {
                ReadModelBatchTerminalFence? decision = await ReadModelBatchTerminalFence.ReadAsync(_accessor, _batch.Scope, _fingerprint, cancellationToken).ConfigureAwait(false);
                retiredMarker.Status = decision?.Decision ?? ReadModelBatchMarkerStatus.Aborted;
                retiredMarker.RetainedParticipantCount = decision?.Version == 2 ? decision.ParticipantCount : 0;
                retiredMarker.OriginalManifest = null;
                retiredMarker.OriginalManifestKey = null;
                ReadModelBatchTerminalFence? proof = await ReadModelBatchTerminalFence.ReadAsync(_accessor, _batch.Scope, _fingerprint, cancellationToken).ConfigureAwait(false);
                if (proof is { Anchors.Length: 2 }) retiredMarker.ScratchCompensationKey = ReadModelBatchKeys.ScratchCompensationKey(_scopeHash);
                _ = await _accessor.TryWriteAsync(_markerKey, SerializeMarker(retiredMarker), retiredETag, cancellationToken).ConfigureAwait(false);
            }
            return ReadModelBatchResult.OptimisticConflict(_fingerprint, "retired-commit");
        }
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelMutations, _batch.Operations.Count);

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.AfterCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        ReadModelBatchLog.Committed(_logger, _scopeHash, _profileName, _batch.Operations.Count);
        return null;
    }

    private async Task<bool> CompactAndCompleteAsync(CancellationToken cancellationToken) {
        ReadModelBatchRecoveryAnchor? anchor = null;
        for (int ordinal = 0; ordinal < _batch.Operations.Count; ordinal++) {
            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            RawStateEntry current = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.BeforeCompaction, ordinal, cancellationToken).ConfigureAwait(false);
            }

            if (TryGetOwnEnvelope(current, ordinal, out ReadModelBatchEnvelope? envelope)) {
                if (_batch.RetainManifestForRecovery && !await ReadModelBatchParticipantProof.SealAsync(_accessor,
                    _batch.Scope, _fingerprint, CreateRecoveryAnchor(ordinal, envelope!), operation.Concurrency,
                    cancellationToken).ConfigureAwait(false)) return false;
                if (_batch.RetainManifestForRecovery && ordinal == _batch.Operations.Count - 1)
                {
                    anchor = CreateRecoveryAnchor(ordinal, envelope!);
                    continue;
                }
                TimeSpan? retention = Retention(operation);
                if (envelope!.IsDelete || retention <= TimeSpan.Zero) {
                    _ = await _accessor
                        .TryDeleteAsync(operation.Key, current.ETag, cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    _ = await _accessor
                        .TryWriteAsync(
                            operation.Key,
                            FinalBytes(operation),
                            current.ETag,
                            retention,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                RawStateEntry verified = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
                if (!IsCompacted(operation, verified)) {
                    return false;
                }
            }
            else if (IsOmittedExpired(operation)) {
                continue; // Never help or modify an unrelated envelope at an omitted key.
            }
            else if (!IsCompacted(operation, current)) {
                // A guarded owner can have been replaced by a newer generation after this batch
                // committed. Its original receipt is recoverable; it cannot reclaim newer live keys.
                if (operation.ExpiresAtUtc is not null)
                {
                    if (current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)) return false;
                    continue; // This historical expiring candidate never reclaims a later writer.
                }
                if (_ownerSelection is not null || _ownerSelections is not null) return false;
                if (_batch.RetainManifestForRecovery)
                {
                    // Commit is the durable historical decision. An ordinary row may already be
                    // superseded after compaction; never restore our older candidate over it.
                    if (current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)) return false;
                    continue;
                }
                // The key holds neither our envelope nor the compacted value: a concurrent single-key
                // writer overwrote it after the commit marker became durable. The committed marker is the
                // durable visibility decision, so reclaim the key to the committed end-state once (a foreign
                // envelope belonging to a different batch is left untouched and retried).
                if (current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)) {
                    return false;
                }

                if (operation.Kind == ReadModelBatchOperationKind.Delete) {
                    _ = await _accessor
                        .TryDeleteAsync(operation.Key, current.ETag, cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    _ = await _accessor
                        .TryWriteAsync(
                            operation.Key,
                            operation.CanonicalValue,
                            current.ETag,
                            Retention(operation),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                RawStateEntry reclaimed = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
                if (!IsCompacted(operation, reclaimed)) {
                    return false;
                }
            }

            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.AfterCompaction, ordinal, cancellationToken).ConfigureAwait(false);
            }
        }

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeReceipt, -1, cancellationToken).ConfigureAwait(false);
        }

        var receipt = new ReadModelBatchMarker {
            ScopeHash = _scopeHash,
            BatchId = _batch.Scope.BatchId,
            Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Completed,
            OwnerSelection = _ownerSelection,
            OwnerSelections = _ownerSelections,
            RecoveryAnchor = anchor,
            RetainedParticipantCount = _batch.RetainManifestForRecovery ? _batch.Operations.Count : 0,
            Operations = null,
            TerminalTimeUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };

        // The terminal receipt is written last using a freshly read ETag. A lost transition race is
        // accepted only when read-back proves that this exact receipt is already durable.
        (ReadModelBatchMarker? marker, string markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (marker is null || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
            return false;
        }

        if (marker.Status != ReadModelBatchMarkerStatus.Completed) {
            if (marker.Status != ReadModelBatchMarkerStatus.Committed) {
                return false;
            }

            bool ok = await _accessor
                .TryWriteAsync(_markerKey, SerializeMarker(receipt), markerEtag, cancellationToken)
                .ConfigureAwait(false);
            if (!ok) {
                (marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
                if (marker is null
                    || marker.Status != ReadModelBatchMarkerStatus.Completed
                    || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                    return false;
                }
            }
        }

        if (_batch.RetainManifestForRecovery && !await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)) return false;
        return await VerifyCompactedBatchAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CompensateAsync(
        int installedCount,
        bool requireInstalledPrefix,
        CancellationToken cancellationToken) {
        // Move the marker to a recoverable aborting state before restoring the previous view.
        (ReadModelBatchMarker? marker, string currentEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (marker is null || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
            return false;
        }

        if (marker.Status == ReadModelBatchMarkerStatus.Prepared) {
            marker.Status = ReadModelBatchMarkerStatus.Aborting;
            bool moved = await _accessor
                .TryWriteAsync(_markerKey, SerializeMarker(marker), currentEtag, cancellationToken)
                .ConfigureAwait(false);
            if (!moved) {
                (marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
                if (marker is null
                    || marker.Status != ReadModelBatchMarkerStatus.Aborting
                    || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                    return false;
                }
            }
        }
        else if (marker.Status == ReadModelBatchMarkerStatus.Aborted) {
            return !_batch.RetainManifestForRecovery || await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (marker.Status != ReadModelBatchMarkerStatus.Aborting) {
            return false;
        }

        ReadModelBatchRecoveryAnchor? anchor = null;
        for (int ordinal = installedCount - 1; ordinal >= 0; ordinal--) {
            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            RawStateEntry current = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (!TryGetOwnEnvelope(current, ordinal, out ReadModelBatchEnvelope? envelope)) {
                // Expired writes are deliberately omitted by InstallAsync. They cannot own an
                // unresolved envelope at this point; preserve every foreign/newer physical value.
                if (requireInstalledPrefix && !IsOmittedExpired(operation)) {
                    return false;
                }

                continue;
            }

            if (_batch.RetainManifestForRecovery && !await ReadModelBatchParticipantProof.SealAsync(_accessor,
                _batch.Scope, _fingerprint, CreateRecoveryAnchor(ordinal, envelope!), operation.Concurrency,
                cancellationToken).ConfigureAwait(false)) return false;
            if (_batch.RetainManifestForRecovery && ordinal == 0)
            {
                anchor = CreateRecoveryAnchor(ordinal, envelope!);
                continue;
            }
            if (envelope!.PreviousBase64 is null) {
                _ = await _accessor
                    .TryDeleteAsync(operation.Key, current.ETag, cancellationToken)
                    .ConfigureAwait(false);
            }
            else {
                _ = await ReadModelBatchExpiringValue.RestoreAsync(_accessor, operation.Key,
                    envelope.PreviousBytes(), current.ETag, _options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }

            RawStateEntry verified = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (!IsPreviousValue(envelope, verified)) {
                return false;
            }
        }

        (ReadModelBatchMarker? aborting, string abortingEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (aborting is null || !string.Equals(aborting.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
            return false;
        }

        if (aborting.Status == ReadModelBatchMarkerStatus.Aborted) {
            return !_batch.RetainManifestForRecovery || await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false);
        }

        if (aborting.Status != ReadModelBatchMarkerStatus.Aborting) {
            return false;
        }

        aborting.Status = ReadModelBatchMarkerStatus.Aborted;
        if (_batch.RetainManifestForRecovery)
        {
            aborting.RecoveryAnchor = anchor;
            aborting.RetainedParticipantCount = _batch.Operations.Count;
            aborting.OriginalManifest = null;
            aborting.OriginalManifestKey = null;
            aborting.Operations = null;
        }
        bool aborted = await _accessor
            .TryWriteAsync(_markerKey, SerializeMarker(aborting), abortingEtag, cancellationToken)
            .ConfigureAwait(false);
        if (aborted) {
            return !_batch.RetainManifestForRecovery || await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false);
        }

        (aborting, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        return aborting is not null
            && aborting.Status == ReadModelBatchMarkerStatus.Aborted
            && string.Equals(aborting.Fingerprint, _fingerprint, StringComparison.Ordinal)
            && (!_batch.RetainManifestForRecovery || await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<ReadModelBatchResult> RunTransactionAsync(CancellationToken cancellationToken) {
        if (!_accessor.SupportsTransaction) {
            throw new InvalidOperationException(
                $"Store '{_batch.Scope.StoreName}' is configured TransactionQualified but the backing store cannot execute state transactions.");
        }

        (ReadModelBatchMarker? marker, string markerEtag, bool exists) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (exists && marker is null) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "unreadable-marker");
        }

        if (marker is not null) {
            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return ReadModelBatchResult.IdentityConflict(_fingerprint);
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Completed) {
                // Never trust the receipt the (possibly mis-qualified) transaction wrote on a retry: prove
                // the operation keys are durable before reporting idempotent success.
                return await VerifyOperationKeysAsync(cancellationToken).ConfigureAwait(false)
                    && await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false)
                    ? ReadModelBatchResult.AlreadyCompleted(_fingerprint)
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-partial-on-retry");
            }
        }

        if (marker?.Status is ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted)
            return await SettleTransactionConflictAsync(cancellationToken).ConfigureAwait(false);

        // Write the prepared identity/fingerprint record, then issue exactly one transaction.
        _ = await PrepareMarkerAsync(marker, markerEtag, cancellationToken).ConfigureAwait(false);
        ReadModelBatchLog.Prepared(_logger, _scopeHash, _profileName, _batch.Operations.Count);

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeCommit, -1, cancellationToken).ConfigureAwait(false);
        }
        // Complete every asynchronous physical-CAS preflight before assembling any final TTL.
        // No SDK await may retain an earlier candidate's stale positive duration across its deadline.
        var physical = new Dictionary<string, RawStateEntry>(StringComparer.Ordinal);
        foreach (ReadModelBatchOperation operation in _batch.Operations)
            if (operation.ExpiresAtUtc is not null || operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.ExpectedETag
                    && operation.Concurrency.ExpectedETag.Length == 0)
                physical.Add(operation.Key, await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false));
        (ReadModelBatchMarker? prepared, string transactionMarkerETag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (prepared?.Fingerprint != _fingerprint) return ReadModelBatchResult.IdentityConflict(_fingerprint);
        if (prepared.Status != ReadModelBatchMarkerStatus.Prepared)
            return ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-marker-race");
        var operations = new List<RawTransactionOperation>(_batch.Operations.Count + 1);
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            TimeSpan? retention = Retention(operation);
            if (retention <= TimeSpan.Zero && operation.Concurrency.Mode != ReadModelBatchConcurrencyMode.ExpectedETag) continue;
            bool firstWrite = operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.ExpectedETag;
            string etag = firstWrite ? operation.Concurrency.ExpectedETag : string.Empty;
            if (physical.TryGetValue(operation.Key, out RawStateEntry current))
            {
                if (current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span))
                    return ReadModelBatchResult.Indeterminate(_fingerprint, "foreign-envelope");
                if (!SatisfiesConcurrency(operation, current))
                    return await SettleTransactionConflictAsync(cancellationToken).ConfigureAwait(false);
                firstWrite = true;
                etag = current.ETag;
            }
            operations.Add(new RawTransactionOperation(
                operation.Key,
                FinalBytes(operation),
                operation.Kind == ReadModelBatchOperationKind.Delete || retention <= TimeSpan.Zero,
                etag,
                firstWrite,
                retention <= TimeSpan.Zero ? null : retention));
        }

        var receipt = new ReadModelBatchMarker {
            ScopeHash = _scopeHash,
            BatchId = _batch.Scope.BatchId,
            Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Completed,
            OwnerSelection = _ownerSelection,
            OwnerSelections = _ownerSelections,
            Operations = null,
            TerminalTimeUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };
        operations.Add(new RawTransactionOperation(_markerKey, SerializeMarker(receipt), false, transactionMarkerETag, true));

        try {
            await _accessor.ExecuteTransactionAsync(operations, cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelMutations, _batch.Operations.Count);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException) {
            // The atomic transaction threw (ETag/create-only rejection, transport failure, or post-dispatch
            // cancellation). Never re-issue under a new identity/profile: reconcile the same marker and keys
            // and classify a proven optimistic-precondition failure as a convergent Conflict (recompute the
            // whole batch), otherwise Indeterminate. Returning Incomplete here would tell the caller to
            // retry the identical doomed transaction forever.
            return await ReconcileTransactionAsync().ConfigureAwait(false);
        }

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.AfterCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        // Success follows read-back proof, never the void transaction response.
        return await VerifyTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReadModelBatchResult> VerifyTransactionAsync(CancellationToken cancellationToken) {
        (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (marker is null
            || !string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)
            || marker.Status != ReadModelBatchMarkerStatus.Completed) {
            return ReadModelBatchResult.Incomplete(_fingerprint, "transaction-marker-unverified");
        }

        if (!await VerifyOperationKeysAsync(cancellationToken).ConfigureAwait(false)) {
            return ReadModelBatchResult.Incomplete(_fingerprint, "transaction-operation-unverified");
        }

        ReadModelBatchLog.Committed(_logger, _scopeHash, _profileName, _batch.Operations.Count);
        if (!await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false))
            return ReadModelBatchResult.Indeterminate(_fingerprint, "manifest-cleanup-pending");
        ReadModelBatchLog.Outcome(_logger, _scopeHash, nameof(ReadModelBatchStatus.Completed), _profileName, "transaction");
        return ReadModelBatchResult.Completed(_fingerprint);
    }

    /// <summary>Reads back every operation key and proves it matches the manifest's write/delete intent.</summary>
    private async Task<bool> VerifyOperationKeysAsync(CancellationToken cancellationToken) {
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            RawStateEntry entry = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (operation.ExpiresAtUtc <= _options.TimeProvider.GetUtcNow())
            {
                if (!IsOmittedExpired(operation) && entry.Exists && ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span)) return false;
                continue;
            }
            if (operation.Kind == ReadModelBatchOperationKind.Delete) {
                if (entry.Exists) {
                    return false;
                }
            }
            else if (!entry.Exists || !entry.Value.Span.SequenceEqual(FinalBytes(operation).Span)) {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> VerifyStagedOperationKeysAsync(CancellationToken cancellationToken) {
        for (int ordinal = 0; ordinal < _batch.Operations.Count; ordinal++) {
            RawStateEntry entry = await _accessor
                .ReadAsync(_batch.Operations[ordinal].Key, cancellationToken)
                .ConfigureAwait(false);
            if (!TryGetOwnEnvelope(entry, ordinal, out _)
                && !IsOmittedExpired(_batch.Operations[ordinal])) {
                return false;
            }
        }

        (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        return marker is not null
            && marker.Status == ReadModelBatchMarkerStatus.Prepared
            && string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reconciles a transaction-qualified batch after the single transaction threw or was ambiguous, using a
    /// bounded, caller-token-independent read-back. A durable receipt is trusted only when the operation keys
    /// are also durable; a still-prepared marker with a failed precondition is a convergent optimistic
    /// conflict; anything else is indeterminate.
    /// </summary>
    private async Task<ReadModelBatchResult> ReconcileTransactionAsync() {
        using var reconcileSource = new CancellationTokenSource(_options.ReconciliationTimeout);
        CancellationToken token = reconcileSource.Token;
        ReadModelBatchLog.Reconciling(_logger, _scopeHash, _profileName, "transaction-dispatch");
        try {
            (ReadModelBatchMarker? marker, _, bool exists) = await ReadMarkerAsync(token).ConfigureAwait(false);
            if (!exists || marker is null) {
                return ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-dispatch");
            }

            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return ReadModelBatchResult.IdentityConflict(_fingerprint);
            }

            if (marker.Status == ReadModelBatchMarkerStatus.Completed) {
                // The receipt is part of the transaction, so a durable receipt only proves completion when
                // the operation keys are also durable.
                return await VerifyOperationKeysAsync(token).ConfigureAwait(false)
                    && await CleanupOriginalManifestAsync(token).ConfigureAwait(false)
                    ? ReadModelBatchResult.Completed(_fingerprint)
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-partial");
            }

            if (marker.Status is ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted)
                return await SettleTransactionConflictAsync(token).ConfigureAwait(false);

            // The marker is still Prepared: the atomic transaction did not commit (receipt not durable). If
            // any operation's optimistic precondition no longer holds, this is a truthful optimistic conflict
            // that converges by recomputing the whole batch under a new identity; otherwise it is ambiguous.
            foreach (ReadModelBatchOperation operation in _batch.Operations) {
                RawStateEntry current = await _accessor.ReadAsync(operation.Key, token).ConfigureAwait(false);
                if (!SatisfiesConcurrency(operation, current)) {
                    return await SettleTransactionConflictAsync(token).ConfigureAwait(false);
                }
            }

            return ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-dispatch");
        }
        catch (Exception) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-dispatch");
        }
    }

    private async Task<ReadModelBatchResult> ReconcileAsync(string reason) {
        using var reconcileSource = new CancellationTokenSource(_options.ReconciliationTimeout);
        CancellationToken token = reconcileSource.Token;
        ReadModelBatchLog.Reconciling(_logger, _scopeHash, _profileName, reason);
        try {
            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.Reconcile, -1, token).ConfigureAwait(false);
            }

            (ReadModelBatchMarker? marker, string markerEtag, bool exists) = await ReadMarkerAsync(token).ConfigureAwait(false);
            if (!exists || marker is null) {
                return ReadModelBatchResult.Indeterminate(_fingerprint, reason);
            }

            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return ReadModelBatchResult.IdentityConflict(_fingerprint);
            }

            switch (marker.Status) {
                case ReadModelBatchMarkerStatus.Completed:
                    return await CleanupOriginalManifestAsync(token).ConfigureAwait(false)
                        ? ReadModelBatchResult.Completed(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "manifest-cleanup-pending");
                case ReadModelBatchMarkerStatus.Committed:
                    return await CompactAndCompleteAsync(token).ConfigureAwait(false)
                        ? ReadModelBatchResult.Completed(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compaction-race");
                case ReadModelBatchMarkerStatus.Aborting:
                    return await CompensateAsync(_batch.Operations.Count, false, token).ConfigureAwait(false)
                        ? ReadModelBatchResult.OptimisticConflict(_fingerprint, reason)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
                case ReadModelBatchMarkerStatus.Aborted:
                    return !_batch.RetainManifestForRecovery || await CleanupOriginalManifestAsync(token).ConfigureAwait(false)
                        ? ReadModelBatchResult.OptimisticConflict(_fingerprint, reason)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "manifest-cleanup-pending");
                default:
                    return ReadModelBatchResult.Incomplete(_fingerprint, reason);
            }
        }
        catch (Exception) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, reason);
        }
    }

    private async Task<ReadModelBatchStagingResult> ReconcileStagingAsync(string reason) {
        using var reconcileSource = new CancellationTokenSource(_options.ReconciliationTimeout);
        CancellationToken token = reconcileSource.Token;
        try {
            (ReadModelBatchMarker? marker, _, bool exists) = await ReadMarkerAsync(token).ConfigureAwait(false);
            if (!exists || marker is null) {
                return Staging(ReadModelBatchStagingStatus.Indeterminate, reason);
            }

            if (!string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal)) {
                return Staging(ReadModelBatchStagingStatus.Conflict, "identity-conflict");
            }

            return marker.Status switch {
                ReadModelBatchMarkerStatus.Prepared => await VerifyStagedOperationKeysAsync(token).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Prepared)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "candidate-unverified"),
                ReadModelBatchMarkerStatus.Committed => await CompactAndCompleteAsync(token).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "compaction-race"),
                ReadModelBatchMarkerStatus.Completed => await VerifyOperationKeysAsync(token).ConfigureAwait(false)
                    && await CleanupOriginalManifestAsync(token).ConfigureAwait(false)
                    ? Staging(ReadModelBatchStagingStatus.Committed)
                    : Staging(ReadModelBatchStagingStatus.Indeterminate, "operation-unverified"),
                ReadModelBatchMarkerStatus.Aborted => Staging(ReadModelBatchStagingStatus.Aborted),
                _ => Staging(ReadModelBatchStagingStatus.Indeterminate, reason),
            };
        }
        catch (Exception) {
            return Staging(ReadModelBatchStagingStatus.Indeterminate, reason);
        }
    }

    private ReadModelBatchStagingResult Staging(ReadModelBatchStagingStatus status, string? reason = null)
        => new(status, _fingerprint, reason);

    private ReadModelBatchStagingResult Map(ReadModelBatchResult result)
        => result.Status switch {
            ReadModelBatchStatus.Completed or ReadModelBatchStatus.AlreadyCompleted =>
                Staging(ReadModelBatchStagingStatus.Committed, result.RecoveryReason),
            ReadModelBatchStatus.Conflict =>
                Staging(ReadModelBatchStagingStatus.Conflict, result.RecoveryReason),
            _ => Staging(ReadModelBatchStagingStatus.Indeterminate, result.RecoveryReason),
        };

    private async Task<(ReadModelBatchMarker? Marker, string ETag, bool Exists)> ReadMarkerAsync(CancellationToken cancellationToken) {
        RawStateEntry entry = await _accessor.ReadAsync(_markerKey, cancellationToken).ConfigureAwait(false);
        if (!entry.Exists) {
            return (null, string.Empty, false);
        }

        return (ParseMarker(entry.Value), entry.ETag, true);
    }

    private async Task<ReadModelBatchResult> SettleTransactionConflictAsync(CancellationToken token)
        => await CompensateAsync(0, false, token).ConfigureAwait(false)
            ? ReadModelBatchResult.OptimisticConflict(_fingerprint, "transaction-precondition")
            : ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-conflict-unsettled");

    private bool IsOmittedExpired(ReadModelBatchOperation operation)
        => operation.ExpiresAtUtc <= _options.TimeProvider.GetUtcNow()
            && operation.Concurrency.Mode != ReadModelBatchConcurrencyMode.ExpectedETag;

    private bool SatisfiesConcurrency(ReadModelBatchOperation operation, RawStateEntry current) {
        ReadModelBatchConcurrency concurrency = operation.Concurrency;
        return concurrency.Mode switch {
            ReadModelBatchConcurrencyMode.Unconditional => true,
            ReadModelBatchConcurrencyMode.IdempotentAbsent => true,
            ReadModelBatchConcurrencyMode.ExpectedETag when concurrency.ExpectedETag.Length == 0 =>
                !ReadModelBatchExpiringValue.Resolve(current, _options.TimeProvider).Exists,
            ReadModelBatchConcurrencyMode.ExpectedETag =>
                current.Exists && string.Equals(current.ETag, concurrency.ExpectedETag, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static ReadOnlyMemory<byte> FinalBytes(ReadModelBatchOperation operation)
        => operation.ExpiresAtUtc is { } deadline
            ? ReadModelBatchExpiringValue.Encode(operation.CanonicalValue, deadline) : operation.CanonicalValue;

    private TimeSpan? Retention(ReadModelBatchOperation operation)
        => ReadModelBatchExpiringValue.Retention(operation.ExpiresAtUtc, operation.TimeToLive, _options.TimeProvider);

    private bool IsCompacted(ReadModelBatchOperation operation, RawStateEntry entry) =>
        operation.ExpiresAtUtc <= _options.TimeProvider.GetUtcNow()
            ? !entry.Exists || !ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span)
                || IsOmittedExpired(operation) && !IsOwnParticipant(operation, entry)
            : operation.Kind == ReadModelBatchOperationKind.Delete
                ? !entry.Exists
                : entry.Exists && (operation.ExpiresAtUtc is { } deadline
                    ? ReadModelBatchExpiringValue.IsExpiring(entry.Value.Span)
                        && ReadModelBatchExpiringValue.Decode(entry.Value) is var final
                        && final.Deadline == deadline && final.Value.Span.SequenceEqual(operation.CanonicalValue.Span)
                    : entry.Value.Span.SequenceEqual(operation.CanonicalValue.Span));

    private bool IsOwnParticipant(ReadModelBatchOperation operation, RawStateEntry entry)
    {
        ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(entry.Value);
        return envelope is not null && envelope.Ordinal >= 0 && envelope.Ordinal < _batch.Operations.Count
            && _batch.Operations[envelope.Ordinal].Key == operation.Key
            && TryGetOwnEnvelope(entry, envelope.Ordinal, out _);
    }

    private bool IsPreviousValue(ReadModelBatchEnvelope envelope, RawStateEntry entry) =>
        envelope.PreviousBase64 is null
            || !ReadModelBatchExpiringValue.Resolve(new RawStateEntry(true, envelope.PreviousBytes(), string.Empty), _options.TimeProvider).Exists
            ? !entry.Exists
            : entry.Exists && entry.Value.Span.SequenceEqual(envelope.PreviousBytes().Span);

    private async Task<bool> VerifyCompactedBatchAsync(CancellationToken cancellationToken) {
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            RawStateEntry entry = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (!IsCompacted(operation, entry)
                && (!_batch.RetainManifestForRecovery || _ownerSelection is not null || _ownerSelections is not null || entry.Exists && ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span))) {
                return false;
            }
        }

        (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        bool complete = marker is not null
            && marker.Status == ReadModelBatchMarkerStatus.Completed
            && string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal);
        return complete && await CleanupOriginalManifestAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CleanupOriginalManifestAsync(CancellationToken cancellationToken)
    {
        if (_batch.RetainManifestForRecovery)
        {
            (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
            if (marker?.Status is ReadModelBatchMarkerStatus.Aborted or ReadModelBatchMarkerStatus.Completed
                && !await new ReadModelBatchTerminalFence(_batch.Scope, _fingerprint, [], 2,
                    marker.Status, _batch.Operations.Count).SealAsync(_accessor, cancellationToken).ConfigureAwait(false))
                return false;
            if (marker?.RecoveryAnchor is not null)
                return await FinishRecoveryAnchorAsync(_accessor, marker, _batch.Scope, _injector, cancellationToken, _options.TimeProvider).ConfigureAwait(false);
        }
        return _ownerSelection is null && _ownerSelections is null && !_batch.RetainManifestForRecovery
            || await ReclaimOriginalManifestAsync(_accessor, _scopeHash, _fingerprint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reclaims only the same terminal receipt's original immutable manifest; uncertainty retains owned retry work.</summary>
    internal static async Task<bool> ReclaimOriginalManifestAsync(IReadModelBatchStateAccessor accessor,
        string scopeHash, string fingerprint, CancellationToken cancellationToken)
    {
        string key = ReadModelBatchKeys.ManifestKey(scopeHash);
        try
        {
            RawStateEntry stored = await accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
            if (!stored.Exists) return true;
            if (stored.Value.Length > 1_048_576
                || ReadModelBatchFingerprint.ComputeFromManifest(stored.Value.Span) != fingerprint) return false;
            if (await accessor.TryDeleteAsync(key, stored.ETag, cancellationToken).ConfigureAwait(false)) return true;
        }
        catch (Exception)
        {
            // A lost response can still have deleted the exact manifest. Read back
            // below under this same bounded attempt; no model operation is repeated.
        }
        try { return !(await accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false)).Exists; }
        catch (Exception) { return false; }
    }

    private bool TryGetOwnEnvelope(RawStateEntry entry, int ordinal, out ReadModelBatchEnvelope? envelope) {
        envelope = null;
        if (!entry.Exists || !ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span)) {
            return false;
        }

        ReadModelBatchEnvelope? candidate = ReadModelBatchEnvelope.FromBytes(entry.Value);
        if (candidate is null
            || !string.Equals(candidate.ScopeHash, _scopeHash, StringComparison.Ordinal)
            || !string.Equals(candidate.Fingerprint, _fingerprint, StringComparison.Ordinal)
            || candidate.ExpiresAtUtc != _batch.Operations[ordinal].ExpiresAtUtc
            || candidate.Ordinal != ordinal
            || candidate.IsDelete != (_batch.Operations[ordinal].Kind == ReadModelBatchOperationKind.Delete)
            || !candidate.CandidateBytes().Span.SequenceEqual(_batch.Operations[ordinal].CanonicalValue.Span)) {
            return false;
        }

        envelope = candidate;
        return true;
    }

    private IReadOnlyList<ReadModelBatchMarkerOperation> BuildMarkerOperations() {
        var markerOperations = new List<ReadModelBatchMarkerOperation>(_batch.Operations.Count);
        for (int ordinal = 0; ordinal < _batch.Operations.Count; ordinal++) {
            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            markerOperations.Add(new ReadModelBatchMarkerOperation {
                Ordinal = ordinal,
                Key = operation.Key,
                IsDelete = operation.Kind == ReadModelBatchOperationKind.Delete,
            });
        }

        return markerOperations;
    }

    private static ReadOnlyMemory<byte> SerializeMarker(ReadModelBatchMarker marker) =>
        ReadModelBatchCanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(marker, s_json));

    private static ReadModelBatchMarker? ParseMarker(ReadOnlyMemory<byte> bytes) {
        try {
            return JsonSerializer.Deserialize<ReadModelBatchMarker>(bytes.Span, s_json);
        }
        catch (JsonException) {
            return null;
        }
    }
}
