using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

internal static partial class ReadModelPendingBatchRecovery
{
    private static async Task<ReadModelPendingBatchRecoveryResult> ReclaimMarkerlessManifestAsync(
        IReadModelBatchStateAccessor accessor, ReadModelBatchOptions options,
        ReadModelBatchScope expected, CancellationToken token)
    {
        string manifestKey = ReadModelBatchKeys.ManifestKey(expected.ComputeScopeHash());
        RawStateEntry original = await accessor.ReadAsync(manifestKey, token).ConfigureAwait(false);
        RawStateEntry index = await accessor.ReadAsync(expected.ComputeMarkerKey() + ":retired", token).ConfigureAwait(false);
        if (index.Exists)
        {
            if (index.Value.Length > ReadModelBatchParticipantProof.MaxEncodedBytes) return new(false, "invalid-recovery-evidence");
            ReadModelBatchTerminalFence? retired = JsonSerializer.Deserialize<ReadModelBatchTerminalFence>(index.Value.Span, JsonSerializerOptions.Web);
            if (retired is null || retired.Version != 2 || retired.Scope != expected
                || !ReadModelBatchCanonicalJson.Serialize(retired).Span.SequenceEqual(index.Value.Span)
                || await ReadModelBatchTerminalFence.ReadAsync(accessor, expected, retired.Fingerprint, token).ConfigureAwait(false) is not { } authoritative
                || !ReadModelBatchCanonicalJson.Serialize(authoritative).Span.SequenceEqual(index.Value.Span))
                return new(false, "invalid-recovery-evidence");
            bool recovered = false;
            for (int ordinal = 0; ordinal < retired.ParticipantCount; ordinal++)
            {
                RawStateEntry proofRow = await accessor.ReadAsync(ReadModelBatchParticipantProof.Key(expected, retired.Fingerprint, ordinal), token).ConfigureAwait(false);
                if (!proofRow.Exists) continue; // No installer reached this original ordinal.
                if (proofRow.Value.Length > ReadModelBatchParticipantProof.MaxEncodedBytes) return new(false, "invalid-recovery-evidence");
                ReadModelBatchParticipantProof? proof = JsonSerializer.Deserialize<ReadModelBatchParticipantProof>(proofRow.Value.Span, JsonSerializerOptions.Web);
                if (proof?.Anchor is null || proof.Scope != expected || proof.Fingerprint != retired.Fingerprint
                    || proof.Anchor.Ordinal != ordinal || !ReadModelBatchCanonicalJson.Serialize(proof).Span.SequenceEqual(proofRow.Value.Span))
                    return new(false, "invalid-recovery-evidence");
                RawStateEntry participant = await accessor.ReadAsync(proof.Anchor.Key, token).ConfigureAwait(false);
                ReadModelBatchEnvelope? envelope = participant.Exists && ReadModelBatchEnvelope.IsEnvelope(participant.Value.Span)
                    ? ReadModelBatchEnvelope.FromBytes(participant.Value) : null;
                if (participant.Exists && ReadModelBatchEnvelope.IsEnvelope(participant.Value.Span) && envelope is null)
                    return new(false, "invalid-recovery-evidence");
                if (envelope is null || envelope.ScopeHash != expected.ComputeScopeHash() || envelope.Fingerprint != retired.Fingerprint) continue;
                if (!await retired.RestoreAsync(accessor, proof.Anchor.Key, participant, envelope, options.TimeProvider, token).ConfigureAwait(false))
                    return new(false, "recovery-incomplete");
                recovered = true;
            }
            return await ReadModelBatchProtocol.ReclaimOriginalManifestAsync(accessor, expected.ComputeScopeHash(), retired.Fingerprint, token).ConfigureAwait(false)
                ? new(true, WasRecovered: recovered) : new(false, "recovery-incomplete");
        }
        if (!original.Exists) return new(true);
        if (original.Value.Length > Math.Min(options.MaxCanonicalManifestBytes, ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes)) return new(false, "invalid-recovery-evidence");
        using JsonDocument document = JsonDocument.Parse(original.Value);
        if (!document.RootElement.TryGetProperty("recoverable", out JsonElement recoverable)
            || recoverable.ValueKind != JsonValueKind.True) return new(false, "invalid-recovery-evidence");
        string fingerprint = ReadModelBatchFingerprint.ComputeFromManifest(original.Value.Span);
        ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(original.Value, expected, null, null, fingerprint, retainManifestForRecovery: true);
        if (batch.Operations.Count > Math.Min(options.MaxOperations, ReadModelBatchOptions.DefaultMaxOperations)
            || batch.Operations.Any(operation => System.Text.Encoding.UTF8.GetByteCount(operation.Key)
                > Math.Min(options.MaxKeyByteLength, ReadModelBatchOptions.DefaultMaxKeyByteLength))
            || !ReadModelBatchFingerprint.BuildCanonicalManifest(batch).AsSpan().SequenceEqual(original.Value.Span))
            return new(false, "invalid-recovery-evidence");
        foreach (ReadModelBatchOperation operation in batch.Operations)
        {
            RawStateEntry participant = await accessor.ReadAsync(operation.Key, token).ConfigureAwait(false);
            if (!participant.Exists || !ReadModelBatchEnvelope.IsEnvelope(participant.Value.Span)) continue;
            ReadModelBatchEnvelope? envelope = ReadModelBatchEnvelope.FromBytes(participant.Value);
            if (envelope is null || envelope.ScopeHash == expected.ComputeScopeHash())
                return new(false, "invalid-recovery-evidence");
        }
        // The original immutable manifest proves the identity, but never a commit decision. Claim
        // original absence with CAS before sealing retirement; a racing real marker wins unchanged.
        var aborted = new ReadModelBatchMarker { ScopeHash = expected.ComputeScopeHash(), BatchId = expected.BatchId,
            Fingerprint = fingerprint, Status = ReadModelBatchMarkerStatus.Aborted, RetainedParticipantCount = batch.Operations.Count };
        if (!await accessor.TryWriteAsync(expected.ComputeMarkerKey(), ReadModelBatchCanonicalJson.Serialize(aborted), string.Empty, token).ConfigureAwait(false))
            return new(false, "recovery-marker-race");
        var fence = new ReadModelBatchTerminalFence(expected, fingerprint, [], 2, ReadModelBatchMarkerStatus.Aborted, batch.Operations.Count);
        if (!await fence.SealAsync(accessor, token).ConfigureAwait(false)) return new(false, "recovery-incomplete");
        return await ReadModelBatchProtocol.ReclaimOriginalManifestAsync(accessor, expected.ComputeScopeHash(), fingerprint, token).ConfigureAwait(false)
            ? new(true, WasRecovered: true) : new(false, "recovery-incomplete");
    }
}
