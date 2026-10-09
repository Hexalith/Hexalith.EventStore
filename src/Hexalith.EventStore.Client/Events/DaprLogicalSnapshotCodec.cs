using System.Text;

namespace Hexalith.EventStore.Client.Events;
/// <summary>Encodes strict distinct logical snapshot, checkpoint and rebase candidate records.</summary>
internal static class DaprLogicalSnapshotCodec
{
    /// <summary>Gets the explicit snapshot candidate model, separate from event-only prefix v1.</summary>
    internal const string SnapshotModel = "dapr-actor-logical-snapshot-v1";
    /// <summary>Gets the projection-only candidate model.</summary>
    internal const string CheckpointModel = "dapr-actor-logical-checkpoint-v1";
    /// <summary>Gets the distinct unqualified rebase candidate model.</summary>
    internal const string RebaseModel = "dapr-actor-logical-snapshot-rebase-v1";
    /// <summary>Gets the bounded complete witness ceiling.</summary>
    internal const int MaximumBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Derives private candidate tail arithmetic without granting progress or incrementing MAX.</summary>
    internal static long TailStart(long coveredSequence)
    {
        if (coveredSequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(coveredSequence));
        }

        return coveredSequence == long.MaxValue ? long.MaxValue : coveredSequence + 1;
    }

    /// <summary>Encodes all twenty snapshot fields after exact measured capacity and scalar checks.</summary>
    internal static byte[] EncodeSnapshot(DaprLogicalSnapshotWitness value)
    {
        int capacity = MeasureSnapshot(value);
        using var w = Header("HX-EV-DAPR-SNAPSHOT-WITNESS-1\0"u8, 20, capacity);
        Text(w, 1, value.TenantId);
        Text(w, 2, value.Domain);
        Text(w, 3, value.AggregateId);
        Text(w, 4, value.AggregateType);
        Hash(w, 5, value.SourceBindingHash);
        Number(w, 6, value.CoveredSequence);
        Text(w, 7, value.StorageKey);
        Text(w, 8, value.WitnessKey);
        Hash(w, 9, value.StorageHash);
        Hash(w, 10, value.FoldedHash);
        Hash(w, 11, value.RegistryFingerprint);
        Hash(w, 12, value.ReconstructionBindingHash);
        Hash(w, 13, value.Accumulator);
        Text(w, 14, value.OperationId);
        Number(w, 15, value.Generation);
        Hash(w, 16, value.TranscriptHash);
        Hash(w, 17, value.EffectiveChainHash);
        Text(w, 18, value.ProtectionCodec);
        Text(w, 19, value.ModelId);
        Text(w, 20, value.SerializerId);
        return w.CopyEncodedBytes();
    }

    /// <summary>Measures exact writer capacity without allocating decoded strings or a binary writer.</summary>
    internal static int MeasureSnapshot(DaprLogicalSnapshotWitness value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.CoveredSequence < 1 || value.Generation < 1 || value.ModelId != SnapshotModel || value.ProtectionCodec != "plaintext-canonical-v1")
        {
            throw new ArgumentException("Unsupported snapshot model, protection or scalar.");
        }

        int size = "HX-EV-DAPR-SNAPSHOT-WITNESS-1\0"u8.Length + 3 + 20 + 8 * 32 + 2 * 8;
        foreach (string text in new[]
        {
            value.TenantId,
            value.Domain,
            value.AggregateId,
            value.AggregateType,
            value.StorageKey,
            value.WitnessKey,
            value.OperationId,
            value.ProtectionCodec,
            value.ModelId,
            value.SerializerId
        }

        )
        {
            size = checked(size + MeasureText(text));
        }

        foreach (ReadOnlyMemory<byte> hash in new[]
        {
            value.SourceBindingHash,
            value.StorageHash,
            value.FoldedHash,
            value.RegistryFingerprint,
            value.ReconstructionBindingHash,
            value.Accumulator,
            value.TranscriptHash,
            value.EffectiveChainHash
        }

        )
        {
            RequireHash(hash);
        }

        if (size > MaximumBytes)
        {
            throw new ArgumentException("Snapshot witness exceeds its ceiling.");
        }

        return size;
    }

    /// <summary>Decodes only an already privately captured bounded witness, rejecting every ambiguous field.</summary>
    internal static DaprLogicalSnapshotWitness DecodeSnapshot(ReadOnlyMemory<byte> image)
    {
        var r = Reader(image, "HX-EV-DAPR-SNAPSHOT-WITNESS-1\0"u8, 20);
        string tenant = Text(ref r, 1);
        string domain = Text(ref r, 2);
        string aggregate = Text(ref r, 3);
        string type = Text(ref r, 4);
        ReadOnlyMemory<byte> source = Hash(ref r, image, 5);
        long sequence = Number(ref r, 6);
        string storage = Text(ref r, 7);
        string witness = Text(ref r, 8);
        ReadOnlyMemory<byte> stored = Hash(ref r, image, 9);
        ReadOnlyMemory<byte> folded = Hash(ref r, image, 10);
        ReadOnlyMemory<byte> registry = Hash(ref r, image, 11);
        ReadOnlyMemory<byte> binding = Hash(ref r, image, 12);
        ReadOnlyMemory<byte> accumulator = Hash(ref r, image, 13);
        string operation = Text(ref r, 14);
        long generation = Number(ref r, 15);
        ReadOnlyMemory<byte> transcript = Hash(ref r, image, 16);
        ReadOnlyMemory<byte> effective = Hash(ref r, image, 17);
        string protection = Text(ref r, 18);
        string model = Text(ref r, 19);
        string serializer = Text(ref r, 20);
        r.RequireEnd();
        var value = new DaprLogicalSnapshotWitness(tenant, domain, aggregate, type, source, sequence, storage, witness, stored, folded, registry, binding, accumulator, operation, generation, transcript, effective, protection, model, serializer);
        _ = MeasureSnapshot(value);
        return value;
    }

    /// <summary>Encodes exact projection checkpoint framing without enabling projection intake.</summary>
    internal static byte[] EncodeCheckpoint(DaprLogicalCheckpointWitness value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.CoveredSequence < 0 || value.ModelId != CheckpointModel)
        {
            throw new ArgumentException("Invalid checkpoint model or sequence.");
        }

        int size = "HX-EV-DAPR-CHECKPOINT-WITNESS-1\0"u8.Length + 3 + 9 + 4 * 32 + 8;
        foreach (string text in new[]
        {
            value.TenantId,
            value.Domain,
            value.Projection,
            value.ModelId
        }

        )
        {
            size += MeasureText(text);
        }

        using var w = Header("HX-EV-DAPR-CHECKPOINT-WITNESS-1\0"u8, 9, size);
        Text(w, 1, value.TenantId);
        Text(w, 2, value.Domain);
        Text(w, 3, value.Projection);
        Hash(w, 4, value.ScopeHash);
        Number(w, 5, value.CoveredSequence);
        Hash(w, 6, value.Accumulator);
        Hash(w, 7, value.RegistryFingerprint);
        Hash(w, 8, value.RootHash);
        Text(w, 9, value.ModelId);
        return w.CopyEncodedBytes();
    }

    /// <summary>Decodes strict projection checkpoint candidate fields; no owner authority is inferred.</summary>
    internal static DaprLogicalCheckpointWitness DecodeCheckpoint(ReadOnlyMemory<byte> image)
    {
        var r = Reader(image, "HX-EV-DAPR-CHECKPOINT-WITNESS-1\0"u8, 9);
        var value = new DaprLogicalCheckpointWitness(Text(ref r, 1), Text(ref r, 2), Text(ref r, 3), Hash(ref r, image, 4), Number(ref r, 5), Hash(ref r, image, 6), Hash(ref r, image, 7), Hash(ref r, image, 8), Text(ref r, 9));
        r.RequireEnd();
        if (value.ModelId != CheckpointModel)
        {
            throw new ArgumentException("Foreign checkpoint model.");
        }

        return value;
    }

    /// <summary>Encodes distinct rebase candidate framing without claiming equivalence or a current owner.</summary>
    internal static byte[] EncodeRebase(DaprLogicalSnapshotRebase value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ModelId != RebaseModel)
        {
            throw new ArgumentException("Foreign rebase model.");
        }

        using var w = Header("HX-EV-DAPR-SNAPSHOT-REBASE-1\0"u8, 8, "HX-EV-DAPR-SNAPSHOT-REBASE-1\0"u8.Length + 3 + 8 + 7 * 32 + MeasureText(value.ModelId));
        Hash(w, 1, value.PriorWitnessHash);
        Hash(w, 2, value.SuccessorWitnessHash);
        Hash(w, 3, value.PriorSourceHash);
        Hash(w, 4, value.SuccessorSourceHash);
        Hash(w, 5, value.PriorRegistry);
        Hash(w, 6, value.SuccessorRegistry);
        Hash(w, 7, value.FoldedHash);
        Text(w, 8, value.ModelId);
        return w.CopyEncodedBytes();
    }

    /// <summary>Decodes strict rebase fields while retaining their untrusted candidate meaning.</summary>
    internal static DaprLogicalSnapshotRebase DecodeRebase(ReadOnlyMemory<byte> image)
    {
        var r = Reader(image, "HX-EV-DAPR-SNAPSHOT-REBASE-1\0"u8, 8);
        var value = new DaprLogicalSnapshotRebase(Hash(ref r, image, 1), Hash(ref r, image, 2), Hash(ref r, image, 3), Hash(ref r, image, 4), Hash(ref r, image, 5), Hash(ref r, image, 6), Hash(ref r, image, 7), Text(ref r, 8));
        r.RequireEnd();
        if (value.ModelId != RebaseModel)
        {
            throw new ArgumentException("Foreign rebase model.");
        }

        return value;
    }

    private static int MeasureText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        int length = StrictUtf8.GetByteCount(value);
        if (length > 512)
        {
            throw new ArgumentException("Snapshot field exceeds 512 UTF-8 bytes.");
        }

        return checked(4 + length);
    }

    private static void RequireHash(ReadOnlyMemory<byte> value)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("Anchor hash requires exactly 32 bytes.");
        }
    }

    private static EventEvolutionBinaryWriter Header(ReadOnlySpan<byte> prefix, ushort count, int capacity)
    {
        var w = new EventEvolutionBinaryWriter(capacity);
        w.WriteRaw(prefix);
        w.WriteByte(1);
        w.WriteUInt16(count);
        return w;
    }

    private static EventEvolutionBinaryReader Reader(ReadOnlyMemory<byte> image, ReadOnlySpan<byte> prefix, ushort count)
    {
        if (image.Length > MaximumBytes)
        {
            throw new ArgumentException("Anchor record exceeds its ceiling.");
        }

        var r = new EventEvolutionBinaryReader(image.Span);
        if (!r.ReadRaw(prefix.Length).SequenceEqual(prefix) || r.ReadByte() != 1 || r.ReadUInt16() != count)
        {
            throw new ArgumentException("Foreign anchor schema.");
        }

        return r;
    }

    private static void Text(EventEvolutionBinaryWriter w, byte tag, string value)
    {
        w.WriteByte(tag);
        w.WriteString(value);
    }

    private static void Hash(EventEvolutionBinaryWriter w, byte tag, ReadOnlyMemory<byte> value)
    {
        w.WriteByte(tag);
        w.WriteHash(value.Span);
    }

    private static void Number(EventEvolutionBinaryWriter w, byte tag, long value)
    {
        w.WriteByte(tag);
        w.WriteInt64(value);
    }

    private static void Tag(ref EventEvolutionBinaryReader r, byte tag)
    {
        if (r.ReadByte() != tag)
        {
            throw new ArgumentException("Missing, duplicate or unordered anchor tag.");
        }
    }

    private static string Text(ref EventEvolutionBinaryReader r, byte tag)
    {
        Tag(ref r, tag);
        string text = r.ReadString(512);
        _ = MeasureText(text);
        return text;
    }

    private static ReadOnlyMemory<byte> Hash(ref EventEvolutionBinaryReader r, ReadOnlyMemory<byte> image, byte tag)
    {
        Tag(ref r, tag);
        int position = r.Position;
        _ = r.ReadHash();
        return image.Slice(position, 32);
    }

    private static long Number(ref EventEvolutionBinaryReader r, byte tag)
    {
        Tag(ref r, tag);
        long value = r.ReadInt64();
        if (value < 0)
        {
            throw new ArgumentException("Negative anchor scalar.");
        }

        return value;
    }
}
