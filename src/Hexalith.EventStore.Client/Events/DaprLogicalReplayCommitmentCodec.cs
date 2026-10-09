using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Extends complete verified effective history and exact operation page transcripts from their bound genesis.</summary>
internal static class DaprLogicalReplayCommitmentCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Creates the source/registry/reconstruction-bound effective genesis.</summary>
    internal static byte[] EffectiveGenesis(ReadOnlyMemory<byte> source, ReadOnlyMemory<byte> registry, ReadOnlyMemory<byte>? binding,
        EventBufferBudget? budget = null)
    {
        using EventBufferReservation working = (budget ?? new EventBufferBudget()).Reserve(1280);
        using var writer = new EventEvolutionBinaryWriter(512);
        writer.WriteRaw("HX-EV-DAPR-EFFECTIVE-GENESIS-1\0"u8);
        writer.WriteByte(1);
        Scope(writer, source, registry, binding);
        return writer.ComputeSha256();
    }

    /// <summary>Measures future proof, decoded-string and writer capacity without charging raw payload bytes twice.</summary>
    internal static int GetPreparationCapacity(ReadOnlySpan<byte> response, CancellationToken token, bool anchored = false)
    {
        int strings = GetResponseStringCapacity(response, token, anchored);
        var reader = new EventEvolutionBinaryReader(response);
        _ = reader.ReadRaw(anchored ? "HX-EV-DAPR-ANCHORED-PAGE-1\0"u8.Length : "HX-EV-DAPR-REPLAY-PAGE-1\0"u8.Length);
        _ = reader.ReadByte();
        int proof = reader.ReadBytes(2 * 1024 * 1024).Length;
        return checked(256 * 1024 + 12 * proof + 3 * strings);
    }

    /// <summary>Extends only after every actual response route and effective payload has passed the shared verifier.</summary>
    internal static byte[] EffectiveSuccessor(ReadOnlySpan<byte> response, DaprLogicalSourceBinding source,
        DaprLogicalClaimTrust trust, ReadOnlyMemory<byte> previousAccumulator, ReadOnlyMemory<byte> previousEffective,
        ReadOnlyMemory<byte>? binding, EventBufferBudget budget, CancellationToken token, DaprLogicalAnchoredIntake? anchored = null)
    {
        using EventBufferReservation strings = budget.Reserve(GetResponseStringCapacity(response, token, anchored is not null));
        using DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> verified = PrivateLogicalReplayResponseVerifier.Verify(
            response, source, trust, previousAccumulator, budget, token, anchored);
        byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(source, budget);
        var reader = new EventEvolutionBinaryReader(response);
        _ = reader.ReadRaw(anchored is null ? "HX-EV-DAPR-REPLAY-PAGE-1\0"u8.Length : "HX-EV-DAPR-ANCHORED-PAGE-1\0"u8.Length);
        _ = reader.ReadByte();
        using UnverifiedEventEvolutionProofFrame proof = EventEvolutionProofFraming.Capture(reader.ReadBytes(2 * 1024 * 1024),
            budget, true, false, token);
        int count = checked((int)reader.ReadUInt32());
        byte[] chain = previousEffective.ToArray();
        if (chain.Length != 32)
        {
            throw new ArgumentException("Effective predecessor must be B32.");
        }
        for (int index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            long sequence = reader.ReadInt64();
            string type = reader.ReadString(64);
            int version = reader.ReadInt32();
            string format = reader.ReadString(512 * 1024);
            ReadOnlySpan<byte> payload = reader.ReadBytes(64 * 1024 * 1024);
            chain = EffectiveStep(sourceHash, trust.RegistryFingerprint, binding, chain, sequence,
                SHA256.HashData(proof.RouteClaim(index)), type, version, format, SHA256.HashData(payload), budget,
                anchored is null ? (ReadOnlyMemory<byte>?)null : anchored.SelectionHash);
        }

        reader.RequireEnd();
        trust.RequireCurrent(token);
        anchored?.Trust.RequireCurrent(trust, token);
        return chain;
    }

    /// <summary>Hashes the exact independently specified effective-event step.</summary>
    internal static byte[] EffectiveStep(ReadOnlyMemory<byte> source, ReadOnlyMemory<byte> registry, ReadOnlyMemory<byte>? binding,
        ReadOnlyMemory<byte> previous, long sequence, ReadOnlyMemory<byte> routeHash, string type, int version,
        string format, ReadOnlyMemory<byte> payloadHash, EventBufferBudget? budget = null, ReadOnlyMemory<byte>? selectionHash = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (version is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }
        int capacity = checked(512 + StrictUtf8.GetByteCount(type) + StrictUtf8.GetByteCount(format));
        using EventBufferReservation working = (budget ?? new EventBufferBudget()).Reserve(checked(capacity * 2 + 256));
        using var writer = new EventEvolutionBinaryWriter(capacity);
        writer.WriteRaw(selectionHash.HasValue ? "HX-EV-DAPR-ANCHORED-EFFECTIVE-STEP-1\0"u8 : "HX-EV-DAPR-EFFECTIVE-STEP-1\0"u8);
        writer.WriteByte(1);
        if (selectionHash.HasValue)
        {
            writer.WriteHash(selectionHash.Value.Span);
        }
        Scope(writer, source, registry, binding, selectionHash.HasValue);
        writer.WriteHash(previous.Span);
        writer.WriteInt64(sequence);
        writer.WriteHash(routeHash.Span);
        writer.WriteString(type);
        writer.WriteInt32(version);
        writer.WriteString(format);
        writer.WriteHash(payloadHash.Span);
        return writer.ComputeSha256();
    }

    /// <summary>Creates the stable operation transcript genesis, including initial state and optional exact command.</summary>
    internal static byte[] TranscriptGenesis(string tenant, string operation, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> registry, ReadOnlyMemory<byte>? binding, ReadOnlyMemory<byte>? state, ReadOnlyMemory<byte>? command,
        EventBufferBudget? budget = null)
    {
        using EventBufferReservation working = (budget ?? new EventBufferBudget()).Reserve(8192 + 256);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteRaw("HX-EV-DAPR-TRANSCRIPT-GENESIS-1\0"u8);
        writer.WriteByte(1);
        writer.WriteString(tenant);
        writer.WriteString(operation);
        Scope(writer, source, registry, binding);
        OptionalHash(writer, state);
        OptionalHash(writer, command);
        return writer.ComputeSha256();
    }

    /// <summary>Extends the exact scalar ledger and pinned response/state commitments without self-reference.</summary>
    internal static byte[] TranscriptStep(string tenant, string operation, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> registry, ReadOnlyMemory<byte>? binding, ReadOnlyMemory<byte> previous, DaprLogicalPageTranscriptEntry entry,
        EventBufferBudget? budget = null)
    {
        using EventBufferReservation working = (budget ?? new EventBufferBudget()).Reserve(20 * 1024);
        byte[] bytes = EncodeTranscriptEntry(entry);
        try
        {
            using var writer = new EventEvolutionBinaryWriter(8192);
            writer.WriteRaw("HX-EV-DAPR-TRANSCRIPT-STEP-1\0"u8);
            writer.WriteByte(1);
            writer.WriteString(tenant);
            writer.WriteString(operation);
            Scope(writer, source, registry, binding);
            writer.WriteHash(previous.Span);
            writer.WriteBytes(bytes);
            return writer.ComputeSha256();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Binds the covered transcript seed to this new actual anchored operation and canonical initial state.</summary>
    internal static byte[] AnchoredTranscriptGenesis(string tenant, string operation, ReadOnlyMemory<byte> selection,
        ReadOnlyMemory<byte> coveredSeed, ReadOnlyMemory<byte> stateHash, EventBufferBudget budget)
    {
        using EventBufferReservation working = budget.Reserve(8192 + 256);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteRaw("HX-EV-DAPR-ANCHORED-TRANSCRIPT-GENESIS-1\0"u8);
        writer.WriteByte(1);
        writer.WriteString(tenant); writer.WriteString(operation);
        writer.WriteHash(selection.Span); writer.WriteHash(coveredSeed.Span); writer.WriteHash(stateHash.Span);
        return writer.ComputeSha256();
    }

    /// <summary>Extends the exact anchored page transcript under its immutable selection and actual operation identity.</summary>
    internal static byte[] AnchoredTranscriptStep(string tenant, string operation, ReadOnlyMemory<byte> selection,
        ReadOnlyMemory<byte> previous, DaprLogicalPageTranscriptEntry entry, EventBufferBudget budget)
    {
        using EventBufferReservation working = budget.Reserve(20 * 1024);
        byte[] bytes = EncodeTranscriptEntry(entry);
        try
        {
            using var writer = new EventEvolutionBinaryWriter(8192);
            writer.WriteRaw("HX-EV-DAPR-ANCHORED-TRANSCRIPT-STEP-1\0"u8);
            writer.WriteByte(1);
            writer.WriteString(tenant); writer.WriteString(operation);
            writer.WriteHash(selection.Span); writer.WriteHash(previous.Span); writer.WriteBytes(bytes);
            return writer.ComputeSha256();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Hashes the separately selected exact anchored page request, including immutable selection identity.</summary>
    internal static byte[] AnchoredRequestHash(string tenant, string operation, string owner, long generation,
        long ordinal, string request, int count, ReadOnlyMemory<byte> selection, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> registry, EventBufferBudget budget)
    {
        using EventBufferReservation working = budget.Reserve(8192 + 256);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteRaw("HX-EV-DAPR-ANCHORED-PAGE-REQUEST-1\0"u8);
        writer.WriteByte(1); writer.WriteHash(selection.Span);
        writer.WriteString(tenant); writer.WriteString(operation); writer.WriteString(owner);
        writer.WriteInt64(generation); writer.WriteInt64(ordinal); writer.WriteString(request);
        writer.WriteInt32(count); writer.WriteHash(source.Span); writer.WriteHash(registry.Span);
        return writer.ComputeSha256();
    }

    /// <summary>Encodes the independent page transcript vector preimage.</summary>
    internal static byte[] EncodeTranscriptEntry(DaprLogicalPageTranscriptEntry entry)
    {
        if (entry.PageOrdinal is < 1 or > 65536 || entry.Generation < 1 || entry.Count is < 0 or > 256
            || entry.StartSequence < 1 || entry.EndSequence < 0)
        {
            throw new ArgumentException("Invalid transcript range.");
        }
        using var writer = new EventEvolutionBinaryWriter(1024);
        writer.WriteRaw("HX-EV-DAPR-PAGE-TRANSCRIPT-1\0"u8);
        writer.WriteByte(1);
        writer.WriteInt64(entry.PageOrdinal);
        writer.WriteInt64(entry.Generation);
        writer.WriteHash(entry.RequestHash.Span);
        writer.WriteHash(entry.PreviousAccumulator.Span);
        writer.WriteHash(entry.Accumulator.Span);
        writer.WriteInt64(entry.StartSequence);
        writer.WriteInt64(entry.EndSequence);
        writer.WriteInt32(entry.Count);
        writer.WriteHash(entry.ResponseHash.Span);
        writer.WriteByte(entry.IsFinal ? (byte)1 : (byte)0);
        OptionalHash(writer, entry.PriorStateHash);
        OptionalHash(writer, entry.CanonicalStateHash);
        writer.WriteHash(entry.PreviousEffectiveChain.Span);
        writer.WriteHash(entry.EffectiveChain.Span);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Reads exact final prefix and proof-frame digests after the complete response has been verified.</summary>
    internal static (byte[] Prefix, byte[] Proof) FinalProofHashes(ReadOnlySpan<byte> response, EventBufferBudget budget, CancellationToken token)
    {
        var reader = new EventEvolutionBinaryReader(response);
        if (!reader.ReadRaw("HX-EV-DAPR-REPLAY-PAGE-1\0"u8.Length).SequenceEqual("HX-EV-DAPR-REPLAY-PAGE-1\0"u8)
            || reader.ReadByte() != 1)
        {
            throw new ArgumentException("Invalid logical response framing.");
        }
        ReadOnlySpan<byte> bytes = reader.ReadBytes(2 * 1024 * 1024);
        using UnverifiedEventEvolutionProofFrame proof = EventEvolutionProofFraming.Capture(bytes, budget, true, false, token);
        return (SHA256.HashData(proof.PrefixClaim), SHA256.HashData(bytes));
    }

    private static void Scope(EventEvolutionBinaryWriter writer, ReadOnlyMemory<byte> source,
        ReadOnlyMemory<byte> registry, ReadOnlyMemory<byte>? binding, bool anchored = false)
    {
        writer.WriteHash(source.Span);
        writer.WriteString(anchored ? DaprLogicalReplayAnchorCodec.ModelId : DaprLogicalSourceBinding.ModelId);
        writer.WriteHash(registry.Span);
        OptionalHash(writer, binding);
    }

    /// <summary>Writes exact optional B32 presence without treating absence as an empty hash.</summary>
    internal static void OptionalHash(EventEvolutionBinaryWriter writer, ReadOnlyMemory<byte>? value)
    {
        writer.WriteByte(value.HasValue ? (byte)1 : (byte)0);
        if (value.HasValue)
        {
            writer.WriteHash(value.Value.Span);
        }
    }

    private static int GetResponseStringCapacity(ReadOnlySpan<byte> response, CancellationToken token, bool anchored = false)
    {
        if (response.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical response exceeds its ceiling.");
        }
        var reader = new EventEvolutionBinaryReader(response);
        ReadOnlySpan<byte> separator = anchored ? "HX-EV-DAPR-ANCHORED-PAGE-1\0"u8 : "HX-EV-DAPR-REPLAY-PAGE-1\0"u8;
        if (!reader.ReadRaw(separator.Length).SequenceEqual(separator)
            || reader.ReadByte() != 1)
        {
            throw new ArgumentException("Invalid logical response framing.");
        }
        _ = reader.ReadBytes(2 * 1024 * 1024);
        uint count = reader.ReadUInt32();
        if (count > 256)
        {
            throw new InvalidOperationException("ProofLimit: effective commitment admits at most 256 events.");
        }
        int capacity = 8192;
        for (int index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            _ = reader.ReadInt64();
            capacity = checked(capacity + 2 * StrictUtf8.GetCharCount(reader.ReadBytes(64)) + 256);
            _ = reader.ReadInt32();
            capacity = checked(capacity + 2 * StrictUtf8.GetCharCount(reader.ReadBytes(512 * 1024)) + 256);
            _ = reader.ReadBytes(64 * 1024 * 1024);
        }

        reader.RequireEnd();
        token.ThrowIfCancellationRequested();
        return capacity;
    }
}
