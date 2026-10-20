using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Payload-free immutable retirement of one private fingerprint, separate from reclaimable receipts.</summary>
/// <param name="Scope">The original exact private batch scope.</param>
/// <param name="Fingerprint">The retired immutable identity.</param>
/// <param name="Anchors">Validated original legacy participant digests, or none for pre-marker retirement.</param>
/// <param name="Version">The immutable internal proof format.</param>
/// <param name="Decision">The distinct retained terminal decision, absent for historical legacy proof.</param>
/// <param name="ParticipantCount">The original bounded retained operation count.</param>
internal sealed record ReadModelBatchTerminalFence(ReadModelBatchScope Scope, string Fingerprint, ReadModelBatchRecoveryAnchor[] Anchors, int Version = 1,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReadModelBatchMarkerStatus? Decision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ParticipantCount = 0)
{
    /// <summary>Derives a separate bounded key without exposing scope or payload.</summary>
    internal static string Key(string scopeHash, string fingerprint) => ReadModelBatchKeys.MarkerKey(scopeHash)
        + ":retired:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));

    /// <summary>Reads only canonical, bounded evidence matching the requested original identity.</summary>
    internal static async Task<ReadModelBatchTerminalFence?> ReadAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchScope scope, string fingerprint, CancellationToken token)
    {
        RawStateEntry row = await accessor.ReadAsync(Key(scope.ComputeScopeHash(), fingerprint), token).ConfigureAwait(false);
        if (!row.Exists) return null;
        if (row.Value.Length > ReadModelBatchParticipantProof.MaxEncodedBytes) throw new JsonException("Invalid private terminal fence.");
        ReadModelBatchTerminalFence? fence = JsonSerializer.Deserialize<ReadModelBatchTerminalFence>(row.Value.Span, JsonSerializerOptions.Web);
        if (fence is null || fence.Version is not (1 or 2) || fence.Scope != scope || fence.Fingerprint != fingerprint || fence.Anchors is null
            || fence.Anchors.Length is not (0 or 2)
            || (fence.Version == 1 ? row.Value.Length > 16_384 || fence.Decision is not null || fence.ParticipantCount != 0
                : fence.Anchors.Length != 0 || fence.Decision is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
                    || fence.ParticipantCount is < 1 or > ReadModelBatchOptions.DefaultMaxOperations)
            || !ReadModelBatchCanonicalJson.Serialize(fence).Span.SequenceEqual(row.Value.Span))
            throw new JsonException("Invalid private terminal fence.");
        var marker = new ReadModelBatchMarker { ScopeHash = scope.ComputeScopeHash(), BatchId = scope.BatchId,
            Fingerprint = fingerprint, Status = ReadModelBatchMarkerStatus.Aborted };
        if (!ReadModelBatchFingerprint.IsCanonical(fingerprint)
            || fence.Anchors.Where((anchor, ordinal) => anchor is null || anchor.Ordinal != ordinal
                || !anchor.Matches(marker, scope)).Any()) throw new JsonException("Invalid private terminal fence.");
        return fence;
    }

    /// <summary>Finds the original scope only through its bounded canonical terminal proof.</summary>
    internal static async Task<ReadModelBatchTerminalFence?> ReadForEnvelopeAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchScope expected, ReadModelBatchEnvelope envelope, CancellationToken token)
    {
        RawStateEntry row = await accessor.ReadAsync(Key(envelope.ScopeHash, envelope.Fingerprint), token).ConfigureAwait(false);
        if (!row.Exists) return null;
        if (row.Value.Length > ReadModelBatchParticipantProof.MaxEncodedBytes) throw new JsonException("Invalid private terminal fence.");
        ReadModelBatchTerminalFence? fence = JsonSerializer.Deserialize<ReadModelBatchTerminalFence>(row.Value.Span, JsonSerializerOptions.Web);
        if (fence?.Scope is null || fence.Scope with { BatchId = expected.BatchId } != expected
            || fence.Scope.ComputeScopeHash() != envelope.ScopeHash) throw new JsonException("Invalid private terminal fence scope.");
        return await ReadAsync(accessor, fence.Scope, envelope.Fingerprint, token).ConfigureAwait(false);
    }

    /// <summary>Seals the same immutable decision before discarding any original recovery evidence.</summary>
    internal async Task<bool> SealAsync(IReadModelBatchStateAccessor accessor, CancellationToken token)
    {
        ReadOnlyMemory<byte> bytes = ReadModelBatchCanonicalJson.Serialize(this);
        if (bytes.Length > (Version == 1 ? 16_384 : ReadModelBatchParticipantProof.MaxEncodedBytes)) return false;
        bool sealedFence = await accessor.TryWriteAsync(Key(Scope.ComputeScopeHash(), Fingerprint), bytes, string.Empty, token).ConfigureAwait(false);
        if (!sealedFence)
        {
            ReadModelBatchTerminalFence? existing = await ReadAsync(accessor, Scope, Fingerprint, token).ConfigureAwait(false);
            if (existing is null || !ReadModelBatchCanonicalJson.Serialize(existing).Span.SequenceEqual(bytes.Span)) return false;
        }
        if (Version == 1) return true;
        string indexKey = Scope.ComputeMarkerKey() + ":retired";
        if (await accessor.TryWriteAsync(indexKey, bytes, string.Empty, token).ConfigureAwait(false)) return true;
        RawStateEntry index = await accessor.ReadAsync(indexKey, token).ConfigureAwait(false);
        return index.Exists && index.Value.Span.SequenceEqual(bytes.Span);
    }

    /// <summary>Restores only a fully proved original orphan; newer rows and foreign evidence remain untouched.</summary>
    internal async Task<bool> RestoreAsync(IReadModelBatchStateAccessor accessor, string key,
        RawStateEntry row, ReadModelBatchEnvelope envelope, TimeProvider clock, CancellationToken token)
    {
        var marker = new ReadModelBatchMarker { ScopeHash = Scope.ComputeScopeHash(), BatchId = Scope.BatchId,
            Fingerprint = Fingerprint, Status = ReadModelBatchMarkerStatus.Aborted };
        ReadModelBatchRecoveryAnchor? anchor = Version == 2
            ? await ReadModelBatchParticipantProof.ReadAsync(accessor, this, envelope, key, token).ConfigureAwait(false)
            : Anchors.SingleOrDefault(item => item.Key == key);
        if (anchor is null || !anchor.Matches(marker, envelope)) return false;
        if (Version == 2 && Decision == ReadModelBatchMarkerStatus.Completed)
        {
            RawStateEntry receipt = await accessor.ReadAsync(Scope.ComputeMarkerKey(), token).ConfigureAwait(false);
            ReadModelBatchMarker? current = receipt.Exists ? ReadModelBatchMarker.ReadProof(receipt.Value) : null;
            if (current?.Fingerprint == Fingerprint && current.Status == ReadModelBatchMarkerStatus.Completed)
            {
                // Keep its authenticated marker-gated output readable until the owning cleanup
                // erases it. Publishing raw candidate bytes here could race receipt reclamation and
                // lose the only discoverable identity. Markerless recovery compensates below.
                if (anchor.ExpiresAtUtc is null || anchor.ExpiresAtUtc > clock.GetUtcNow()) return true;
                if (await accessor.TryDeleteAsync(key, row.ETag, token).ConfigureAwait(false)) return true;
                RawStateEntry afterExpiry = await accessor.ReadAsync(key, token).ConfigureAwait(false);
                return !afterExpiry.Exists || !afterExpiry.Value.Span.SequenceEqual(row.Value.Span);
            }
        }
        bool restored = envelope.PreviousBase64 is null
            ? await accessor.TryDeleteAsync(key, row.ETag, token).ConfigureAwait(false)
            : await ReadModelBatchExpiringValue.RestoreAsync(accessor, key, envelope.PreviousBytes(), row.ETag, clock, token).ConfigureAwait(false);
        if (restored) return true;
        RawStateEntry after = await accessor.ReadAsync(key, token).ConfigureAwait(false);
        return !after.Exists || !after.Value.Span.SequenceEqual(row.Value.Span);
    }
}
