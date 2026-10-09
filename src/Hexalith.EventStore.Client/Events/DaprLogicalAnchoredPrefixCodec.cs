using System.Text;

namespace Hexalith.EventStore.Client.Events;
/// <summary>Encodes only the distinct anchored prefix, including its exact selection and eligible zero-tail form.</summary>
internal static class DaprLogicalAnchoredPrefixCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static ReadOnlySpan<byte> Separator => "HX-EV-DAPR-ANCHORED-PREFIX-1\0"u8;

    /// <summary>Gets the bounded anchored claim ceiling.</summary>
    internal const int MaximumBytes = 8 * 1024;
    /// <summary>Measures the complete exact prefix image before allocating its writer.</summary>
    internal static int Measure(DaprLogicalAnchoredPrefixClaim claim)
    {
        Require(claim);
        DaprLogicalPrefixClaim p = claim.Prefix;
        int length = checked(Separator.Length + 3 + 17 + 5 * 4 + 5 * 8 + 4 + 6 * 32);
        foreach (string value in new[]
        {
            p.TenantId,
            p.Domain,
            p.AggregateId,
            p.AggregateType,
            p.LogicalEvidenceModelId
        }

        )
        {
            length = checked(length + StrictUtf8.GetByteCount(value));
        }

        if (length > MaximumBytes)
        {
            throw new ArgumentException("Anchored prefix exceeds its ceiling.");
        }

        return length;
    }

    /// <summary>Writes strict mandatory ascending anchored fields without historical optional markers.</summary>
    internal static byte[] Encode(DaprLogicalAnchoredPrefixClaim claim)
    {
        using var w = new EventEvolutionBinaryWriter(Measure(claim));
        DaprLogicalPrefixClaim p = claim.Prefix;
        w.WriteRaw(Separator);
        w.WriteByte(1);
        w.WriteUInt16(17);
        w.WriteByte(1);
        w.WriteString(p.TenantId);
        w.WriteByte(2);
        w.WriteString(p.Domain);
        w.WriteByte(3);
        w.WriteString(p.AggregateId);
        w.WriteByte(4);
        w.WriteString(p.AggregateType);
        w.WriteByte(5);
        w.WriteInt64(p.StartSequence);
        w.WriteByte(6);
        w.WriteInt64(p.EndSequence);
        w.WriteByte(7);
        w.WriteInt64(p.ActorHead);
        w.WriteByte(8);
        w.WriteInt64(p.TargetSequence);
        w.WriteByte(9);
        w.WriteInt32(p.Count);
        w.WriteByte(10);
        w.WriteHash(p.OrderedLogicalDigestListHash.Span);
        w.WriteByte(11);
        w.WriteHash(p.Accumulator.Span);
        w.WriteByte(12);
        w.WriteHash(p.RegistryFingerprint.Span);
        w.WriteByte(13);
        w.WriteHash(claim.SelectionHash.Span);
        w.WriteByte(14);
        w.WriteInt64(claim.CoveredSequence);
        w.WriteByte(15);
        w.WriteHash(claim.InitialStateHash.Span);
        w.WriteByte(16);
        w.WriteHash(p.SourceBindingHash.Span);
        w.WriteByte(17);
        w.WriteString(p.LogicalEvidenceModelId);
        return w.CopyEncodedBytes();
    }

    /// <summary>Decodes only the exact distinct model and bounded canonical tail/zero-tail forms.</summary>
    internal static DaprLogicalAnchoredPrefixClaim Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new ArgumentException("Anchored prefix exceeds its ceiling.");
        }

        var r = new EventEvolutionBinaryReader(bytes);
        if (!r.ReadRaw(Separator.Length).SequenceEqual(Separator) || r.ReadByte() != 1 || r.ReadUInt16() != 17)
        {
            throw new ArgumentException("Distinct anchored prefix model is required.");
        }

        Tag(ref r, 1);
        string tenant = r.ReadString(512);
        Tag(ref r, 2);
        string domain = r.ReadString(512);
        Tag(ref r, 3);
        string aggregate = r.ReadString(512);
        Tag(ref r, 4);
        string type = r.ReadString(512);
        Tag(ref r, 5);
        long start = r.ReadInt64();
        Tag(ref r, 6);
        long end = r.ReadInt64();
        Tag(ref r, 7);
        long head = r.ReadInt64();
        Tag(ref r, 8);
        long target = r.ReadInt64();
        Tag(ref r, 9);
        int count = r.ReadInt32();
        Tag(ref r, 10);
        byte[] list = r.ReadRaw(32).ToArray();
        Tag(ref r, 11);
        byte[] accumulator = r.ReadRaw(32).ToArray();
        Tag(ref r, 12);
        byte[] registry = r.ReadRaw(32).ToArray();
        Tag(ref r, 13);
        byte[] selection = r.ReadRaw(32).ToArray();
        Tag(ref r, 14);
        long covered = r.ReadInt64();
        Tag(ref r, 15);
        byte[] state = r.ReadRaw(32).ToArray();
        Tag(ref r, 16);
        byte[] source = r.ReadRaw(32).ToArray();
        Tag(ref r, 17);
        string model = r.ReadString(512);
        r.RequireEnd();
        var value = new DaprLogicalAnchoredPrefixClaim(new DaprLogicalPrefixClaim(tenant, domain, aggregate, type, start, end, head, target, count, list, accumulator, registry, source, model), selection, covered, state);
        Require(value);
        return value;
    }

    private static void Tag(ref EventEvolutionBinaryReader reader, byte tag)
    {
        if (reader.ReadByte() != tag)
        {
            throw new ArgumentException("Anchored prefix fields are missing, duplicated or reordered.");
        }
    }

    private static void Require(DaprLogicalAnchoredPrefixClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        DaprLogicalPrefixClaim p = claim.Prefix;
        ArgumentNullException.ThrowIfNull(p);
        foreach (string value in new[]
        {
            p.TenantId,
            p.Domain,
            p.AggregateId,
            p.AggregateType
        }

        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (StrictUtf8.GetByteCount(value) > 512)
            {
                throw new ArgumentException("Anchored prefix scope exceeds its UTF-8 bound.");
            }
        }

        if (p.LogicalEvidenceModelId != DaprLogicalReplayAnchorCodec.ModelId || claim.CoveredSequence < 1 || claim.CoveredSequence > p.TargetSequence || p.TargetSequence > p.ActorHead || p.StartSequence < 1 || claim.CoveredSequence == p.TargetSequence && p.TargetSequence < p.ActorHead || p.Count is < 0 or > 256 || p.EndSequence > p.TargetSequence || (p.Count == 0 ? claim.CoveredSequence != p.TargetSequence || p.TargetSequence != p.ActorHead || p.EndSequence != claim.CoveredSequence || p.StartSequence != (claim.CoveredSequence == long.MaxValue ? long.MaxValue : claim.CoveredSequence + 1) : p.StartSequence <= claim.CoveredSequence || p.EndSequence < p.StartSequence || p.EndSequence - p.StartSequence != p.Count - 1))
        {
            throw new ArgumentException("Unsupported anchored prefix range or model.");
        }

        foreach (ReadOnlyMemory<byte> hash in new[]
        {
            p.OrderedLogicalDigestListHash,
            p.Accumulator,
            p.RegistryFingerprint,
            p.SourceBindingHash,
            claim.SelectionHash,
            claim.InitialStateHash
        }

        )
        {
            if (hash.Length != 32)
            {
                throw new ArgumentException("Anchored prefix requires exact B32 commitments.");
            }
        }
    }
}
