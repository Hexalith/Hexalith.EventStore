using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>SDK-only immutable content-free completion evidence; never carries recovery operations or manifests.</summary>
/// <param name="Version">The completion proof protocol.</param>
/// <param name="ScopeHash">The original coordinated batch scope.</param>
/// <param name="BatchId">The original batch identity.</param>
/// <param name="Fingerprint">The original immutable manifest fingerprint.</param>
/// <param name="TerminalTimeUtc">The original durable receipt time.</param>
/// <param name="Owners">The exact coordinated selections.</param>
/// <param name="Digest">The canonical content integrity digest.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ReadModelBatchCompletionProof(
    [property: JsonRequired] int Version,
    [property: JsonRequired] string ScopeHash,
    [property: JsonRequired] string BatchId,
    [property: JsonRequired] string Fingerprint,
    [property: JsonRequired] string TerminalTimeUtc,
    [property: JsonRequired] IReadOnlyList<ProjectionOwnerSelection> Owners,
    [property: JsonRequired] string Digest)
{
    private static readonly JsonSerializerOptions s_strictJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Copies only terminal metadata from the actual SDK receipt.</summary>
    internal static ReadModelBatchCompletionProof From(ReadModelBatchMarker receipt)
    {
        var proof = new ReadModelBatchCompletionProof(1, receipt.ScopeHash, receipt.BatchId,
            receipt.Fingerprint, receipt.TerminalTimeUtc!, receipt.OwnerSelections!, string.Empty);
        return proof with { Digest = proof.ComputeDigest() };
    }

    /// <summary>Parses only the bounded exact completion format and detects altered fields.</summary>
    internal static ReadModelBatchCompletionProof? Read(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > ReadModelBatchMarker.MaxControlBytes) return null;
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || ReadModelBatchMarker.HasDuplicateProperties(document.RootElement)) return null;
        ReadModelBatchCompletionProof? proof = document.RootElement.Deserialize<ReadModelBatchCompletionProof>(s_strictJson);
        return proof is { Version: 1, Owners.Count: > 0 } && ReadModelBatchFingerprint.IsCanonical(proof.Fingerprint)
            && ReadModelBatchMarker.IsUtcTerminalTime(proof.TerminalTimeUtc)
            && proof.Digest == proof.ComputeDigest() ? proof : null;
    }

    /// <summary>Adapts the isolated proof to the existing exact owner/receipt validation.</summary>
    internal ReadModelBatchMarker ToReceipt() => new()
    {
        ScopeHash = ScopeHash, BatchId = BatchId, Fingerprint = Fingerprint,
        Status = ReadModelBatchMarkerStatus.Completed, TerminalTimeUtc = TerminalTimeUtc, OwnerSelections = Owners,
    };

    private string ComputeDigest() => Convert.ToHexStringLower(SHA256.HashData(
        ReadModelBatchCanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(this with { Digest = string.Empty }, JsonSerializerOptions.Web))));
}
