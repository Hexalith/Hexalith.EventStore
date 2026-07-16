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
internal sealed class ReadModelBatchProtocol {
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
        _injector = injector;
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
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
    /// <returns>The visible raw entry (existence, bytes, ETag).</returns>
    public static async Task<RawStateEntry> ResolveVisibleAsync(
        IReadModelBatchStateAccessor accessor,
        string key,
        CancellationToken cancellationToken) {
        RawStateEntry entry = await accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (!entry.Exists || !ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span)) {
            return entry;
        }

        ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(entry.Value);
        if (envelope is null) {
            return entry;
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
            return envelope.IsDelete
                ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
                : new RawStateEntry(true, envelope.CandidateBytes(), string.Empty);
        }

        return envelope.PreviousBase64 is null
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : new RawStateEntry(true, envelope.PreviousBytes(), string.Empty);
    }

    private void ValidateLimits(ReadModelBatch batch) {
        if (batch.Operations.Count > _options.MaxOperations) {
            throw new ArgumentException(
                $"Read-model batch has {batch.Operations.Count} operations, exceeding the configured maximum of {_options.MaxOperations}.",
                nameof(batch));
        }

        foreach (ReadModelBatchOperation operation in batch.Operations) {
            int keyBytes = System.Text.Encoding.UTF8.GetByteCount(operation.Key);
            if (keyBytes > _options.MaxKeyByteLength) {
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
        if (manifest.Length > _options.MaxCanonicalManifestBytes) {
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
                    return ReadModelBatchResult.AlreadyCompleted(_fingerprint);
                case ReadModelBatchMarkerStatus.Committed:
                    return await CompactAndCompleteAsync(cancellationToken).ConfigureAwait(false)
                        ? ReadModelBatchResult.Completed(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compaction-race");
                case ReadModelBatchMarkerStatus.Aborting:
                    if (!await CompensateAsync(_batch.Operations.Count, false, cancellationToken).ConfigureAwait(false)) {
                        return ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
                    }

                    (marker, markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
                    break;
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
        var marker = new ReadModelBatchMarker {
            ScopeHash = _scopeHash,
            BatchId = _batch.Scope.BatchId,
            Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Prepared,
            Operations = BuildMarkerOperations(),
        };
        ReadOnlyMemory<byte> bytes = SerializeMarker(marker);
        bool ok = existing is null
            ? await _accessor.TryWriteAsync(_markerKey, bytes, string.Empty, cancellationToken).ConfigureAwait(false)
            : await _accessor.TryWriteAsync(_markerKey, bytes, existingEtag, cancellationToken).ConfigureAwait(false);
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

            if (TryGetOwnEnvelope(current, ordinal, out _)) {
                // Already installed on a prior attempt (resume): leave as-is.
                if (_injector is not null) {
                    await _injector.InjectAsync(ReadModelBatchPhase.AfterInstallOperation, ordinal, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            bool foreignEnvelope = current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span);
            if (foreignEnvelope || !SatisfiesConcurrency(operation, current)) {
                bool compensated = await CompensateAsync(ordinal, true, cancellationToken).ConfigureAwait(false);
                ReadModelBatchLog.Compensated(_logger, _scopeHash, _profileName);
                return compensated
                    ? ReadModelBatchResult.OptimisticConflict(_fingerprint, "pre-commit-conflict")
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
            }

            var envelope = new ReadModelBatchEnvelope {
                ScopeHash = _scopeHash,
                Fingerprint = _fingerprint,
                Ordinal = ordinal,
                IsDelete = operation.Kind == ReadModelBatchOperationKind.Delete,
                PreviousBase64 = current.Exists ? Convert.ToBase64String(current.Value.Span) : null,
                CandidateBase64 = operation.Kind == ReadModelBatchOperationKind.Write
                    ? Convert.ToBase64String(operation.CanonicalValue.Span)
                    : null,
            };

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

            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.AfterInstallOperation, ordinal, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private async Task<ReadModelBatchResult?> CommitAsync(CancellationToken cancellationToken) {
        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        (ReadModelBatchMarker? marker, string markerEtag, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (marker is null) {
            return ReadModelBatchResult.Indeterminate(_fingerprint, "marker-lost");
        }

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
            if (reread is not null && reread.Status is ReadModelBatchMarkerStatus.Committed or ReadModelBatchMarkerStatus.Completed) {
                return null;
            }

            return ReadModelBatchResult.Indeterminate(_fingerprint, "commit-race");
        }

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.AfterCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        ReadModelBatchLog.Committed(_logger, _scopeHash, _profileName, _batch.Operations.Count);
        return null;
    }

    private async Task<bool> CompactAndCompleteAsync(CancellationToken cancellationToken) {
        for (int ordinal = 0; ordinal < _batch.Operations.Count; ordinal++) {
            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            RawStateEntry current = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (_injector is not null) {
                await _injector.InjectAsync(ReadModelBatchPhase.BeforeCompaction, ordinal, cancellationToken).ConfigureAwait(false);
            }

            if (TryGetOwnEnvelope(current, ordinal, out ReadModelBatchEnvelope? envelope)) {
                if (envelope!.IsDelete) {
                    _ = await _accessor
                        .TryDeleteAsync(operation.Key, current.ETag, cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    _ = await _accessor
                        .TryWriteAsync(operation.Key, envelope.CandidateBytes(), current.ETag, cancellationToken)
                        .ConfigureAwait(false);
                }

                RawStateEntry verified = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
                if (!IsCompacted(operation, verified)) {
                    return false;
                }
            }
            else if (!IsCompacted(operation, current)) {
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
                        .TryWriteAsync(operation.Key, operation.CanonicalValue, current.ETag, cancellationToken)
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
            return true;
        }
        else if (marker.Status != ReadModelBatchMarkerStatus.Aborting) {
            return false;
        }

        for (int ordinal = installedCount - 1; ordinal >= 0; ordinal--) {
            ReadModelBatchOperation operation = _batch.Operations[ordinal];
            RawStateEntry current = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (!TryGetOwnEnvelope(current, ordinal, out ReadModelBatchEnvelope? envelope)) {
                if (requireInstalledPrefix) {
                    return false;
                }

                continue;
            }

            if (envelope!.PreviousBase64 is null) {
                _ = await _accessor
                    .TryDeleteAsync(operation.Key, current.ETag, cancellationToken)
                    .ConfigureAwait(false);
            }
            else {
                _ = await _accessor
                    .TryWriteAsync(operation.Key, envelope.PreviousBytes(), current.ETag, cancellationToken)
                    .ConfigureAwait(false);
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
            return true;
        }

        if (aborting.Status != ReadModelBatchMarkerStatus.Aborting) {
            return false;
        }

        aborting.Status = ReadModelBatchMarkerStatus.Aborted;
        bool aborted = await _accessor
            .TryWriteAsync(_markerKey, SerializeMarker(aborting), abortingEtag, cancellationToken)
            .ConfigureAwait(false);
        if (aborted) {
            return true;
        }

        (aborting, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        return aborting is not null
            && aborting.Status == ReadModelBatchMarkerStatus.Aborted
            && string.Equals(aborting.Fingerprint, _fingerprint, StringComparison.Ordinal);
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
                    ? ReadModelBatchResult.AlreadyCompleted(_fingerprint)
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-partial-on-retry");
            }
        }

        // Write the prepared identity/fingerprint record, then issue exactly one transaction.
        _ = await PrepareMarkerAsync(marker, markerEtag, cancellationToken).ConfigureAwait(false);
        ReadModelBatchLog.Prepared(_logger, _scopeHash, _profileName, _batch.Operations.Count);

        var operations = new List<RawTransactionOperation>(_batch.Operations.Count + 1);
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            bool firstWrite = operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.ExpectedETag;
            operations.Add(new RawTransactionOperation(
                operation.Key,
                operation.CanonicalValue,
                operation.Kind == ReadModelBatchOperationKind.Delete,
                firstWrite ? operation.Concurrency.ExpectedETag : string.Empty,
                firstWrite));
        }

        var receipt = new ReadModelBatchMarker {
            ScopeHash = _scopeHash,
            BatchId = _batch.Scope.BatchId,
            Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Completed,
            Operations = null,
            TerminalTimeUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };
        operations.Add(new RawTransactionOperation(_markerKey, SerializeMarker(receipt), false, string.Empty, false));

        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeCommit, -1, cancellationToken).ConfigureAwait(false);
        }

        try {
            await _accessor.ExecuteTransactionAsync(operations, cancellationToken).ConfigureAwait(false);
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
        ReadModelBatchLog.Outcome(_logger, _scopeHash, nameof(ReadModelBatchStatus.Completed), _profileName, "transaction");
        return ReadModelBatchResult.Completed(_fingerprint);
    }

    /// <summary>Reads back every operation key and proves it matches the manifest's write/delete intent.</summary>
    private async Task<bool> VerifyOperationKeysAsync(CancellationToken cancellationToken) {
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            RawStateEntry entry = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (operation.Kind == ReadModelBatchOperationKind.Delete) {
                if (entry.Exists) {
                    return false;
                }
            }
            else if (!entry.Exists || !entry.Value.Span.SequenceEqual(operation.CanonicalValue.Span)) {
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
            if (!TryGetOwnEnvelope(entry, ordinal, out _)) {
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
                    ? ReadModelBatchResult.Completed(_fingerprint)
                    : ReadModelBatchResult.Indeterminate(_fingerprint, "transaction-partial");
            }

            // The marker is still Prepared: the atomic transaction did not commit (receipt not durable). If
            // any operation's optimistic precondition no longer holds, this is a truthful optimistic conflict
            // that converges by recomputing the whole batch under a new identity; otherwise it is ambiguous.
            foreach (ReadModelBatchOperation operation in _batch.Operations) {
                RawStateEntry current = await _accessor.ReadAsync(operation.Key, token).ConfigureAwait(false);
                if (!SatisfiesConcurrency(operation, current)) {
                    return ReadModelBatchResult.OptimisticConflict(_fingerprint, "transaction-precondition");
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
                    return ReadModelBatchResult.Completed(_fingerprint);
                case ReadModelBatchMarkerStatus.Committed:
                    return await CompactAndCompleteAsync(token).ConfigureAwait(false)
                        ? ReadModelBatchResult.Completed(_fingerprint)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compaction-race");
                case ReadModelBatchMarkerStatus.Aborting:
                    return await CompensateAsync(_batch.Operations.Count, false, token).ConfigureAwait(false)
                        ? ReadModelBatchResult.OptimisticConflict(_fingerprint, reason)
                        : ReadModelBatchResult.Indeterminate(_fingerprint, "compensation-race");
                case ReadModelBatchMarkerStatus.Aborted:
                    return ReadModelBatchResult.OptimisticConflict(_fingerprint, reason);
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

    private static bool SatisfiesConcurrency(ReadModelBatchOperation operation, RawStateEntry current) {
        ReadModelBatchConcurrency concurrency = operation.Concurrency;
        return concurrency.Mode switch {
            ReadModelBatchConcurrencyMode.Unconditional => true,
            ReadModelBatchConcurrencyMode.IdempotentAbsent => true,
            ReadModelBatchConcurrencyMode.ExpectedETag when concurrency.ExpectedETag.Length == 0 => !current.Exists,
            ReadModelBatchConcurrencyMode.ExpectedETag =>
                current.Exists && string.Equals(current.ETag, concurrency.ExpectedETag, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool IsCompacted(ReadModelBatchOperation operation, RawStateEntry entry) =>
        operation.Kind == ReadModelBatchOperationKind.Delete
            ? !entry.Exists
            : entry.Exists && entry.Value.Span.SequenceEqual(operation.CanonicalValue.Span);

    private static bool IsPreviousValue(ReadModelBatchEnvelope envelope, RawStateEntry entry) =>
        envelope.PreviousBase64 is null
            ? !entry.Exists
            : entry.Exists && entry.Value.Span.SequenceEqual(envelope.PreviousBytes().Span);

    private async Task<bool> VerifyCompactedBatchAsync(CancellationToken cancellationToken) {
        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            RawStateEntry entry = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (!IsCompacted(operation, entry)) {
                return false;
            }
        }

        (ReadModelBatchMarker? marker, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        return marker is not null
            && marker.Status == ReadModelBatchMarkerStatus.Completed
            && string.Equals(marker.Fingerprint, _fingerprint, StringComparison.Ordinal);
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
            || candidate.Ordinal != ordinal) {
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
