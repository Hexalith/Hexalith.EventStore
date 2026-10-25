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
        var fence = new ReadModelBatchTerminalFence(scope, entry.Fingerprint, [], 2, marker.Status, entry.OperationCount);
        return await fence.SealAsync(_accessor, token).ConfigureAwait(false)
            && await ReclaimOriginalManifestAsync(_accessor, scope.ComputeScopeHash(), entry.Fingerprint, token).ConfigureAwait(false);
    }
}
