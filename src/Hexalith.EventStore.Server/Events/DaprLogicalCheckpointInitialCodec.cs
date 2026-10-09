using System.Text;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Measures and strictly frames unsigned checkpoint preparation; encoding grants no proof authority.</summary>
internal static class DaprLogicalCheckpointInitialCodec
{
    private static ReadOnlySpan<byte> Separator => "HX-EV-DAPR-CHECKPOINT-INITIAL-PREPARE-1\0"u8;

    /// <summary>Gets the exact explicitly selected unsigned policy.</summary>
    internal const string ModelId = "dapr-actor-logical-checkpoint-initial-preparation-v1";
    /// <summary>Gets the complete measured metadata ceiling.</summary>
    internal const int MaximumBytes = 1024;
    /// <summary>Measures every exact written byte before writer allocation.</summary>
    internal static int Measure(DaprLogicalCheckpointInitialSelection value)
    {
        Require(value);
        return checked(Separator.Length + 3 + 12 + 8 * 32 + 3 * 8 + 4 + Encoding.UTF8.GetByteCount(ModelId));
    }

    /// <summary>Encodes only mandatory ascending fields without optional markers or signing.</summary>
    internal static byte[] Encode(DaprLogicalCheckpointInitialSelection value)
    {
        using var writer = new EventEvolutionBinaryWriter(Measure(value));
        writer.WriteRaw(Separator);
        writer.WriteByte(1);
        writer.WriteUInt16(12);
        writer.WriteByte(1);
        writer.WriteHash(value.CheckpointSourceHash.Span);
        writer.WriteByte(2);
        writer.WriteHash(value.RequestedSourceHash.Span);
        writer.WriteByte(3);
        writer.WriteHash(value.FoldHash.Span);
        writer.WriteByte(4);
        writer.WriteHash(value.ReconstructionHash.Span);
        writer.WriteByte(5);
        writer.WriteHash(value.RegistryFingerprint.Span);
        writer.WriteByte(6);
        writer.WriteHash(value.StateHash.Span);
        writer.WriteByte(7);
        writer.WriteHash(value.RootHash.Span);
        writer.WriteByte(8);
        writer.WriteHash(value.WitnessHash.Span);
        writer.WriteByte(9);
        writer.WriteInt64(value.CoveredSequence);
        writer.WriteByte(10);
        writer.WriteInt64(value.ActorHead);
        writer.WriteByte(11);
        writer.WriteInt64(value.TargetSequence);
        writer.WriteByte(12);
        writer.WriteString(value.Model);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Decodes a bounded private image; returned hash fields alias that image and grant no authority.</summary>
    internal static DaprLogicalCheckpointInitialSelection Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new ArgumentException("Unsigned preparation exceeds its ceiling.");
        }

        var reader = new EventEvolutionBinaryReader(bytes.Span);
        if (!reader.ReadRaw(Separator.Length).SequenceEqual(Separator) || reader.ReadByte() != 1 || reader.ReadUInt16() != 12)
        {
            throw new ArgumentException("Exact unsigned preparation framing required.");
        }

        Tag(ref reader, 1);
        ReadOnlyMemory<byte> field1 = Hash(ref reader, bytes);
        Tag(ref reader, 2);
        ReadOnlyMemory<byte> field2 = Hash(ref reader, bytes);
        Tag(ref reader, 3);
        ReadOnlyMemory<byte> field3 = Hash(ref reader, bytes);
        Tag(ref reader, 4);
        ReadOnlyMemory<byte> field4 = Hash(ref reader, bytes);
        Tag(ref reader, 5);
        ReadOnlyMemory<byte> field5 = Hash(ref reader, bytes);
        Tag(ref reader, 6);
        ReadOnlyMemory<byte> field6 = Hash(ref reader, bytes);
        Tag(ref reader, 7);
        ReadOnlyMemory<byte> field7 = Hash(ref reader, bytes);
        Tag(ref reader, 8);
        ReadOnlyMemory<byte> field8 = Hash(ref reader, bytes);
        Tag(ref reader, 9);
        long field9 = reader.ReadInt64();
        Tag(ref reader, 10);
        long field10 = reader.ReadInt64();
        Tag(ref reader, 11);
        long field11 = reader.ReadInt64();
        Tag(ref reader, 12);
        string field12 = reader.ReadString(512);
        reader.RequireEnd();
        var value = new DaprLogicalCheckpointInitialSelection(field1, field2, field3, field4, field5, field6, field7, field8, field9, field10, field11, field12);
        Require(value);
        return value;
    }

    private static void Require(DaprLogicalCheckpointInitialSelection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Model != ModelId || value.CheckpointSourceHash.Length != 32 || value.RequestedSourceHash.Length != 32 || value.FoldHash.Length != 32 || value.ReconstructionHash.Length != 32 || value.RegistryFingerprint.Length != 32 || value.StateHash.Length != 32 || value.RootHash.Length != 32 || value.WitnessHash.Length != 32)
        {
            throw new ArgumentException("CheckpointPreparationHold: exact policy and 32-byte fields required.");
        }

        if (value.CoveredSequence < 1 || value.ActorHead < value.TargetSequence || value.TargetSequence < value.CoveredSequence)
        {
            throw new ArgumentException("CheckpointPreparationHold: require 1 <= covered <= target <= actual head.");
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
