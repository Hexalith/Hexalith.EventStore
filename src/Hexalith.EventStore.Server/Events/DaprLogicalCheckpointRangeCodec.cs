using System.Text;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Measures and strictly frames unsigned checkpoint preparation; encoding grants no proof authority.</summary>
internal static class DaprLogicalCheckpointRangeCodec
{
    private static ReadOnlySpan<byte> Separator => "HX-EV-DAPR-CHECKPOINT-RANGE-PREPARE-1\0"u8;

    /// <summary>Gets the exact explicitly selected unsigned policy.</summary>
    internal const string ModelId = "dapr-actor-logical-checkpoint-initial-preparation-v1";
    /// <summary>Gets the complete measured metadata ceiling.</summary>
    internal const int MaximumBytes = 1024;
    /// <summary>Measures every exact written byte before writer allocation.</summary>
    internal static int Measure(DaprLogicalCheckpointRangePreparation value)
    {
        Require(value);
        return checked(Separator.Length + 3 + 10 + 2 * 32 + 5 * 8 + 4 + 1 + 4 + Encoding.UTF8.GetByteCount(ModelId));
    }

    /// <summary>Encodes only mandatory ascending fields without optional markers or signing.</summary>
    internal static byte[] Encode(DaprLogicalCheckpointRangePreparation value)
    {
        using var writer = new EventEvolutionBinaryWriter(Measure(value));
        writer.WriteRaw(Separator);
        writer.WriteByte(1);
        writer.WriteUInt16(10);
        writer.WriteByte(1);
        writer.WriteHash(value.SelectionHash.Span);
        writer.WriteByte(2);
        writer.WriteHash(value.RequestedSourceHash.Span);
        writer.WriteByte(3);
        writer.WriteInt64(value.CoveredSequence);
        writer.WriteByte(4);
        writer.WriteInt64(value.ActorHead);
        writer.WriteByte(5);
        writer.WriteInt64(value.TargetSequence);
        writer.WriteByte(6);
        writer.WriteInt64(value.StartSequence);
        writer.WriteByte(7);
        writer.WriteInt64(value.EndSequence);
        writer.WriteByte(8);
        writer.WriteInt32(value.PlannedCount);
        writer.WriteByte(9);
        writer.WriteByte((byte)value.Kind);
        writer.WriteByte(10);
        writer.WriteString(value.Model);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Decodes a bounded private image; returned hash fields alias that image and grant no authority.</summary>
    internal static DaprLogicalCheckpointRangePreparation Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new ArgumentException("Unsigned preparation exceeds its ceiling.");
        }

        var reader = new EventEvolutionBinaryReader(bytes.Span);
        if (!reader.ReadRaw(Separator.Length).SequenceEqual(Separator) || reader.ReadByte() != 1 || reader.ReadUInt16() != 10)
        {
            throw new ArgumentException("Exact unsigned preparation framing required.");
        }

        Tag(ref reader, 1);
        ReadOnlyMemory<byte> field1 = Hash(ref reader, bytes);
        Tag(ref reader, 2);
        ReadOnlyMemory<byte> field2 = Hash(ref reader, bytes);
        Tag(ref reader, 3);
        long field3 = reader.ReadInt64();
        Tag(ref reader, 4);
        long field4 = reader.ReadInt64();
        Tag(ref reader, 5);
        long field5 = reader.ReadInt64();
        Tag(ref reader, 6);
        long field6 = reader.ReadInt64();
        Tag(ref reader, 7);
        long field7 = reader.ReadInt64();
        Tag(ref reader, 8);
        int field8 = reader.ReadInt32();
        Tag(ref reader, 9);
        DaprLogicalCheckpointRangeKind field9 = (DaprLogicalCheckpointRangeKind)reader.ReadByte();
        Tag(ref reader, 10);
        string field10 = reader.ReadString(512);
        reader.RequireEnd();
        var value = new DaprLogicalCheckpointRangePreparation(field1, field2, field3, field4, field5, field6, field7, field8, field9, field10);
        Require(value);
        return value;
    }

    private static void Require(DaprLogicalCheckpointRangePreparation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Model != ModelId || value.SelectionHash.Length != 32 || value.RequestedSourceHash.Length != 32)
        {
            throw new ArgumentException("CheckpointPreparationHold: exact policy and 32-byte fields required.");
        }

        if (value.CoveredSequence < 1 || value.ActorHead < value.TargetSequence || value.TargetSequence < value.CoveredSequence)
        {
            throw new ArgumentException("CheckpointPreparationHold: invalid covered/head/target.");
        }

        if (value.Kind == DaprLogicalCheckpointRangeKind.CurrentZero)
        {
            long start = value.CoveredSequence == long.MaxValue ? long.MaxValue : value.CoveredSequence + 1;
            if (value.CoveredSequence != value.TargetSequence || value.TargetSequence != value.ActorHead || value.PlannedCount != 0 || value.StartSequence != start || value.EndSequence != value.CoveredSequence)
            {
                throw new ArgumentException("CheckpointPreparationHold: exact current-head zero shape required.");
            }
        }
        else if (value.Kind == DaprLogicalCheckpointRangeKind.Tail)
        {
            if (value.CoveredSequence >= value.TargetSequence || value.PlannedCount is < 1 or > 256 || value.StartSequence != value.CoveredSequence + 1 || value.EndSequence < value.StartSequence || value.EndSequence > value.TargetSequence || value.EndSequence - value.StartSequence + 1 != value.PlannedCount)
            {
                throw new ArgumentException("CheckpointPreparationHold: exact bounded checkpoint-plus-one tail plan required.");
            }
        }
        else
        {
            throw new ArgumentException("CheckpointPreparationHold: unsupported unsigned range kind.");
        }
    }

    private static ReadOnlyMemory<byte> Hash(ref EventEvolutionBinaryReader reader, ReadOnlyMemory<byte> bytes)
    {
        int offset = reader.Position;
        _ = reader.ReadHash();
        return bytes.Slice(offset, 32);
    }

    private static void Tag(ref EventEvolutionBinaryReader reader, byte expected)
    {
        if (reader.ReadByte() != expected)
        {
            throw new ArgumentException("Unsigned preparation fields must be unique and ascending.");
        }
    }
}
