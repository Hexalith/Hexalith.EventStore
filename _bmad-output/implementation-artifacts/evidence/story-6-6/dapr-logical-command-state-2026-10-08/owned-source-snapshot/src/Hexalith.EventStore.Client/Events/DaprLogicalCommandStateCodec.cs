using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Encodes exact command routes and strictly separate logical completed-state claims.</summary>
internal static class DaprLogicalCommandStateCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Hashes every command field with exact null/extension presence and privately supplied payload bytes.</summary>
    internal static byte[] CommandHash(CommandEnvelope command, EventBufferBudget budget, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        token.ThrowIfCancellationRequested();
        using EventBufferReservation workspace = budget.Reserve(4 * 1024 * 1024);
        using var writer = new EventEvolutionBinaryWriter(512 * 1024);
        writer.WriteRaw("HX-EV-DAPR-COMMAND-ROUTE-1\0"u8);
        writer.WriteByte(1);
        foreach (string value in new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId, command.CommandType })
        {
            writer.WriteString(value);
        }
        writer.WriteInt64(command.Payload.Length);
        writer.WriteHash(SHA256.HashData(command.Payload));
        writer.WriteString(command.CorrelationId);
        writer.WriteByte(command.CausationId is null ? (byte)0 : (byte)1);
        if (command.CausationId is not null)
        {
            writer.WriteString(command.CausationId);
        }
        writer.WriteString(command.UserId);
        writer.WriteByte(command.Extensions is null ? (byte)0 : (byte)1);
        if (command.Extensions is not null)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            int length = checked(writer.Length + 4);
            foreach (KeyValuePair<string, string> pair in command.Extensions)
            {
                token.ThrowIfCancellationRequested();
                length = checked(length + 8 + StrictUtf8.GetByteCount(pair.Key) + StrictUtf8.GetByteCount(pair.Value));
                if (pairs.Count == 65536 || length > 512 * 1024)
                {
                    throw new InvalidOperationException("MetadataLimit: command route exceeds its ceiling.");
                }
                pairs.Add(pair);
            }
            pairs.Sort(static (a, b) => CompareUtf8(a.Key, b.Key));
            writer.WriteUInt32(checked((uint)pairs.Count));
            for (int index = 0; index < pairs.Count; index++)
            {
                if (index > 0 && pairs[index - 1].Key == pairs[index].Key)
                {
                    throw new ArgumentException("Duplicate command extension key.");
                }
                writer.WriteString(pairs[index].Key);
                writer.WriteString(pairs[index].Value);
            }
        }
        token.ThrowIfCancellationRequested();
        return writer.ComputeSha256();
    }

    /// <summary>Encodes exactly 27 ascending mandatory purpose-07 fields under the distinct logical separator.</summary>
    internal static byte[] Encode(DaprLogicalCommandStateClaim claim)
    {
        Validate(claim);
        using var writer = new EventEvolutionBinaryWriter(DaprLogicalClaimCodec.MaximumClaimBytes);
        writer.WriteRaw("HX-EV-DAPR-COMMAND-STATE-1\0"u8);
        writer.WriteByte(1);
        writer.WriteUInt16(27);
        Text(writer, 1, DaprLogicalSourceBinding.ModelId);
        byte tag = 2;
        foreach (string text in new[] { claim.TenantId, claim.Domain, claim.AggregateId, claim.AggregateType, claim.CommandType, claim.MessageId })
        {
            Text(writer, tag++, text);
        }
        Hash(writer, 8, claim.CommandHash);
        Hash(writer, 9, claim.SourceBindingHash);
        Number(writer, 10, claim.ActorHead);
        Number(writer, 11, claim.TargetSequence);
        Hash(writer, 12, claim.RegistryFingerprint);
        Hash(writer, 13, claim.ReconstructionBindingHash);
        Text(writer, 14, claim.OperationId);
        Text(writer, 15, claim.OwnerId);
        Number(writer, 16, claim.Generation);
        Number(writer, 17, claim.PageOrdinal);
        Number(writer, 18, claim.CompletedSequence);
        tag = 19;
        foreach (ReadOnlyMemory<byte> value in Hashes(claim).Skip(4))
        {
            Hash(writer, tag++, value);
        }
        writer.WriteByte(26);
        writer.WriteTimestamp(claim.IssuedAt);
        writer.WriteByte(27);
        writer.WriteTimestamp(claim.ExpiresAt);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Rejects historical purpose meanings, wrong/repeated/reordered fields, malformed terminal forms and trailing bytes.</summary>
    internal static DaprLogicalCommandStateClaim Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > DaprLogicalClaimCodec.MaximumClaimBytes)
        {
            throw new InvalidOperationException("ProofLimit: command claim exceeds 1 MiB.");
        }
        var reader = new EventEvolutionBinaryReader(bytes);
        if (!reader.ReadRaw("HX-EV-DAPR-COMMAND-STATE-1\0"u8.Length).SequenceEqual("HX-EV-DAPR-COMMAND-STATE-1\0"u8)
            || reader.ReadByte() != 1 || reader.ReadUInt16() != 27 || Text(ref reader, 1) != DaprLogicalSourceBinding.ModelId)
        {
            throw new ArgumentException("Logical command-state model, codec or field count mismatch.");
        }
        string tenant = Text(ref reader, 2), domain = Text(ref reader, 3), aggregate = Text(ref reader, 4), type = Text(ref reader, 5);
        string command = Text(ref reader, 6), message = Text(ref reader, 7);
        byte[] route = Hash(ref reader, 8), source = Hash(ref reader, 9);
        long head = Number(ref reader, 10), target = Number(ref reader, 11);
        byte[] registry = Hash(ref reader, 12), binding = Hash(ref reader, 13);
        string operation = Text(ref reader, 14), owner = Text(ref reader, 15);
        long generation = Number(ref reader, 16), ordinal = Number(ref reader, 17), completed = Number(ref reader, 18);
        byte[] accumulator = Hash(ref reader, 19), prefix = Hash(ref reader, 20), proof = Hash(ref reader, 21);
        byte[] response = Hash(ref reader, 22), state = Hash(ref reader, 23), effective = Hash(ref reader, 24), transcript = Hash(ref reader, 25);
        Tag(ref reader, 26);
        DateTimeOffset issued = reader.ReadTimestamp();
        Tag(ref reader, 27);
        DateTimeOffset expires = reader.ReadTimestamp();
        reader.RequireEnd();
        var claim = new DaprLogicalCommandStateClaim(tenant, domain, aggregate, type, command, message, route, source, head, target,
            registry, binding, operation, owner, generation, ordinal, completed, accumulator, prefix, proof, response, state,
            effective, transcript, issued, expires);
        Validate(claim);
        return claim;
    }

    private static void Validate(DaprLogicalCommandStateClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var identity = new AggregateIdentity(claim.TenantId, claim.Domain, claim.AggregateId);
        if (identity.TenantId != claim.TenantId || identity.Domain != claim.Domain || identity.AggregateId != claim.AggregateId
            || claim.ActorHead < 0 || claim.TargetSequence < 0 || claim.TargetSequence > claim.ActorHead
            || claim.CompletedSequence != claim.TargetSequence || claim.Generation < 1 || claim.PageOrdinal is < 1 or > 65536
            || (claim.TargetSequence == 0 ? claim.PageOrdinal != 1 : claim.PageOrdinal > claim.TargetSequence)
            || claim.IssuedAt >= claim.ExpiresAt || claim.ExpiresAt - claim.IssuedAt > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentException("Invalid completed logical command-state scalar or validity form.");
        }
        foreach (string text in new[] { claim.AggregateType, claim.CommandType, claim.MessageId, claim.OperationId, claim.OwnerId })
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
        }
        if (StrictUtf8.GetByteCount(claim.OperationId) > 256 || StrictUtf8.GetByteCount(claim.OwnerId) > 256)
        {
            throw new ArgumentException("Logical command-state owner identity exceeds its ceiling.");
        }
        foreach (ReadOnlyMemory<byte> hash in Hashes(claim))
        {
            if (hash.Length != 32)
            {
                throw new ArgumentException("Logical command-state digest must be B32.");
            }
        }
    }

    private static ReadOnlyMemory<byte>[] Hashes(DaprLogicalCommandStateClaim claim) => [claim.CommandHash, claim.SourceBindingHash,
        claim.RegistryFingerprint, claim.ReconstructionBindingHash, claim.Accumulator, claim.FinalPrefixHash, claim.FinalSourceProofHash,
        claim.FinalResponseHash, claim.CanonicalStateHash, claim.EffectiveChainHash, claim.TranscriptHash];
    private static int CompareUtf8(string a, string b)
    {
        StringRuneEnumerator left = a.EnumerateRunes(), right = b.EnumerateRunes();
        while (left.MoveNext())
        {
            if (!right.MoveNext())
            {
                return 1;
            }
            int order = left.Current.Value.CompareTo(right.Current.Value);
            if (order != 0)
            {
                return order;
            }
        }
        return right.MoveNext() ? -1 : 0;
    }
    private static void Tag(ref EventEvolutionBinaryReader reader, byte tag)
    {
        if (reader.ReadByte() != tag)
        {
            throw new ArgumentException("Missing, duplicate or reordered command-state field.");
        }
    }
    private static void Text(EventEvolutionBinaryWriter writer, byte tag, string value)
    {
        writer.WriteByte(tag);
        writer.WriteString(value);
    }
    private static string Text(ref EventEvolutionBinaryReader reader, byte tag)
    {
        Tag(ref reader, tag);
        return reader.ReadString(512 * 1024);
    }
    private static void Hash(EventEvolutionBinaryWriter writer, byte tag, ReadOnlyMemory<byte> value)
    {
        writer.WriteByte(tag);
        writer.WriteHash(value.Span);
    }
    private static byte[] Hash(ref EventEvolutionBinaryReader reader, byte tag)
    {
        Tag(ref reader, tag);
        return reader.ReadHash().ToArray();
    }
    private static void Number(EventEvolutionBinaryWriter writer, byte tag, long value)
    {
        writer.WriteByte(tag);
        writer.WriteInt64(value);
    }
    private static long Number(ref EventEvolutionBinaryReader reader, byte tag)
    {
        Tag(ref reader, tag);
        return reader.ReadInt64();
    }
}
