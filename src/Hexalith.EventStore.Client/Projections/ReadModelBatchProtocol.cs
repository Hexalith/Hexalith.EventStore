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

        // Cancellation requested before any state access throws without touching state.
        cancellationToken.ThrowIfCancellationRequested();

        // Deterministic validation before state access (throws on violation).
        ValidateLimits(batch);

        _batch = batch;
        _injector = injector;
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        _fingerprint = ReadModelBatchFingerprint.ComputeFromManifest(manifest);
        _scopeHash = batch.Scope.ComputeScopeHash();
        _markerKey = ReadModelBatchKeys.MarkerKey(_scopeHash);
        ReadModelBatchStoreProfile profile = _options.GetProfile(batch.Scope.StoreName);
        _profileName = profile.ToString();

        // A before-dispatch fault (including simulated pre-dispatch cancellation) propagates.
        if (_injector is not null) {
            await _injector.InjectAsync(ReadModelBatchPhase.BeforeDispatch, -1, cancellationToken).ConfigureAwait(false);
        }

        try {
            return profile == ReadModelBatchStoreProfile.TransactionQualified
                ? await RunTransactionAsync(cancellationToken).ConfigureAwait(false)
                : await RunResumableAsync(cancellationToken).ConfigureAwait(false);
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

        if (committed) {
            return envelope.IsDelete
                ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
                : new RawStateEntry(true, envelope.CandidateBytes(), entry.ETag);
        }

        return envelope.PreviousBase64 is null
            ? new RawStateEntry(false, ReadOnlyMemory<byte>.Empty, string.Empty)
            : new RawStateEntry(true, envelope.PreviousBytes(), entry.ETag);
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

            throw new InvalidOperationException("Concurrent batch prepare race could not be resolved.");
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

        if (marker.Status == ReadModelBatchMarkerStatus.Committed) {
            return null;
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
                return false;
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
                return ReadModelBatchResult.AlreadyCompleted(_fingerprint);
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

        await _accessor.ExecuteTransactionAsync(operations, cancellationToken).ConfigureAwait(false);

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

        foreach (ReadModelBatchOperation operation in _batch.Operations) {
            RawStateEntry entry = await _accessor.ReadAsync(operation.Key, cancellationToken).ConfigureAwait(false);
            if (operation.Kind == ReadModelBatchOperationKind.Delete) {
                if (entry.Exists) {
                    return ReadModelBatchResult.Incomplete(_fingerprint, "transaction-delete-unverified");
                }
            }
            else if (!entry.Exists || !entry.Value.Span.SequenceEqual(operation.CanonicalValue.Span)) {
                return ReadModelBatchResult.Incomplete(_fingerprint, "transaction-write-unverified");
            }
        }

        ReadModelBatchLog.Committed(_logger, _scopeHash, _profileName, _batch.Operations.Count);
        ReadModelBatchLog.Outcome(_logger, _scopeHash, nameof(ReadModelBatchStatus.Completed), _profileName, "transaction");
        return ReadModelBatchResult.Completed(_fingerprint);
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
