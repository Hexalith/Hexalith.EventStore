using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Payload-free immutable retirement of one private fingerprint, separate from reclaimable receipts.</summary>
/// <param name="Scope">The original exact private batch scope.</param>
/// <param name="Fingerprint">The retired immutable identity.</param>
/// <param name="Anchors">Validated original legacy participant digests, or none for pre-marker retirement.</param>
/// <param name="Version">The immutable internal proof format.</param>
internal sealed record ReadModelBatchTerminalFence(ReadModelBatchScope Scope, string Fingerprint, ReadModelBatchRecoveryAnchor[] Anchors, int Version = 1)
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
        if (row.Value.Length > 16_384) throw new JsonException("Invalid private terminal fence.");
        ReadModelBatchTerminalFence? fence = JsonSerializer.Deserialize<ReadModelBatchTerminalFence>(row.Value.Span, JsonSerializerOptions.Web);
        if (fence is null || fence.Version != 1 || fence.Scope != scope || fence.Fingerprint != fingerprint || fence.Anchors is null
            || fence.Anchors.Length is not (0 or 2)
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
        if (row.Value.Length > 16_384) throw new JsonException("Invalid private terminal fence.");
        ReadModelBatchTerminalFence? fence = JsonSerializer.Deserialize<ReadModelBatchTerminalFence>(row.Value.Span, JsonSerializerOptions.Web);
        if (fence?.Scope is null || fence.Scope with { BatchId = expected.BatchId } != expected
            || fence.Scope.ComputeScopeHash() != envelope.ScopeHash) throw new JsonException("Invalid private terminal fence scope.");
        return await ReadAsync(accessor, fence.Scope, envelope.Fingerprint, token).ConfigureAwait(false);
    }

    /// <summary>Seals the same immutable decision before discarding any original recovery evidence.</summary>
    internal async Task<bool> SealAsync(IReadModelBatchStateAccessor accessor, CancellationToken token)
    {
        ReadOnlyMemory<byte> bytes = ReadModelBatchCanonicalJson.Serialize(this);
        if (bytes.Length > 16_384) return false;
        if (await accessor.TryWriteAsync(Key(Scope.ComputeScopeHash(), Fingerprint), bytes, string.Empty, token).ConfigureAwait(false)) return true;
        ReadModelBatchTerminalFence? existing = await ReadAsync(accessor, Scope, Fingerprint, token).ConfigureAwait(false);
        return existing is not null && ReadModelBatchCanonicalJson.Serialize(existing).Span.SequenceEqual(bytes.Span);
    }

    /// <summary>Restores only a fully proved original orphan; newer rows and foreign evidence remain untouched.</summary>
    internal async Task<bool> RestoreAsync(IReadModelBatchStateAccessor accessor, string key,
        RawStateEntry row, ReadModelBatchEnvelope envelope, TimeProvider clock, CancellationToken token)
    {
        var marker = new ReadModelBatchMarker { ScopeHash = Scope.ComputeScopeHash(), BatchId = Scope.BatchId,
            Fingerprint = Fingerprint, Status = ReadModelBatchMarkerStatus.Aborted };
        ReadModelBatchRecoveryAnchor? anchor = Anchors.SingleOrDefault(item => item.Key == key);
        if (anchor is null || !anchor.Matches(marker, envelope)) return false;
        bool restored = envelope.PreviousBase64 is null
            ? await accessor.TryDeleteAsync(key, row.ETag, token).ConfigureAwait(false)
            : await ReadModelBatchExpiringValue.RestoreAsync(accessor, key, envelope.PreviousBytes(), row.ETag, clock, token).ConfigureAwait(false);
        if (restored) return true;
        RawStateEntry after = await accessor.ReadAsync(key, token).ConfigureAwait(false);
        return !after.Exists || !after.Value.Span.SequenceEqual(row.Value.Span);
    }
}
