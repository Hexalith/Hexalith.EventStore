using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Uses existing envelopes and immutable manifest bytes to rediscover an interrupted conditional batch.</summary>
internal static class ReadModelPendingBatchRecovery
{
    /// <summary>Validates original scope and operation evidence before resuming the existing protocol.</summary>
    internal static async Task<ReadModelPendingBatchRecoveryResult> RecoverAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchOptions options, ILogger logger, ReadModelBatchScope expected, string key,
        IReadModelBatchFaultInjector? injector, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(expected);
        expected.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            RawStateEntry current = await accessor.ReadAsync(key, token).ConfigureAwait(false);
            if (!current.Exists || !ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)) return new(true);
            ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(current.Value);
            if (envelope is null || envelope.EnvelopeVersion is not (1 or 2) || !ReadModelBatchFingerprint.IsCanonical(envelope.Fingerprint)
                || envelope.ScopeHash is not { Length: 43 } || envelope.ScopeHash.Any(static ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_'))
                return new(false, "invalid-recovery-evidence");
            RawStateEntry row = await accessor.ReadAsync(ReadModelBatchKeys.MarkerKey(envelope.ScopeHash), token).ConfigureAwait(false);
            if (!row.Exists || row.Value.Length > 16_384) return new(false, "invalid-recovery-evidence");
            ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadProof(row.Value);
            if (marker is null || marker.Status is not (ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Committed
                    or ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted or ReadModelBatchMarkerStatus.Completed) || marker.ScopeHash != envelope.ScopeHash || marker.Fingerprint != envelope.Fingerprint
                || marker.OriginalManifest is not null && marker.OriginalManifestKey is not null)
                return new(false, "invalid-recovery-evidence");
            if (marker.RecoveryAnchor is { } anchor)
            {
                if (marker.OriginalManifest is not null || marker.OriginalManifestKey is not null || marker.Operations is not null
                    || anchor.Key != key || !anchor.Matches(marker, expected) || !anchor.Matches(marker, envelope))
                    return new(false, "invalid-recovery-evidence");
                return await ReadModelBatchProtocol.FinishRecoveryAnchorAsync(accessor, marker, expected, injector, token, options.TimeProvider).ConfigureAwait(false)
                    ? new(true, WasRecovered: true) : new(false, "recovery-incomplete");
            }
            ReadOnlyMemory<byte> manifest = marker.OriginalManifest;
            if (marker.OriginalManifestKey is not null)
            {
                if (marker.OriginalManifestKey != ReadModelBatchKeys.ManifestKey(marker.ScopeHash))
                    return new(false, "invalid-recovery-evidence");
                RawStateEntry original = await accessor.ReadAsync(marker.OriginalManifestKey, token).ConfigureAwait(false);
                if (!original.Exists) return new(false, "invalid-recovery-evidence");
                manifest = original.Value;
            }
            if (manifest.IsEmpty || manifest.Length > options.MaxCanonicalManifestBytes)
                return new(false, "recovery-manifest-unavailable");
            using JsonDocument document = JsonDocument.Parse(manifest);
            if (!document.RootElement.TryGetProperty("recoverable", out JsonElement optedIn) || !optedIn.GetBoolean())
                return new(false, "recovery-manifest-unavailable");
            JsonElement source = document.RootElement.GetProperty("scope");
            var scope = new ReadModelBatchScope(source.GetProperty("store").GetString()!, source.GetProperty("tenant").GetString()!,
                source.GetProperty("domain").GetString()!, source.GetProperty("aggregate").GetString()!,
                source.GetProperty("projection").GetString()!, source.GetProperty("batch").GetString()!);
            if (scope.StoreName != expected.StoreName || scope.TenantId != expected.TenantId || scope.Domain != expected.Domain
                || scope.AggregateId != expected.AggregateId || scope.ProjectionType != expected.ProjectionType
                || scope.BatchId != marker.BatchId || scope.ComputeScopeHash() != envelope.ScopeHash)
                return new(false, "recovery-scope-mismatch");
            ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(manifest, scope, marker.OwnerSelection,
                marker.OwnerSelections, marker.Fingerprint, retainManifestForRecovery: true);
            if (marker.Operations is null || marker.Operations.Count != batch.Operations.Count
                || marker.Operations.Where((operation, index) => operation.Ordinal != index || operation.Key != batch.Operations[index].Key
                    || operation.IsDelete != (batch.Operations[index].Kind == ReadModelBatchOperationKind.Delete)).Any()
                || envelope.Ordinal < 0 || envelope.Ordinal >= batch.Operations.Count
                || batch.Operations[envelope.Ordinal].Key != key
                || envelope.ExpiresAtUtc != batch.Operations[envelope.Ordinal].ExpiresAtUtc
                || envelope.IsDelete != (batch.Operations[envelope.Ordinal].Kind == ReadModelBatchOperationKind.Delete)
                || !envelope.CandidateBytes().Span.SequenceEqual(batch.Operations[envelope.Ordinal].CanonicalValue.Span))
                return new(false, "invalid-recovery-evidence");
            foreach ((ReadModelBatchOperation operation, int ordinal) in batch.Operations.Select((operation, ordinal) => (operation, ordinal)))
            {
                RawStateEntry participating = await accessor.ReadAsync(operation.Key, token).ConfigureAwait(false);
                if (!participating.Exists || !ReadModelBatchEnvelope.IsEnvelope(participating.Value.Span)) continue;
                ReadModelBatchEnvelope? installed = ReadModelBatchEnvelope.FromBytes(participating.Value);
                if (installed is null) return new(false, "invalid-recovery-evidence");
                if (installed.ScopeHash != marker.ScopeHash) continue; // foreign participant is handled as a conflict, never helped
                if (installed.Fingerprint != marker.Fingerprint || installed.Ordinal != ordinal
                    || operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.ExpectedETag
                        && (operation.Concurrency.ExpectedETag.Length == 0) != (installed.PreviousBase64 is null)
                    || installed.ExpiresAtUtc != operation.ExpiresAtUtc
                    || installed.IsDelete != (operation.Kind == ReadModelBatchOperationKind.Delete)
                    || !installed.CandidateBytes().Span.SequenceEqual(operation.CanonicalValue.Span))
                    return new(false, "invalid-recovery-evidence");
            }
            var protocol = new ReadModelBatchProtocol(accessor, options, logger);
            ReadModelBatchResult result = await protocol.ExecuteAsync(batch, injector, token).ConfigureAwait(false);
            if (!result.IsSuccess && result.ConflictKind != ReadModelBatchConflictKind.Optimistic)
                return new(false, "recovery-incomplete");
            RawStateEntry after = await accessor.ReadAsync(key, token).ConfigureAwait(false);
            return !after.Exists || !ReadModelBatchEnvelope.IsEnvelope(after.Value.Span)
                ? new(true, WasRecovered: true) : new(false, "recovery-incomplete");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return new(false, "invalid-recovery-evidence");
        }
        catch { return new(false, "recovery-unavailable"); }
    }
}
