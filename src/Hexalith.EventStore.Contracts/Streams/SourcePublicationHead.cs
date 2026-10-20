using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>The authenticated committed prefix to reconcile for one exact source.</summary>
/// <param name="Identity">The exact tenant/domain/source.</param>
/// <param name="Head">The committed source prefix, never a reserved global position.</param>
public sealed record SourcePublicationHead(AggregateIdentity Identity, long Head)
{
    /// <summary>Gets the exact native incarnation/prefix sampled by the namespace producer.</summary>
    /// <remarks>Absent on legacy carriers, which cannot establish native source completeness.</remarks>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeSourceProof? NativeProof { get; init; }

    /// <summary>Checks carrier structure only; absent legacy evidence remains explicitly absent.</summary>
    internal bool HasConsistentNativeProof => NativeProof is null
        || NativeProof is { CodecVersion: 1, Erased: false, RetainedFloor: 1 } proof
            && proof.TenantId == Identity.TenantId && proof.Domain == Identity.Domain
            && proof.AggregateId == Identity.AggregateId && proof.HeadSequence == Head
            && ByteAether.Ulid.Ulid.TryParse(proof.SourceIncarnation, null, out _)
            && proof.CumulativeDigest is { Length: 64 }
            && proof.CumulativeDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            && (Head == 0 ? proof.HeadMessageId is null : !string.IsNullOrWhiteSpace(proof.HeadMessageId)
                && proof.HeadMessageId.Length <= 256 && proof.HeadMessageId == proof.HeadMessageId.Trim()
                && !proof.HeadMessageId.Any(char.IsControl));

    /// <summary>Checks structural monotonic continuity; this does not authenticate a larger prefix.</summary>
    internal bool CanAdvanceUnder(SourcePublicationHead current)
        => Identity == current.Identity && Head <= current.Head
            && (NativeProof is null ? current.NativeProof is null
                : current.NativeProof is { Erased: false } next && !NativeProof.Erased
                    && NativeProof.CodecVersion == next.CodecVersion
                    && NativeProof.TenantId == next.TenantId && NativeProof.Domain == next.Domain
                    && NativeProof.AggregateId == next.AggregateId
                    && NativeProof.SourceIncarnation == next.SourceIncarnation
                    && (Head != current.Head || NativeProof == next));
}
