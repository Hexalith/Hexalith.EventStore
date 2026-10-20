using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Compensates fully installed legacy scratch batches while retaining their original evidence across interruptions.</summary>
internal static class ReadModelLegacyScratchRecovery
{
    /// <summary>Restores only the fully validated original uncommitted two-key scratch shape and unchanged Building fence.</summary>
    internal static async Task<ReadModelPendingBatchRecoveryResult?> TryCompensateAsync(
        IReadModelBatchStateAccessor accessor, ReadModelBatchOptions options, ReadModelBatchScope expected,
        ReadModelBatchMarker marker, string key, CancellationToken token)
    {
        string evidenceKey = ReadModelBatchKeys.ScratchCompensationKey(marker.ScopeHash);
        bool resumed = marker.ScratchCompensationKey is not null;
        if (marker.Status is not (ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Aborting)
                && !(resumed && marker.Status == ReadModelBatchMarkerStatus.Aborted)
            || marker.OwnerSelection is not null || marker.OwnerSelections is not null || marker.RecoveryAnchor is not null
            || marker.OriginalManifest is not null || marker.OriginalManifestKey is not null
            || marker.Operations is not { Count: 2 } operations
            || expected.AggregateId != expected.ProjectionType || !ReadModelBatchFingerprint.IsCanonical(marker.Fingerprint)
            || !marker.Fingerprint.StartsWith("v1:", StringComparison.Ordinal)
            || (expected with { BatchId = marker.BatchId }).ComputeScopeHash() != marker.ScopeHash
            || resumed && marker.ScratchCompensationKey != evidenceKey
            || operations[0].Ordinal != 0 || operations[1].Ordinal != 1 || operations.Any(static operation => operation.IsDelete)
            || operations[0].Key == operations[1].Key || !operations.Any(operation => operation.Key == key)) return null;

        var entries = new RawStateEntry[2];
        var envelopes = new ReadModelBatchEnvelope[2];
        ReadModelLegacyScratchCompensation? evidence = null;
        if (resumed)
        {
            RawStateEntry retained = await accessor.ReadAsync(evidenceKey, token).ConfigureAwait(false);
            if (!retained.Exists || retained.Value.Length > options.MaxCanonicalManifestBytes) return new(false, "invalid-recovery-evidence");
            evidence = JsonSerializer.Deserialize<ReadModelLegacyScratchCompensation>(retained.Value.Span, JsonSerializerOptions.Web);
            if (evidence is null || !ReadModelBatchCanonicalJson.Serialize(evidence).Span.SequenceEqual(retained.Value.Span)
                || evidence.Scope != expected with { BatchId = marker.BatchId }
                || evidence.Fingerprint != marker.Fingerprint || evidence.Envelopes is not { Length: 2 })
                return new(false, "invalid-recovery-evidence");
        }
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            entries[ordinal] = await accessor.ReadAsync(operations[ordinal].Key, token).ConfigureAwait(false);
            ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(
                evidence is null ? entries[ordinal].Value : evidence.Envelopes[ordinal]);
            if (envelope is not { EnvelopeVersion: 1, IsDelete: false } || envelope.Ordinal != ordinal
                || envelope.ScopeHash != marker.ScopeHash || envelope.Fingerprint != marker.Fingerprint) return null;
            envelopes[ordinal] = envelope;
            if (evidence is not null && !IsOriginalOrRestored(entries[ordinal], envelope, options.TimeProvider))
                return new(false, "legacy-scratch-recovery-race");
        }
        ReadModelBatchEnvelope fence = envelopes[1];
        if (fence.PreviousBase64 is null || !ReadModelBatchCanonicalJson.Canonicalize(fence.PreviousBytes()).AsSpan().SequenceEqual(fence.CandidateBytes().Span)) return null;
        SharedProjectionEpochState? state = JsonSerializer.Deserialize<SharedProjectionEpochState>(fence.CandidateBytes().Span, JsonSerializerOptions.Web);
        if (state is not { Version: 1, Phase: SharedProjectionEpochPhase.Building } || state.Epoch <= state.ActiveGeneration
            || string.IsNullOrWhiteSpace(state.OperationId) || state.RequiredWriters is not { Length: > 0 }) return null;
        var scope = new SharedProjectionScope(expected.StoreName, expected.TenantId, expected.Domain, expected.ProjectionType, state.RequiredWriters);
        scope.Validate();
        if (scope.StateKey != operations[1].Key) return null;

