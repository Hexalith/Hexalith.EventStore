using System.Text.Json;
using System.Text;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Immutable payload-free proof sealed before a retained participant can be physically installed.</summary>
/// <param name="Scope">The original exact batch scope.</param>
/// <param name="Fingerprint">The original manifest identity.</param>
/// <param name="Anchor">The original candidate and previous-value digests.</param>
/// <param name="ConcurrencyMode">The original conditional guard mode.</param>
/// <param name="ExpectedETagDigest">SHA-256 binding the original conditional ETag without retaining an unbounded value.</param>
internal sealed record ReadModelBatchParticipantProof(ReadModelBatchScope Scope, string Fingerprint,
    ReadModelBatchRecoveryAnchor Anchor, ReadModelBatchConcurrencyMode ConcurrencyMode, string ExpectedETagDigest)
{
    /// <summary>Allows worst-case JSON escaping of the original bounded scope and key.</summary>
    internal const int MaxEncodedBytes = 65_536;

    /// <summary>Derives the bounded original participant proof key.</summary>
    internal static string Key(ReadModelBatchScope scope, string fingerprint, int ordinal)
        => ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fingerprint) + ":participant:" + ordinal;

    /// <summary>Seals exact original proof before the awaited installation, retaining it across receipt reclamation.</summary>
    internal static async Task<bool> SealAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope,
        string fingerprint, ReadModelBatchRecoveryAnchor anchor, ReadModelBatchConcurrency concurrency, CancellationToken token)
    {
        var proof = new ReadModelBatchParticipantProof(scope, fingerprint, anchor, concurrency.Mode,
            ReadModelBatchRecoveryAnchor.Digest(Encoding.UTF8.GetBytes(concurrency.ExpectedETag)));
        ReadOnlyMemory<byte> bytes = ReadModelBatchCanonicalJson.Serialize(proof);
        if (bytes.Length > MaxEncodedBytes) return false;
        string key = Key(scope, fingerprint, anchor.Ordinal);
        if (await accessor.TryWriteAsync(key, bytes, string.Empty, token).ConfigureAwait(false)) return true;
        RawStateEntry existing = await accessor.ReadAsync(key, token).ConfigureAwait(false);
        return existing.Exists && existing.Value.Span.SequenceEqual(bytes.Span);
    }

    /// <summary>Validates a receipt's original anchor before any auxiliary recovery mutation.</summary>
    internal static async Task<bool> MatchesAnchorAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope,
        string fingerprint, ReadModelBatchRecoveryAnchor anchor, CancellationToken token)
    {
        RawStateEntry row = await accessor.ReadAsync(Key(scope, fingerprint, anchor.Ordinal), token).ConfigureAwait(false);
        if (!row.Exists || row.Value.Length > MaxEncodedBytes) return false;
        ReadModelBatchParticipantProof? proof = JsonSerializer.Deserialize<ReadModelBatchParticipantProof>(row.Value.Span, JsonSerializerOptions.Web);
        return IsValid(proof, scope, fingerprint, row.Value)
            && ReadModelBatchCanonicalJson.Serialize(proof.Anchor).Span.SequenceEqual(ReadModelBatchCanonicalJson.Serialize(anchor).Span);
    }

    /// <summary>Rejects malformed existing participant proof before a terminal receipt can seal auxiliary retirement.</summary>
    internal static async Task<bool> ValidateExistingAsync(IReadModelBatchStateAccessor accessor, ReadModelBatchScope scope,
        string fingerprint, int participantCount, CancellationToken token)
    {
        for (int ordinal = 0; ordinal < participantCount; ordinal++)
        {
            RawStateEntry row = await accessor.ReadAsync(Key(scope, fingerprint, ordinal), token).ConfigureAwait(false);
            if (!row.Exists) continue; // The original installer never reached this ordinal.
            if (row.Value.Length > MaxEncodedBytes) return false;
            ReadModelBatchParticipantProof? proof = JsonSerializer.Deserialize<ReadModelBatchParticipantProof>(row.Value.Span, JsonSerializerOptions.Web);
            if (!IsValid(proof, scope, fingerprint, row.Value) || proof.Anchor.Ordinal != ordinal) return false;
        }
        return true;
    }

    /// <summary>Reads bounded canonical proof for only the original participant and terminal identity.</summary>
    internal static async Task<ReadModelBatchRecoveryAnchor?> ReadAsync(IReadModelBatchStateAccessor accessor,
        ReadModelBatchTerminalFence fence, ReadModelBatchEnvelope envelope, string key, CancellationToken token)
    {
        if (envelope.Ordinal < 0 || envelope.Ordinal >= fence.ParticipantCount) return null;
        RawStateEntry row = await accessor.ReadAsync(Key(fence.Scope, fence.Fingerprint, envelope.Ordinal), token).ConfigureAwait(false);
        if (!row.Exists || row.Value.Length > MaxEncodedBytes) return null;
        ReadModelBatchParticipantProof? proof = JsonSerializer.Deserialize<ReadModelBatchParticipantProof>(row.Value.Span, JsonSerializerOptions.Web);
        var marker = new ReadModelBatchMarker { ScopeHash = fence.Scope.ComputeScopeHash(), BatchId = fence.Scope.BatchId,
            Fingerprint = fence.Fingerprint, Status = fence.Decision ?? ReadModelBatchMarkerStatus.Aborted };
        return IsValid(proof, fence.Scope, fence.Fingerprint, row.Value)
            && proof!.Anchor.Key == key && proof.Anchor.Ordinal == envelope.Ordinal
            && proof.Anchor.Matches(marker, fence.Scope) && proof.Anchor.Matches(marker, envelope)
            ? proof.Anchor : null;
    }

    /// <summary>Applies the shared canonical typed proof guard before recovery can mutate evidence.</summary>
    internal static bool IsValid([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ReadModelBatchParticipantProof? proof,
        ReadModelBatchScope scope, string fingerprint, ReadOnlyMemory<byte> bytes)
    {
        var marker = new ReadModelBatchMarker { ScopeHash = scope.ComputeScopeHash(), BatchId = scope.BatchId,
            Fingerprint = fingerprint, Status = ReadModelBatchMarkerStatus.Aborted };
        return proof is not null && proof.Scope == scope && proof.Fingerprint == fingerprint
            && proof.Anchor?.Scope is not null && proof.Anchor.Matches(marker, scope)
            && Enum.IsDefined(proof.ConcurrencyMode) && proof.ExpectedETagDigest is { Length: 64 }
            && proof.ExpectedETagDigest.All(static character => char.IsAsciiHexDigitUpper(character))
            && ReadModelBatchCanonicalJson.Serialize(proof).Span.SequenceEqual(bytes.Span);
    }
}
