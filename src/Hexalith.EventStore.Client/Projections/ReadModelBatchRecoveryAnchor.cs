using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Payload-free terminal proof for one existing envelope; no additional key or lookup registry.</summary>
/// <param name="Scope">Original bounded scope.</param>
/// <param name="Key">The final participating logical key.</param>
/// <param name="Ordinal">Its original operation ordinal.</param>
/// <param name="IsDelete">Original operation kind.</param>
/// <param name="CandidateDigest">SHA-256 of original candidate bytes (empty for deletion).</param>
/// <param name="PreviousDigest">SHA-256 of prior bytes, or null for prior absence.</param>
/// <param name="TimeToLive">Original candidate retention.</param>
/// <param name="ExpiresAtUtc">Original inclusive source deadline.</param>
internal sealed record ReadModelBatchRecoveryAnchor([property: JsonRequired] ReadModelBatchScope Scope, [property: JsonRequired] string Key, [property: JsonRequired] int Ordinal, [property: JsonRequired] bool IsDelete,
    [property: JsonRequired] string CandidateDigest, [property: JsonRequired] string? PreviousDigest, [property: JsonRequired] TimeSpan? TimeToLive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresAtUtc = null)
{
    /// <summary>Hashes exact stored value bytes without retaining their payload.</summary>
    internal static string Digest(ReadOnlyMemory<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes.Span));

    /// <summary>Validates terminal proof scope and bounded metadata before any mutation.</summary>
    internal bool Matches(ReadModelBatchMarker marker, ReadModelBatchScope expected)
    {
        Scope.Validate();
        return marker.Version == 1 && marker.OwnerSelection is null && marker.OwnerSelections is null
            && marker.OriginalManifest is null && marker.OriginalManifestKey is null && marker.Operations is null
            && marker.Status is ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted
            && Scope.StoreName == expected.StoreName && Scope.TenantId == expected.TenantId && Scope.Domain == expected.Domain
            && Scope.AggregateId == expected.AggregateId && Scope.ProjectionType == expected.ProjectionType
            && Scope.BatchId == marker.BatchId && Scope.ComputeScopeHash() == marker.ScopeHash
            && ReadModelBatchFingerprint.IsCanonical(marker.Fingerprint) && !string.IsNullOrWhiteSpace(Key)
            && Encoding.UTF8.GetByteCount(Key) <= ReadModelBatchOptions.DefaultMaxKeyByteLength
            && Ordinal >= 0 && Ordinal < ReadModelBatchOptions.DefaultMaxOperations
            && IsDigest(CandidateDigest) && (PreviousDigest is null || IsDigest(PreviousDigest))
            && (TimeToLive is null || TimeToLive > TimeSpan.Zero)
            && (TimeToLive is null || ExpiresAtUtc is null)
            && (!IsDelete || CandidateDigest == Digest(ReadOnlyMemory<byte>.Empty) && TimeToLive is null && ExpiresAtUtc is null);
    }

    /// <summary>Checks both candidate and prior-value digests against the final own envelope.</summary>
    internal bool Matches(ReadModelBatchMarker marker, ReadModelBatchEnvelope envelope) => envelope.ScopeHash == marker.ScopeHash
        && envelope.ExpiresAtUtc == ExpiresAtUtc
        && envelope.Fingerprint == marker.Fingerprint && envelope.Ordinal == Ordinal && envelope.IsDelete == IsDelete
        && Digest(envelope.CandidateBytes()) == CandidateDigest
        && (envelope.PreviousBase64 is null ? PreviousDigest is null : Digest(envelope.PreviousBytes()) == PreviousDigest);

    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(static character => char.IsAsciiHexDigitUpper(character));
}