        // This is compensation evidence, not a synthesized v1 manifest. Keep it in the original batch's
        // private immutable namespace before destroying either of the two validated original copies.
        if (!resumed)
        {
            evidence = new(expected with { BatchId = marker.BatchId }, marker.Fingerprint,
                entries.Select(static entry => entry.Value.ToArray()).ToArray());
            ReadOnlyMemory<byte> bytes = ReadModelBatchCanonicalJson.Serialize(evidence);
            if (bytes.Length > options.MaxCanonicalManifestBytes) return new(false, "legacy-scratch-evidence-too-large");
            if (!await accessor.TryWriteAsync(evidenceKey, bytes, string.Empty, token).ConfigureAwait(false))
            {
                RawStateEntry existing = await accessor.ReadAsync(evidenceKey, token).ConfigureAwait(false);
                if (!existing.Exists || !existing.Value.Span.SequenceEqual(bytes.Span)) return new(false, "invalid-recovery-evidence");
            }
        }
        string markerKey = ReadModelBatchKeys.MarkerKey(marker.ScopeHash);
        RawStateEntry markerEntry = await accessor.ReadAsync(markerKey, token).ConfigureAwait(false);
        ReadModelBatchMarker? current = markerEntry.Exists ? ReadModelBatchMarker.ReadProof(markerEntry.Value) : null;
        if (current is null || !MatchesOriginalMarker(current, marker)
            || current.Status != marker.Status
            || current.ScratchCompensationKey != marker.ScratchCompensationKey)
            return new(false, "legacy-scratch-recovery-race");
        if (!resumed)
        {
            current.Status = ReadModelBatchMarkerStatus.Aborting;
            current.ScratchCompensationKey = evidenceKey;
            if (!await accessor.TryWriteAsync(markerKey, ReadModelBatchCanonicalJson.Serialize(current), markerEntry.ETag, token).ConfigureAwait(false))
                return new(false, "legacy-scratch-recovery-race");
        }
        // Keep the discoverable own fence until all private compensation has settled. A replacement
        // coordinator must reconcile that fence before advancing the epoch. Every write retains CAS.
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            if (ordinal == 1)
            {
                // Seal the terminal compensation decision before removing the last discoverable copy.
                markerEntry = await accessor.ReadAsync(markerKey, token).ConfigureAwait(false);
                current = markerEntry.Exists ? ReadModelBatchMarker.ReadProof(markerEntry.Value) : null;
                if (current is null || !MatchesOriginalMarker(current, marker) || current.ScratchCompensationKey != evidenceKey
                    || current.Status is not (ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted))
                    return new(false, "legacy-scratch-recovery-race");
                if (current.Status != ReadModelBatchMarkerStatus.Aborted)
                {
                    current.Status = ReadModelBatchMarkerStatus.Aborted;
                    if (!await accessor.TryWriteAsync(markerKey, ReadModelBatchCanonicalJson.Serialize(current), markerEntry.ETag, token).ConfigureAwait(false))
                        return new(false, "legacy-scratch-recovery-race");
                }
            }
            RawStateEntry entry = await accessor.ReadAsync(operations[ordinal].Key, token).ConfigureAwait(false);
            if (!IsOriginalOrRestored(entry, envelopes[ordinal], options.TimeProvider))
                return new(false, "legacy-scratch-recovery-race");
            if (!entry.Exists || !entry.Value.Span.SequenceEqual(envelopes[ordinal].ToBytes().Span)) continue;
            bool restored = envelopes[ordinal].PreviousBase64 is null
                ? await accessor.TryDeleteAsync(operations[ordinal].Key, entry.ETag, token).ConfigureAwait(false)
                : await ReadModelBatchExpiringValue.RestoreAsync(accessor, operations[ordinal].Key,
                    envelopes[ordinal].PreviousBytes(), entry.ETag, options.TimeProvider, token).ConfigureAwait(false);
            if (!restored) return new(false, "legacy-scratch-recovery-race");
        }
        markerEntry = await accessor.ReadAsync(markerKey, token).ConfigureAwait(false);
        current = markerEntry.Exists ? ReadModelBatchMarker.ReadProof(markerEntry.Value) : null;
        if (current is null || !MatchesOriginalMarker(current, marker) || current.ScratchCompensationKey != evidenceKey
            || current.Status is not (ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted))
            return new(false, "legacy-scratch-recovery-race");
        RawStateEntry retainedEvidence = await accessor.ReadAsync(evidenceKey, token).ConfigureAwait(false);
        if (retainedEvidence.Exists && !await accessor.TryDeleteAsync(evidenceKey, retainedEvidence.ETag, token).ConfigureAwait(false))
            return new(false, "legacy-scratch-recovery-race");
        return new(true, WasRecovered: true);
    }

    private static bool MatchesOriginalMarker(ReadModelBatchMarker current, ReadModelBatchMarker original)
        => current.Version == 1 && current.ScopeHash == original.ScopeHash && current.BatchId == original.BatchId
            && current.Fingerprint == original.Fingerprint && current.OwnerSelection is null && current.OwnerSelections is null
            && current.OriginalManifest is null && current.OriginalManifestKey is null && current.RecoveryAnchor is null
            && current.Operations is { Count: 2 } operations && original.Operations is { Count: 2 } prior
            && operations.Select((item, ordinal) => item.Ordinal == prior[ordinal].Ordinal
                && item.Key == prior[ordinal].Key && item.IsDelete == prior[ordinal].IsDelete).All(static matches => matches);

    private static bool IsOriginalOrRestored(RawStateEntry current, ReadModelBatchEnvelope original, TimeProvider clock)
        => current.Exists && current.Value.Span.SequenceEqual(original.ToBytes().Span)
            || (original.PreviousBase64 is null ? !current.Exists
            : current.Exists && current.Value.Span.SequenceEqual(original.PreviousBytes().Span)
                || !current.Exists && ReadModelBatchExpiringValue.IsExpiring(original.PreviousBytes().Span)
                    && ReadModelBatchExpiringValue.Decode(original.PreviousBytes()).Deadline <= clock.GetUtcNow());
}
