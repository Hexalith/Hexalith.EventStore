using System.Text;

namespace Hexalith.EventStore.Client.Events;
/// <summary>Encodes strict distinct anchor selection and covered-history-bound private preparation seeds.</summary>
internal static class DaprLogicalReplayAnchorCodec
{
    /// <summary>Gets the explicitly selected preparation model, unsupported by event-only v1 consumers.</summary>
    internal const string ModelId = "dapr-actor-logical-anchored-replay-v1";
    /// <summary>Gets the complete strict anchor selection ceiling.</summary>
    internal const int MaximumBytes = 8 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Measures exact writer capacity and refuses invalid scope/scalars before allocating its writer.</summary>
    internal static int Measure(DaprLogicalReplayAnchorSelection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ModelId != ModelId || value.CoveredSequence < 1 || value.TargetSequence < value.CoveredSequence || value.ActorHead < value.TargetSequence || value.CoveredSequence == value.TargetSequence && value.TargetSequence < value.ActorHead)
        {
            throw new ArgumentException("Unsupported anchor scope, model or zero-tail selection.");
        }

        int capacity = "HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8.Length + 3 + 16 + 8 * 32 + 3 * 8;
        foreach (string text in new[]
        {
            value.TenantId,
            value.Domain,
            value.AggregateId,
            value.AggregateType,
            value.ModelId
        }

        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            int length = StrictUtf8.GetByteCount(text);
            if (length > 512)
            {
                throw new ArgumentException("Anchor text exceeds 512 UTF-8 bytes.");
            }

            capacity = checked(capacity + 4 + length);
        }

        foreach (ReadOnlyMemory<byte> hash in new[]
        {
            value.SourceBindingHash,
            value.RegistryFingerprint,
            value.ReconstructionBindingHash,
            value.SnapshotWitnessHash,
            value.CanonicalStateHash,
            value.CoveredAccumulator,
            value.CoveredEffectiveChain,
            value.CoveredTranscript
        }

        )
        {
            RequireHash(hash);
        }

        if (capacity > MaximumBytes)
        {
            throw new ArgumentException("Anchor selection exceeds its ceiling.");
        }

