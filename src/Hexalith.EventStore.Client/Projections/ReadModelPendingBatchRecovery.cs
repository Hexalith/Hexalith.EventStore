using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Uses existing envelopes and immutable manifest bytes to rediscover an interrupted conditional batch.</summary>
internal static partial class ReadModelPendingBatchRecovery
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
            // A caller owning the deterministic batch identity can recover preparation before the
            // first participant exists. The exact marker key binds its batch ID; logical-key discovery
            // continues to obtain that identity only from the original participant envelope.
            bool knownMarker = key == expected.ComputeMarkerKey();
            RawStateEntry current = await accessor.ReadAsync(key, token).ConfigureAwait(false);
            if (!current.Exists) return knownMarker
                ? await ReclaimMarkerlessManifestAsync(accessor, options, expected, token).ConfigureAwait(false) : new(true);
            ReadModelBatchEnvelope? envelope = null;
            if (!knownMarker)
            {
                if (!ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)) return new(true);
                envelope = ReadModelBatchEnvelope.FromBytes(current.Value);
                if (envelope is null || envelope.EnvelopeVersion is not (1 or 2) || !ReadModelBatchFingerprint.IsCanonical(envelope.Fingerprint)
                    || envelope.ScopeHash is not { Length: 43 } || envelope.ScopeHash.Any(static ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_'))
                    return new(false, "invalid-recovery-evidence");
            }
            RawStateEntry row = knownMarker ? current
                : await accessor.ReadAsync(ReadModelBatchKeys.MarkerKey(envelope!.ScopeHash), token).ConfigureAwait(false);
            if (!knownMarker && (!row.Exists || row.Value.Length > 16_384 || ReadModelBatchMarker.ReadProof(row.Value)?.Fingerprint != envelope!.Fingerprint))
            {
                ReadModelBatchTerminalFence? retired = await ReadModelBatchTerminalFence.ReadForEnvelopeAsync(accessor, expected, envelope!, token).ConfigureAwait(false);
                return retired is not null && await retired.RestoreAsync(accessor, key, current, envelope!, options.TimeProvider, token).ConfigureAwait(false)
                    ? new(true, WasRecovered: true) : new(false, "invalid-recovery-evidence");
            }
            if (!row.Exists || row.Value.Length > 16_384) return new(false, "invalid-recovery-evidence");
            ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadProof(row.Value);
            if (marker is null || marker.Status is not (ReadModelBatchMarkerStatus.Prepared or ReadModelBatchMarkerStatus.Committed
                    or ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted or ReadModelBatchMarkerStatus.Completed)
                || !ReadModelBatchFingerprint.IsCanonical(marker.Fingerprint)
                || (knownMarker ? marker.ScopeHash != expected.ComputeScopeHash() || marker.BatchId != expected.BatchId
                    : marker.ScopeHash != envelope!.ScopeHash || marker.Fingerprint != envelope.Fingerprint)
                || marker.OriginalManifest is not null && marker.OriginalManifestKey is not null)
                return new(false, "invalid-recovery-evidence");
            // Validate existing terminal anchor evidence before any auxiliary proof write.
            if (marker.RecoveryAnchor is { } originalAnchor
                && (marker.OriginalManifest is not null || marker.OriginalManifestKey is not null || marker.Operations is not null
                    || !originalAnchor.Matches(marker, expected)
                    || marker.RetainedParticipantCount > 0
                        && !await ReadModelBatchParticipantProof.MatchesAnchorAsync(accessor, expected with { BatchId = marker.BatchId },
                            marker.Fingerprint, originalAnchor, token).ConfigureAwait(false)
                    || marker.RetainedParticipantCount == 0 && !knownMarker
                        && (originalAnchor.Key != key || !originalAnchor.Matches(marker, envelope!))))
                return new(false, "invalid-recovery-evidence");
            if (marker.RetainedParticipantCount > 0)
            {
                var terminalScope = expected with { BatchId = marker.BatchId };
                var terminalFence = new ReadModelBatchTerminalFence(terminalScope, marker.Fingerprint, [], 2,
                    marker.Status, marker.RetainedParticipantCount);
                if (marker.Status is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
                    || !await terminalFence.SealAsync(accessor, token).ConfigureAwait(false)) return new(false, "recovery-incomplete");
            }
            if (!knownMarker && marker.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
            {
                ReadModelBatchTerminalFence? retired = await ReadModelBatchTerminalFence.ReadForEnvelopeAsync(accessor, expected, envelope!, token).ConfigureAwait(false);
                if (retired is not null && marker.RecoveryAnchor?.Key != key)
                    return await retired.RestoreAsync(accessor, key, current, envelope!, options.TimeProvider, token).ConfigureAwait(false)
                        ? new(true, WasRecovered: true) : new(false, "recovery-incomplete");
            }
            if (marker.RecoveryAnchor is { } anchor)
            {
                if (marker.OriginalManifest is not null || marker.OriginalManifestKey is not null || marker.Operations is not null
                    || !knownMarker && (anchor.Key != key || !anchor.Matches(marker, envelope!)) || !anchor.Matches(marker, expected))
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
            if (manifest.IsEmpty)
            {
                // A terminal receipt needs no replay. Legacy compensation keeps its original key
                // list and provenance until the owning private protocol conditionally reclaims it.
                if (knownMarker && marker.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted
                    && marker.ScratchCompensationKey is null)
                {
                    if (marker.Status == ReadModelBatchMarkerStatus.Aborted && marker.RetainedParticipantCount == 0
                        && !await new ReadModelBatchTerminalFence(expected, marker.Fingerprint, []).SealAsync(accessor, token).ConfigureAwait(false))
                        return new(false, "recovery-incomplete");
                    return await ReadModelBatchProtocol.ReclaimOriginalManifestAsync(accessor, marker.ScopeHash,
                        marker.Fingerprint, token).ConfigureAwait(false) ? new(true) : new(false, "recovery-incomplete");
                }
                string participantKey = knownMarker && marker.Operations is { Count: > 0 } participants
                    ? participants[0].Key : key;
                if (knownMarker && marker.Status == ReadModelBatchMarkerStatus.Aborted && marker.ScratchCompensationKey is not null)
                {
                    RawStateEntry retained = await accessor.ReadAsync(marker.ScratchCompensationKey, token).ConfigureAwait(false);
                    ReadModelBatchScope originalScope = expected with { BatchId = marker.BatchId };
                    ReadModelBatchTerminalFence? retired = await ReadModelBatchTerminalFence.ReadAsync(accessor, originalScope, marker.Fingerprint, token).ConfigureAwait(false);
                    if (retired is not { Anchors.Length: 2 } || marker.Operations is not { Count: 2 }
                        || retired.Anchors.Where((proof, ordinal) => marker.Operations[ordinal].Key != proof.Key
                            || marker.Operations[ordinal].Ordinal != ordinal || marker.Operations[ordinal].IsDelete != proof.IsDelete).Any())
                        return new(false, "invalid-recovery-evidence");
                    if (retained.Exists)
                    {
                        if (retained.Value.Length > ReadModelLegacyScratchCompensation.MaxEncodedBytes) return new(false, "invalid-recovery-evidence");
                        ReadModelLegacyScratchCompensation? evidence = JsonSerializer.Deserialize<ReadModelLegacyScratchCompensation>(retained.Value.Span, JsonSerializerOptions.Web);
                        if (evidence is null || evidence.Scope != originalScope || evidence.Fingerprint != marker.Fingerprint
                            || evidence.Envelopes is not { Length: 2 }
                            || !ReadModelBatchCanonicalJson.Serialize(evidence).Span.SequenceEqual(retained.Value.Span))
                            return new(false, "invalid-recovery-evidence");
                        for (int ordinal = 0; ordinal < 2; ordinal++)
                        {
                            ReadModelBatchEnvelope? original = ReadModelBatchEnvelope.FromBytes(evidence.Envelopes[ordinal]);
                            if (original is null || !retired.Anchors[ordinal].Matches(marker, original)) return new(false, "invalid-recovery-evidence");
                        }
                    }
                    bool recovered = false;
                    foreach (ReadModelBatchRecoveryAnchor proof in retired.Anchors)
                    {
                        RawStateEntry participant = await accessor.ReadAsync(proof.Key, token).ConfigureAwait(false);
                        ReadModelBatchEnvelope? original = participant.Exists && ReadModelBatchEnvelope.IsEnvelope(participant.Value.Span)
                            ? ReadModelBatchEnvelope.FromBytes(participant.Value) : null;
                        if (original?.ScopeHash != marker.ScopeHash || original.Fingerprint != marker.Fingerprint) continue;
                        if (!await retired.RestoreAsync(accessor, proof.Key, participant, original, options.TimeProvider, token).ConfigureAwait(false))
                            return new(false, "recovery-incomplete");
                        recovered = true;
                    }
                    return new(true, WasRecovered: recovered);
                }
                return await ReadModelLegacyScratchRecovery.TryCompensateAsync(accessor, options, expected, marker, participantKey, token).ConfigureAwait(false)
                    ?? new(false, "recovery-manifest-unavailable");
            }
            if (manifest.Length > options.MaxCanonicalManifestBytes) return new(false, "recovery-manifest-unavailable");
            using JsonDocument document = JsonDocument.Parse(manifest);
            if (!document.RootElement.TryGetProperty("recoverable", out JsonElement optedIn) || !optedIn.GetBoolean())
                return new(false, "recovery-manifest-unavailable");
            JsonElement source = document.RootElement.GetProperty("scope");
            var scope = new ReadModelBatchScope(source.GetProperty("store").GetString()!, source.GetProperty("tenant").GetString()!,
                source.GetProperty("domain").GetString()!, source.GetProperty("aggregate").GetString()!,
                source.GetProperty("projection").GetString()!, source.GetProperty("batch").GetString()!);
            if (scope.StoreName != expected.StoreName || scope.TenantId != expected.TenantId || scope.Domain != expected.Domain
                || scope.AggregateId != expected.AggregateId || scope.ProjectionType != expected.ProjectionType
                || scope.BatchId != marker.BatchId || scope.ComputeScopeHash() != marker.ScopeHash)
                return new(false, "recovery-scope-mismatch");
            ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(manifest, scope, marker.OwnerSelection,
                marker.OwnerSelections, marker.Fingerprint, retainManifestForRecovery: true);
            if (marker.Operations is null || marker.Operations.Count != batch.Operations.Count
                || marker.Operations.Where((operation, index) => operation.Ordinal != index || operation.Key != batch.Operations[index].Key
                    || operation.IsDelete != (batch.Operations[index].Kind == ReadModelBatchOperationKind.Delete)).Any()
                || !knownMarker && (envelope!.Ordinal < 0 || envelope.Ordinal >= batch.Operations.Count
                || batch.Operations[envelope.Ordinal].Key != key
                || envelope.ExpiresAtUtc != batch.Operations[envelope.Ordinal].ExpiresAtUtc
                || envelope.IsDelete != (batch.Operations[envelope.Ordinal].Kind == ReadModelBatchOperationKind.Delete)
                || !envelope.CandidateBytes().Span.SequenceEqual(batch.Operations[envelope.Ordinal].CanonicalValue.Span)))
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
                        && (operation.Concurrency.ExpectedETag.Length == 0
                            ? installed.PreviousBase64 is not null && (!ReadModelBatchExpiringValue.IsExpiring(installed.PreviousBytes().Span)
                                || ReadModelBatchExpiringValue.Decode(installed.PreviousBytes()).Deadline > options.TimeProvider.GetUtcNow())
                            : installed.PreviousBase64 is null)
                    || installed.ExpiresAtUtc != operation.ExpiresAtUtc
                    || installed.IsDelete != (operation.Kind == ReadModelBatchOperationKind.Delete)
                    || !installed.CandidateBytes().Span.SequenceEqual(operation.CanonicalValue.Span))
                    return new(false, "invalid-recovery-evidence");
            }
            var protocol = new ReadModelBatchProtocol(accessor, options, logger);
            ReadModelBatchResult result = await protocol.ExecuteAsync(batch, injector, token).ConfigureAwait(false);
            if (!result.IsSuccess && result.ConflictKind != ReadModelBatchConflictKind.Optimistic)
                return new(false, "recovery-incomplete");
            if (knownMarker)
            {
                RawStateEntry settled = await accessor.ReadAsync(key, token).ConfigureAwait(false);
                ReadModelBatchMarker? terminal = settled.Exists ? ReadModelBatchMarker.ReadProof(settled.Value) : null;
                return terminal is not null && terminal.Fingerprint == marker.Fingerprint
                    && terminal.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted
                    && (terminal.RecoveryAnchor is null
                        || await ReadModelBatchProtocol.FinishRecoveryAnchorAsync(accessor, terminal, expected, injector, token, options.TimeProvider).ConfigureAwait(false))
                    ? new(true, WasRecovered: true) : new(false, "recovery-incomplete");
            }
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
