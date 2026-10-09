using System.Text;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Measures and frames distinct local projection checkpoint commitments; hashes alone grant no authority.</summary>
internal static class DaprLogicalCheckpointCodec
{
    /// <summary>Gets the explicit dormant issue/readback policy.</summary>
    internal const string ModelId = "dapr-actor-logical-checkpoint-owner-v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Measures a strict nonempty bounded text before any writer allocation.</summary>
    internal static int TextSize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        int bytes = StrictUtf8.GetByteCount(value);
        if (bytes > 512)
        {
            throw new ArgumentException("CheckpointHold: text exceeds 512 UTF-8 bytes.", nameof(value));
        }

        return checked(4 + bytes);
    }

    /// <summary>Commits the stable addressed projection namespace and declared local backend configuration pin.</summary>
    internal static byte[] Namespace(DaprLogicalSourceBinding source, string route, string actor, string store, ReadOnlySpan<byte> backend)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] texts = [source.Identity.TenantId, source.Identity.Domain, source.Identity.AggregateId, source.AggregateType, route, actor, store];
        using EventEvolutionBinaryWriter writer = Writer("HX-EV-DAPR-CHECKPOINT-NAMESPACE-1\0"u8, texts, 32);
        foreach (string text in texts)
        {
            writer.WriteString(text);
        }

        writer.WriteHash(backend);
        return writer.ComputeSha256();
    }

    /// <summary>Commits exactly the addressed declaration and reconstructed pure fold binding.</summary>
    internal static byte[] Fold(ReadOnlySpan<byte> scope, ReadOnlySpan<byte> reconstruction)
    {
        using EventEvolutionBinaryWriter writer = Writer("HX-EV-DAPR-PROJECTION-FOLD-1\0"u8, [], 64);
        writer.WriteHash(scope);
        writer.WriteHash(reconstruction);
        return writer.ComputeSha256();
    }

    /// <summary>Commits fixed source/head/target, namespace and exact fold declaration.</summary>
    internal static byte[] Scope(ReadOnlySpan<byte> scope, ReadOnlySpan<byte> source, ReadOnlySpan<byte> fold)
    {
        using EventEvolutionBinaryWriter writer = Writer("HX-EV-DAPR-CHECKPOINT-SCOPE-1\0"u8, [], 96);
        writer.WriteHash(scope);
        writer.WriteHash(source);
        writer.WriteHash(fold);
        return writer.ComputeSha256();
    }

    /// <summary>Derives immutable version identity without allowing an operation string to alter key structure.</summary>
    internal static byte[] Version(string operation, long generation)
    {
        if (generation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        using EventEvolutionBinaryWriter writer = Writer([], [operation], 8);
        writer.WriteString(operation);
        writer.WriteInt64(generation);
        return writer.ComputeSha256();
    }

    /// <summary>Encodes the exact ordered ten-field root, including an untrusted image of the actual completed origin.</summary>
    internal static byte[] Root(DaprLogicalSourceBinding source, string route, string actor, string store, ReadOnlySpan<byte> backend, ReadOnlySpan<byte> scope, ReadOnlySpan<byte> fold, string stateKey, ReadOnlySpan<byte> origin)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] texts = [source.Identity.TenantId, source.Identity.Domain, route, actor, store, stateKey];
        using EventEvolutionBinaryWriter writer = Writer("HX-EV-DAPR-CHECKPOINT-ROOT-1\0"u8, texts, checked(3 + 10 + 96 + 4 + origin.Length));
        writer.WriteByte(1);
        writer.WriteUInt16(10);
        for (int index = 0; index < 5; index++)
        {
            writer.WriteByte((byte)(index + 1));
            writer.WriteString(texts[index]);
        }

        writer.WriteByte(6);
        writer.WriteHash(backend);
        writer.WriteByte(7);
        writer.WriteHash(scope);
        writer.WriteByte(8);
        writer.WriteHash(fold);
        writer.WriteByte(9);
        writer.WriteString(stateKey);
        writer.WriteByte(10);
        writer.WriteBytes(origin);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Seals bounded distinct internal participant keys before handing any one to the SDK.</summary>
    internal static byte[] Keys(string[] keys)
    {
        if (keys.Length != 3 || keys.Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new InvalidOperationException("CheckpointHold: three distinct participant keys required.");
        }

        using EventEvolutionBinaryWriter writer = Writer("HX-EV-DAPR-CHECKPOINT-KEYS-1\0"u8, keys, 0);
        foreach (string key in keys)
        {
            if (!key.StartsWith("logical-checkpoint-v1:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("CheckpointHold: generated namespace key required.");
            }

            writer.WriteString(key);
        }

        return writer.ComputeSha256();
    }

    private static EventEvolutionBinaryWriter Writer(ReadOnlySpan<byte> separator, string[] texts, int fixedBytes)
    {
        int capacity = checked(separator.Length + fixedBytes);
        foreach (string text in texts)
        {
            capacity = checked(capacity + TextSize(text));
        }

        if (capacity > 64 * 1024)
        {
            throw new InvalidOperationException("CheckpointHold: encoded metadata exceeds 64 KiB.");
        }

        var writer = new EventEvolutionBinaryWriter(capacity);
        writer.WriteRaw(separator);
        return writer;
    }
}
