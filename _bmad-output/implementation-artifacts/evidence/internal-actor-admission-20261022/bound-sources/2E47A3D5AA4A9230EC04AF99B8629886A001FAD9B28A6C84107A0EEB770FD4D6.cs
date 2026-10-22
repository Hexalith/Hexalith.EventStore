namespace Hexalith.EventStore.Client.Projections;

internal sealed partial class ReadModelBatchProtocol
{
    private ReadModelBatchRecoveryAnchor CreateRecoveryAnchor(int ordinal, ReadModelBatchEnvelope envelope)
        => new(_batch.Scope, _batch.Operations[ordinal].Key, ordinal, envelope.IsDelete,
            ReadModelBatchRecoveryAnchor.Digest(envelope.CandidateBytes()),
            envelope.PreviousBase64 is null ? null : ReadModelBatchRecoveryAnchor.Digest(envelope.PreviousBytes()),
            _batch.Operations[ordinal].TimeToLive, _batch.Operations[ordinal].ExpiresAtUtc);

    /// <summary>Reclaims terminal payload before removing the last discoverable own envelope, preserving later writers.</summary>
    internal static async Task<bool> FinishRecoveryAnchorAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchMarker marker, ReadModelBatchScope expected, IReadModelBatchFaultInjector? injector, CancellationToken token, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        ReadModelBatchRecoveryAnchor? anchor = marker.RecoveryAnchor;
        if (anchor is null || !anchor.Matches(marker, expected)) return false;
        RawStateEntry current = await accessor.ReadAsync(anchor.Key, token).ConfigureAwait(false);
        ReadModelBatchEnvelope? envelope = current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)
            ? ReadModelBatchEnvelope.FromBytes(current.Value) : null;
        if (current.Exists && ReadModelBatchEnvelope.IsEnvelope(current.Value.Span)
            && (envelope is null || !anchor.Matches(marker, envelope))) return false;
        if (injector is not null) await injector.InjectAsync(ReadModelBatchPhase.RecoveryReceiptWritten, anchor.Ordinal, token).ConfigureAwait(false);
        if (!await ReclaimOriginalManifestAsync(accessor, marker.ScopeHash, marker.Fingerprint, token).ConfigureAwait(false)) return false;
        if (injector is not null) await injector.InjectAsync(ReadModelBatchPhase.RecoveryManifestReclaimed, anchor.Ordinal, token).ConfigureAwait(false);
        if (envelope is null) return true; // already compacted or superseded; never reclaim a later writer
        bool restore = marker.Status == ReadModelBatchMarkerStatus.Aborted;
        TimeSpan? retention = ReadModelBatchExpiringValue.Retention(anchor.ExpiresAtUtc, anchor.TimeToLive, clock);
        bool absent = restore ? envelope.PreviousBase64 is null
            : envelope.IsDelete || retention <= TimeSpan.Zero;
        ReadOnlyMemory<byte> bytes = restore ? envelope.PreviousBytes()
            : anchor.ExpiresAtUtc is { } deadline ? ReadModelBatchExpiringValue.Encode(envelope.CandidateBytes(), deadline)
            : envelope.CandidateBytes();
        bool saved = absent
            ? await accessor.TryDeleteAsync(anchor.Key, current.ETag, token).ConfigureAwait(false)
            : restore
                ? await ReadModelBatchExpiringValue.RestoreAsync(accessor, anchor.Key, bytes, current.ETag, clock, token).ConfigureAwait(false)
                : await accessor.TryWriteAsync(anchor.Key, bytes, current.ETag,
                    retention, token).ConfigureAwait(false);
        if (injector is not null) await injector.InjectAsync(ReadModelBatchPhase.RecoveryAnchorCompacted, anchor.Ordinal, token).ConfigureAwait(false);
        if (saved) return true;
        RawStateEntry after = await accessor.ReadAsync(anchor.Key, token).ConfigureAwait(false);
        return !after.Exists || !ReadModelBatchEnvelope.IsEnvelope(after.Value.Span);
    }
}
