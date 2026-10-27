using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

internal sealed partial class ReadModelBatchProtocol
{
    internal async Task<ReadModelBatchStagingResult> PrepareRecoveryManifestAsync(ReadModelBatch batch, IReadModelBatchFaultInjector? injector, CancellationToken token)
    {
        Initialize(batch, injector, token);
        if (!batch.DiscoverableRecovery) throw new ArgumentException("Discoverable preparation is required.", nameof(batch));
        try
        {
            var (marker, etag, exists) = await ReadMarkerAsync(token).ConfigureAwait(false);
            if (exists && (marker is null || marker.Fingerprint != _fingerprint)) return Staging(ReadModelBatchStagingStatus.Conflict, "identity-conflict");
            if (marker is not null)
                return marker.Status == ReadModelBatchMarkerStatus.Prepared ? Staging(ReadModelBatchStagingStatus.Prepared)
                    : Staging(ReadModelBatchStagingStatus.Conflict, "already-terminal");
            _ = await PrepareMarkerAsync(marker, etag, token).ConfigureAwait(false);
            return Staging(ReadModelBatchStagingStatus.Prepared);
        }
        catch (ReadModelBatchSnapshotConflictException) { return Staging(ReadModelBatchStagingStatus.Conflict, "recovery-snapshot-conflict"); }
        catch (ReadModelBatchRaceException) { return Staging(ReadModelBatchStagingStatus.Conflict, "retired-preparation"); }
        catch (ArgumentException) { throw; }
        catch { return Staging(ReadModelBatchStagingStatus.Indeterminate, "preparation-unavailable"); }
    }