        return capacity;
    }

    /// <summary>Encodes exact sixteen-field selection bytes without creating a continuation or proof.</summary>
    internal static byte[] Encode(DaprLogicalReplayAnchorSelection value)
    {
        using var writer = new EventEvolutionBinaryWriter(Measure(value));
        writer.WriteRaw("HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8);
        writer.WriteByte(1);
        writer.WriteUInt16(16);
        Text(writer, 1, value.TenantId);
        Text(writer, 2, value.Domain);
        Text(writer, 3, value.AggregateId);
        Text(writer, 4, value.AggregateType);
        Hash(writer, 5, value.SourceBindingHash);
        Hash(writer, 6, value.RegistryFingerprint);
        Hash(writer, 7, value.ReconstructionBindingHash);
        Hash(writer, 8, value.SnapshotWitnessHash);
        Hash(writer, 9, value.CanonicalStateHash);
        Hash(writer, 10, value.CoveredAccumulator);
        Hash(writer, 11, value.CoveredEffectiveChain);
        Hash(writer, 12, value.CoveredTranscript);
        Number(writer, 13, value.CoveredSequence);
        Number(writer, 14, value.ActorHead);
        Number(writer, 15, value.TargetSequence);
        Text(writer, 16, value.ModelId);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Decodes only privately captured bounded bytes, preserving hash slices within their owned lifetime.</summary>
    internal static DaprLogicalReplayAnchorSelection Decode(ReadOnlyMemory<byte> image)
    {
        if (image.Length > MaximumBytes)
        {
            throw new ArgumentException("Anchor selection exceeds its ceiling.");
        }

        var reader = new EventEvolutionBinaryReader(image.Span);
        if (!reader.ReadRaw("HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8.Length).SequenceEqual("HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8) || reader.ReadByte() != 1 || reader.ReadUInt16() != 16)
        {
            throw new ArgumentException("Foreign anchor framing.");
        }

        var result = new DaprLogicalReplayAnchorSelection(Text(ref reader, 1), Text(ref reader, 2), Text(ref reader, 3), Text(ref reader, 4), Hash(ref reader, image, 5), Hash(ref reader, image, 6), Hash(ref reader, image, 7), Hash(ref reader, image, 8), Hash(ref reader, image, 9), Hash(ref reader, image, 10), Hash(ref reader, image, 11), Hash(ref reader, image, 12), Number(ref reader, 13), Number(ref reader, 14), Number(ref reader, 15), Text(ref reader, 16));
        reader.RequireEnd();
        _ = Measure(result);
        return result;
    }

    /// <summary>Hashes the distinct accumulator seed bound to the exact selection and covered logical history.</summary>
    internal static byte[] AccumulatorSeed(ReadOnlyMemory<byte> selectionHash, ReadOnlyMemory<byte> covered, EventBufferBudget budget) => Seed("HX-EV-DAPR-ANCHOR-ACCUMULATOR-1\0"u8, selectionHash, covered, budget);
    /// <summary>Hashes the distinct effective seed bound to the exact selection and complete covered effective history.</summary>
    internal static byte[] EffectiveSeed(ReadOnlyMemory<byte> selectionHash, ReadOnlyMemory<byte> covered, EventBufferBudget budget) => Seed("HX-EV-DAPR-ANCHOR-EFFECTIVE-1\0"u8, selectionHash, covered, budget);
    /// <summary>Hashes the distinct transcript seed bound to the exact selection and exact covered page transcript.</summary>
    internal static byte[] TranscriptSeed(ReadOnlyMemory<byte> selectionHash, ReadOnlyMemory<byte> covered, EventBufferBudget budget) => Seed("HX-EV-DAPR-ANCHOR-TRANSCRIPT-1\0"u8, selectionHash, covered, budget);
    private static byte[] Seed(ReadOnlySpan<byte> separator, ReadOnlyMemory<byte> selectionHash, ReadOnlyMemory<byte> covered, EventBufferBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        RequireHash(selectionHash);
        RequireHash(covered);
        int capacity = separator.Length + 1 + 64;
        using EventBufferReservation working = budget.Reserve(checked(capacity * 2 + 256));
        using var writer = new EventEvolutionBinaryWriter(capacity);
        writer.WriteRaw(separator);
        writer.WriteByte(1);
        writer.WriteHash(selectionHash.Span);
        writer.WriteHash(covered.Span);
        return writer.ComputeSha256();
    }

    private static void RequireHash(ReadOnlyMemory<byte> value)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("Anchor commitments require exact B32 values.");
        }
    }

    private static void Text(EventEvolutionBinaryWriter writer, byte tag, string text)
    {
        writer.WriteByte(tag);
        writer.WriteString(text);
    }

    private static void Hash(EventEvolutionBinaryWriter writer, byte tag, ReadOnlyMemory<byte> value)
    {
        writer.WriteByte(tag);
        writer.WriteHash(value.Span);
    }

    private static void Number(EventEvolutionBinaryWriter writer, byte tag, long value)
    {
        writer.WriteByte(tag);
        writer.WriteInt64(value);
    }

    private static void Tag(ref EventEvolutionBinaryReader reader, byte tag)
    {
        if (reader.ReadByte() != tag)
        {
            throw new ArgumentException("Unknown, duplicate or reordered anchor field.");
        }
    }

    private static string Text(ref EventEvolutionBinaryReader reader, byte tag)
    {
        Tag(ref reader, tag);
        return reader.ReadString(512);
    }

    private static ReadOnlyMemory<byte> Hash(ref EventEvolutionBinaryReader reader, ReadOnlyMemory<byte> image, byte tag)
    {
        Tag(ref reader, tag);
        int offset = reader.Position;
        _ = reader.ReadRaw(32);
        return image.Slice(offset, 32);
    }

    private static long Number(ref EventEvolutionBinaryReader reader, byte tag)
    {
        Tag(ref reader, tag);
        long value = reader.ReadInt64();
        if (value < 0)
        {
            throw new ArgumentException("Anchor sequence cannot be negative.");
        }

        return value;
    }
}
