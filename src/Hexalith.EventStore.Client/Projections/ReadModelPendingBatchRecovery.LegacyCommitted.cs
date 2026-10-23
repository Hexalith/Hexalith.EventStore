using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

internal static partial class ReadModelPendingBatchRecovery
{
    /// <summary>Finishes only a committed unguarded legacy decision using its original linked envelopes.</summary>
    private static async Task<ReadModelPendingBatchRecoveryResult> CompactLegacyCommittedAsync(
        IReadModelBatchStateAccessor accessor, ReadModelBatchOptions options, ReadModelBatchScope expected,
        ReadModelBatchMarker marker, RawStateEntry markerRow, string discoveredKey, ReadModelBatchEnvelope? discovered,
        bool knownMarker, IReadModelBatchFaultInjector? injector, CancellationToken token)
    {
        ReadModelBatchScope scope = expected with { BatchId = marker.BatchId };
        using JsonDocument originalMarker = JsonDocument.Parse(markerRow.Value);
        string[] fields = ["v", "scope", "batch", "fp", "st", "ops", "time"];
        if (originalMarker.RootElement.EnumerateObject().Any(property => !fields.Contains(property.Name, StringComparer.Ordinal))
            || !originalMarker.RootElement.TryGetProperty("ops", out JsonElement descriptors) || descriptors.ValueKind != JsonValueKind.Array
            || descriptors.EnumerateArray().Any(descriptor => descriptor.ValueKind != JsonValueKind.Object
                || descriptor.EnumerateObject().Count() != 3
                || descriptor.EnumerateObject().Any(property => property.Name is not ("ord" or "key" or "del"))))
            return new(false, "invalid-recovery-evidence");
        if (marker.Version != 1 || !marker.Fingerprint.StartsWith("v1:", StringComparison.Ordinal)
            || marker.OwnerSelection is not null || marker.OwnerSelections is not null || marker.RetainedParticipantCount != 0
            || marker.OriginalManifest is not null || marker.OriginalManifestKey is not null
            || marker.Operations is not { Count: > 0 } operations || operations.Count > Math.Min(options.MaxOperations, ReadModelBatchOptions.DefaultMaxOperations)
            || operations.Where((operation, ordinal) => operation is null || operation.Ordinal != ordinal
                || string.IsNullOrWhiteSpace(operation.Key) || Encoding.UTF8.GetByteCount(operation.Key) > Math.Min(options.MaxKeyByteLength, ReadModelBatchOptions.DefaultMaxKeyByteLength)
                || operation.Key == scope.ComputeMarkerKey()).Any()
            || operations.Select(operation => operation.Key).Distinct(StringComparer.Ordinal).Count() != operations.Count
            || !knownMarker && (discovered is null || discovered.Ordinal >= operations.Count
                || operations[discovered.Ordinal].Key != discoveredKey || operations[discovered.Ordinal].IsDelete != discovered.IsDelete))
            return new(false, "recovery-manifest-unavailable");

        // Validate every still-owned participant before any mutation. Compacted and superseding
        // values carry no recoverable payload and are never rewritten by this historical decision.
        var pending = new List<(string Key, RawStateEntry Row, ReadModelBatchEnvelope Envelope)>();
        foreach (ReadModelBatchMarkerOperation operation in operations)
        {
            RawStateEntry row = await accessor.ReadAsync(operation.Key, token).ConfigureAwait(false);
            if (!row.Exists || !ReadModelBatchEnvelope.IsEnvelope(row.Value.Span)) continue;
            if (row.Value.Length > options.MaxCanonicalManifestBytes * 3L) return new(false, "invalid-recovery-evidence");
            ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(row.Value);
            if (envelope is null) return new(false, "invalid-recovery-evidence");
            if (envelope.ScopeHash != marker.ScopeHash) continue;
            if (envelope.Fingerprint != marker.Fingerprint || envelope.Ordinal != operation.Ordinal
                || envelope.IsDelete != operation.IsDelete || envelope.EnvelopeVersion != 1 || envelope.ExpiresAtUtc is not null
                || envelope.PreviousBase64 is not null && ReadModelBatchExpiringValue.IsExpiring(envelope.PreviousBytes().Span))
                return new(false, "invalid-recovery-evidence");
            pending.Add((operation.Key, row, envelope));
        }

        ReadModelBatchRecoveryAnchor? anchor = null;
        for (int index = 0; index < pending.Count; index++)
        {
            (string key, RawStateEntry row, ReadModelBatchEnvelope envelope) = pending[index];
            if (index == pending.Count - 1)
            {
                anchor = new(scope, key, envelope.Ordinal, envelope.IsDelete,
                    ReadModelBatchRecoveryAnchor.Digest(envelope.CandidateBytes()),
                    envelope.PreviousBase64 is null ? null : ReadModelBatchRecoveryAnchor.Digest(envelope.PreviousBytes()), null);
                continue;
            }
            if (injector is not null) await injector.InjectAsync(ReadModelBatchPhase.BeforeCompaction, envelope.Ordinal, token).ConfigureAwait(false);
            bool compacted = envelope.IsDelete
                ? await accessor.TryDeleteAsync(key, row.ETag, token).ConfigureAwait(false)
                : await accessor.TryWriteAsync(key, envelope.CandidateBytes(), row.ETag, token).ConfigureAwait(false);
            if (!compacted)
            {
                RawStateEntry after = await accessor.ReadAsync(key, token).ConfigureAwait(false);
                if (after.Exists && after.Value.Span.SequenceEqual(row.Value.Span)) return new(false, "recovery-incomplete");
            }
            if (injector is not null) await injector.InjectAsync(ReadModelBatchPhase.AfterCompaction, envelope.Ordinal, token).ConfigureAwait(false);
        }

        var receipt = new ReadModelBatchMarker
        {
            ScopeHash = marker.ScopeHash, BatchId = marker.BatchId, Fingerprint = marker.Fingerprint,
            Status = ReadModelBatchMarkerStatus.Completed, RecoveryAnchor = anchor,
            TerminalTimeUtc = options.TimeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        if (!await accessor.TryWriteAsync(scope.ComputeMarkerKey(), ReadModelBatchCanonicalJson.Serialize(receipt), markerRow.ETag, token).ConfigureAwait(false))
            return new(false, "recovery-incomplete");
        return anchor is null || await ReadModelBatchProtocol.FinishRecoveryAnchorAsync(accessor, receipt, expected, injector, token, options.TimeProvider).ConfigureAwait(false)
            ? new(true, WasRecovered: pending.Count > 0) : new(false, "recovery-incomplete");
    }
}