    internal static async Task<ReadModelBatch?> ReadPreparedRecoveryBatchAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchScope scope, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        RawStateEntry descriptor = await accessor.ReadAsync(ReadModelRecoveryDiscovery.RegistrationKey(scope), token).ConfigureAwait(false);
        if (!descriptor.Exists) return null;
        RawStateEntry row = await accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        if (!row.Exists) return null;
        ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadBoundedProof(row.Value);
        if (marker is null || marker.ScopeHash != scope.ComputeScopeHash() || marker.BatchId != scope.BatchId)
            throw new JsonException("Invalid private recovery marker.");
        if (marker.Status is not (ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Committed)) return null;
        ReadOnlyMemory<byte> manifest = marker.OriginalManifest;
        if (marker.OriginalManifestKey is { } key)
        {
            if (key != ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())) throw new JsonException("Invalid private recovery key.");
            manifest = (await accessor.ReadAsync(key, token).ConfigureAwait(false)).Value;
        }
        using JsonDocument original = JsonDocument.Parse(manifest);
        bool retained = original.RootElement.TryGetProperty("recoverable", out JsonElement recoverable) && recoverable.GetBoolean();
        ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(manifest, scope, marker.OwnerSelection, marker.OwnerSelections,
            marker.Fingerprint, retained);
        if (!batch.DiscoverableRecovery) return null;
        var expected = new ReadModelRecoveryRegistration(scope, marker.Fingerprint, batch.Operations.Count);
        if (!descriptor.Exists || !descriptor.Value.Span.SequenceEqual(ReadModelBatchCanonicalJson.Serialize(expected).Span))
            throw new JsonException("Private recovery registration is unavailable.");
        return batch;
    }

    internal static async Task<ReadModelBatchStagingResult> ReadRecoveryDecisionAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchScope scope, string fingerprint, CancellationToken token)
    {
        scope.Validate();
        if (!ReadModelBatchFingerprint.IsCanonical(fingerprint)) throw new ArgumentException("Invalid recovery fingerprint.", nameof(fingerprint));
        RawStateEntry row = await accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        ReadModelBatchMarker? marker = row.Exists ? ReadModelBatchMarker.ReadBoundedProof(row.Value) : null;
        if (marker is null || marker.ScopeHash != scope.ComputeScopeHash() || marker.BatchId != scope.BatchId || marker.Fingerprint != fingerprint
            || marker.Status is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
            || marker.OriginalManifest is not null || marker.OriginalManifestKey is not null || marker.Operations is not null || marker.RecoveryAnchor is not null)
            return new(ReadModelBatchStagingStatus.Indeterminate, fingerprint, "terminal-evidence-unavailable");
        RawStateEntry barrier = await accessor.ReadAsync(ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()), token).ConfigureAwait(false);
        if (!barrier.Exists || !barrier.Value.Span.SequenceEqual(ReadModelRecoveryDiscovery.RetiredManifest)
            || await ReadModelBatchTerminalFence.ReadAsync(accessor, scope, fingerprint, token).ConfigureAwait(false) is not { Version: 2 } fence
            || fence.Decision != marker.Status || fence.ParticipantCount != marker.RetainedParticipantCount)
            return new(ReadModelBatchStagingStatus.Indeterminate, fingerprint, "terminal-evidence-unavailable");
        return new(marker.Status == ReadModelBatchMarkerStatus.Completed ? ReadModelBatchStagingStatus.Committed : ReadModelBatchStagingStatus.Aborted, fingerprint);
    }

    private async Task<bool> ReleaseTerminalRegistrationAsync(ReadModelBatchScope scope, string fingerprint, CancellationToken token)
    {
        var decision = await ReadRecoveryDecisionAsync(_accessor, scope, fingerprint, token).ConfigureAwait(false);
        if (decision.Status is not (ReadModelBatchStagingStatus.Committed or ReadModelBatchStagingStatus.Aborted)) return false;
        await ReadModelRecoveryDiscovery.RemoveAsync(_accessor, scope, fingerprint, token).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ClearRecoveryAnchorAsync(ReadModelBatchScope scope, string fingerprint, CancellationToken token)
    {
        RawStateEntry row = await _accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        ReadModelBatchMarker? marker = row.Exists ? ReadModelBatchMarker.ReadBoundedProof(row.Value) : null;
        if (marker is null || marker.ScopeHash != scope.ComputeScopeHash() || marker.BatchId != scope.BatchId
            || marker.Fingerprint != fingerprint || marker.Status is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)) return false;
        if (marker.RecoveryAnchor is null) return true;
        marker.RecoveryAnchor = null;
        if (await _accessor.TryWriteAsync(scope.ComputeMarkerKey(), SerializeMarker(marker), row.ETag, token).ConfigureAwait(false)) return true;
        row = await _accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        marker = row.Exists ? ReadModelBatchMarker.ReadBoundedProof(row.Value) : null;
        return marker is not null && marker.Fingerprint == fingerprint && marker.RecoveryAnchor is null
            && marker.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted;
    }

    internal async Task<bool> SettleRegisteredAsync(ReadModelRecoveryRegistration entry, CancellationToken token)
    {
        RawStateEntry row = await _accessor.ReadAsync(entry.Scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        // Enrollment precedes preparation. Neither a missing marker nor a Prepared marker is
        // permission to commit/abort another delivery or a guarded rebuild.
        if (!row.Exists) return true;
        ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadBoundedProof(row.Value);
        if (marker is null || marker.ScopeHash != entry.Scope.ComputeScopeHash()
            || marker.BatchId != entry.Scope.BatchId || marker.Fingerprint != entry.Fingerprint) return false;
        if (marker.Status is ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Aborting) return true;
        if (marker.Status == ReadModelBatchMarkerStatus.Committed)
        {
            ReadModelBatch? batch = await ReadPreparedRecoveryBatchAsync(_accessor, entry.Scope, token).ConfigureAwait(false);
            if (batch is null || batch.Operations.Count != entry.OperationCount) return false;
            Initialize(batch, null, token);
            if (!await CompactAndCompleteAsync(token).ConfigureAwait(false)) return false;
        }
        else if (marker.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
        {
            // The immutable barrier deliberately precedes the final anchor compaction. Its
            // bounded terminal proof survives a crash after reclaiming the original manifest.
            if (marker.RecoveryAnchor is not null)
                return await RetireRegisteredAsync(entry, token).ConfigureAwait(false);
            var decision = await ReadRecoveryDecisionAsync(_accessor, entry.Scope, entry.Fingerprint, token).ConfigureAwait(false);
            if (decision.Status is ReadModelBatchStagingStatus.Committed or ReadModelBatchStagingStatus.Aborted)
                return await ReleaseTerminalRegistrationAsync(entry.Scope, entry.Fingerprint, token).ConfigureAwait(false);
            if (!await VerifyTerminalOriginalAsync(entry, marker, token).ConfigureAwait(false)) return false;
        }
        else return false;
        return await RetireRegisteredAsync(entry, token).ConfigureAwait(false);
    }

    private async Task<bool> VerifyTerminalOriginalAsync(ReadModelRecoveryRegistration entry, ReadModelBatchMarker marker, CancellationToken token)
    {
        // A transaction's Completed receipt precedes physical read-back proof. Preserve its
        // immutable original until every operation has been independently verified.
        RawStateEntry original = await _accessor.ReadAsync(ReadModelBatchKeys.ManifestKey(entry.Scope.ComputeScopeHash()), token).ConfigureAwait(false);
        if (!original.Exists || original.Value.Span.SequenceEqual(ReadModelRecoveryDiscovery.RetiredManifest)) return false;
        using JsonDocument document = JsonDocument.Parse(original.Value);
        bool retained = document.RootElement.TryGetProperty("recoverable", out JsonElement recoverable) && recoverable.GetBoolean();
        var batch = ReadModelBatchManifestCodec.Decode(original.Value, entry.Scope, marker.OwnerSelection, marker.OwnerSelections,
            entry.Fingerprint, retained);
        if (!batch.DiscoverableRecovery || batch.Operations.Count != entry.OperationCount) return false;
        Initialize(batch, null, token);
        return marker.Status != ReadModelBatchMarkerStatus.Completed
            || _options.GetProfile(entry.Scope.StoreName) != ReadModelBatchStoreProfile.TransactionQualified
            || await VerifyOperationKeysAsync(token).ConfigureAwait(false);
    }

    internal async Task<bool> RetireRegisteredAsync(ReadModelRecoveryRegistration entry, CancellationToken token)
    {
        ReadModelBatchScope scope = entry.Scope;
        RawStateEntry row = await _accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        if (!row.Exists)
        {
            var aborted = new ReadModelBatchMarker { ScopeHash = scope.ComputeScopeHash(), BatchId = scope.BatchId,
                Fingerprint = entry.Fingerprint, Status = ReadModelBatchMarkerStatus.Aborted, RetainedParticipantCount = entry.OperationCount };
            if (!await _accessor.TryWriteAsync(scope.ComputeMarkerKey(), SerializeMarker(aborted), string.Empty, token).ConfigureAwait(false)) return false;
            row = await _accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false);
        }
        ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadProof(row.Value);
        if (marker is null || marker.ScopeHash != scope.ComputeScopeHash() || marker.BatchId != scope.BatchId || marker.Fingerprint != entry.Fingerprint) return false;
        if (marker.Status is not (ReadModelBatchMarkerStatus.Aborted or ReadModelBatchMarkerStatus.Completed))
        {
            ReadOnlyMemory<byte> manifest = marker.OriginalManifest;
            if (marker.OriginalManifestKey is { } key)
            {
                if (key != ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash())) return false;
                manifest = (await _accessor.ReadAsync(key, token).ConfigureAwait(false)).Value;
            }
            using JsonDocument original = JsonDocument.Parse(manifest);
            bool retained = original.RootElement.TryGetProperty("recoverable", out JsonElement recoverable) && recoverable.GetBoolean();
            ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(manifest, scope, marker.OwnerSelection, marker.OwnerSelections,
                entry.Fingerprint, retainManifestForRecovery: retained);
            Initialize(batch, null, token);
            bool retired = marker.Status == ReadModelBatchMarkerStatus.Committed
                ? await CompactAndCompleteAsync(token).ConfigureAwait(false)
                : await CompensateAsync(batch.Operations.Count, false, token).ConfigureAwait(false);
            if (!retired) return false;
            marker = ParseMarker((await _accessor.ReadAsync(scope.ComputeMarkerKey(), token).ConfigureAwait(false)).Value);
        }
        if (marker?.Status is not (ReadModelBatchMarkerStatus.Aborted or ReadModelBatchMarkerStatus.Completed)) return false;
        if (marker.RetainedParticipantCount != entry.OperationCount) return false;
        if (marker.Status == ReadModelBatchMarkerStatus.Completed
            && _options.GetProfile(scope.StoreName) == ReadModelBatchStoreProfile.TransactionQualified
            && (await ReadRecoveryDecisionAsync(_accessor, scope, entry.Fingerprint, token).ConfigureAwait(false)).Status != ReadModelBatchStagingStatus.Committed
            && !await VerifyTerminalOriginalAsync(entry, marker, token).ConfigureAwait(false)) return false;
        if (marker.RecoveryAnchor is not null
            && (!await FinishRecoveryAnchorAsync(_accessor, marker, scope, null, token, _options.TimeProvider).ConfigureAwait(false)
                || !await ClearRecoveryAnchorAsync(scope, entry.Fingerprint, token).ConfigureAwait(false))) return false;
        var fence = new ReadModelBatchTerminalFence(scope, entry.Fingerprint, [], 2, marker.Status, entry.OperationCount);
        return await fence.SealAsync(_accessor, token).ConfigureAwait(false)
            && await ReclaimOriginalManifestAsync(_accessor, scope.ComputeScopeHash(), entry.Fingerprint, token).ConfigureAwait(false)
            && await ReleaseTerminalRegistrationAsync(scope, entry.Fingerprint, token).ConfigureAwait(false);
    }
}
