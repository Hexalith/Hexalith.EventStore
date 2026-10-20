using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;


using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Implements the approved binary source-v1 codec without normalizing stored bytes.</summary>
internal static class SourceProofCodec
{
    /// <summary>The only supported source codec version.</summary>
    internal const int Version = 1;

    /// <summary>Creates the existing scoped source-v1 seed.</summary>
    internal static SourceProof CreateSeed(AggregateIdentity identity, string incarnation)
        => FromNative(Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.CreateSeed(identity, incarnation));

    /// <summary>Hashes every persisted envelope field in its approved order.</summary>
    internal static string ComputeEventHash(EventEnvelope envelope)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceEventHashes);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "hexalith.projection.source.event.v1");
        AppendString(hash, envelope.MessageId);
        AppendString(hash, envelope.AggregateId);
        AppendString(hash, envelope.AggregateType);
        AppendString(hash, envelope.TenantId);
        AppendString(hash, envelope.Domain);
        AppendInt64(hash, envelope.SequenceNumber);
        AppendInt64(hash, envelope.GlobalPosition);
        AppendInt64(hash, envelope.Timestamp.UtcTicks);
        AppendInt32(hash, checked((int)envelope.Timestamp.Offset.TotalMinutes));
        AppendString(hash, envelope.CorrelationId);
        AppendString(hash, envelope.CausationId);
        AppendString(hash, envelope.UserId);
        AppendString(hash, envelope.DomainServiceVersion);
        AppendString(hash, envelope.EventTypeName);
        AppendInt32(hash, envelope.MetadataVersion);
        AppendString(hash, envelope.SerializationFormat);
        AppendBytes(hash, envelope.Payload);
        if (envelope.Extensions is null)
        {
            AppendInt32(hash, -1);
        }
        else
        {
            KeyValuePair<string, string>[] extensions = [.. envelope.Extensions
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)];
            if (extensions.Length != envelope.Extensions.Count
                || extensions.Select(static pair => pair.Key).Distinct(StringComparer.Ordinal).Count() != extensions.Length)
            {
                throw new ArgumentException("Duplicate source extension keys are invalid.", nameof(envelope));
            }

            AppendInt32(hash, extensions.Length);
            foreach (KeyValuePair<string, string> pair in extensions)
            {
                AppendString(hash, pair.Key);
                AppendString(hash, pair.Value);
            }
        }

        AppendString(hash, envelope.EventContractType);
        AppendInt32(hash, envelope.PayloadVersion.HasValue ? 1 : 0);
        if (envelope.PayloadVersion is int payloadVersion)
        {
            AppendInt32(hash, payloadVersion);
        }

        AppendString(hash, envelope.ApplicationPayloadDigest);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Extends a valid scoped proof by exactly one next stored envelope.</summary>
    internal static SourceProof Extend(SourceProof previous, EventEnvelope envelope)
        => ExtendVerified(previous, envelope, ComputeEventHash(envelope));

    /// <summary>Extends using the shared source-v1 step and the already measured raw-envelope hash.</summary>
    internal static SourceProof ExtendVerified(SourceProof previous, EventEnvelope envelope, string eventDigest)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceChainExtensions);
        return FromNative(Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.ExtendDigest(ToNative(previous),
            new(envelope.TenantId, envelope.Domain, envelope.AggregateId), envelope.SequenceNumber, envelope.MessageId, eventDigest));
    }

    /// <summary>Validates native metadata through the existing shared codec.</summary>
    internal static bool IsValid(SourceProof proof, AggregateIdentity identity, long head, long floor)
        => Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.IsValid(ToNative(proof), identity, head, floor);

    /// <summary>Copies native metadata without assigning a new revision or receipt.</summary>
    internal static Hexalith.EventStore.Contracts.Streams.NativeSourceProof ToNative(SourceProof proof)
        => new(proof.CodecVersion, proof.TenantId, proof.Domain, proof.AggregateId, proof.SourceIncarnation,
            proof.HeadSequence, proof.HeadMessageId, proof.CumulativeDigest, proof.RetainedFloor, proof.Erased);

    private static SourceProof FromNative(Hexalith.EventStore.Contracts.Streams.NativeSourceProof proof)
        => new(proof.CodecVersion, proof.TenantId, proof.Domain, proof.AggregateId, proof.SourceIncarnation,
            proof.HeadSequence, proof.HeadMessageId, proof.CumulativeDigest, proof.RetainedFloor, proof.Erased);

    /// <summary>Recognizes only an authenticated actor-owned empty seed, preserving rejection of unproven zero metadata.</summary>
    internal static bool IsQualifiedEmpty(AggregateMetadata metadata, AggregateIdentity identity)
        => !metadata.SourceErasurePending && metadata.CurrentSequence == 0 && metadata.RetainedFloor == 1 && metadata.SourceProof is { Erased: false } proof
            && IsValid(proof, identity, 0, 1) && proof == CreateSeed(identity, proof.SourceIncarnation);

    /// <summary>Compares the immutable completed prefix while retention remains a separately sampled source property.</summary>
    internal static bool SameCompletedPrefix(SourceProof? completed, SourceProof? observed)
        => completed is { Erased: false } && observed is { Erased: false }
            && completed.RetainedFloor >= 1 && completed.RetainedFloor - 1 <= completed.HeadSequence
            && completed with { RetainedFloor = observed.RetainedFloor } == observed;

    private static bool IsDigest(string? digest)
        => digest is { Length: 64 } && digest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void AppendString(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            AppendInt32(hash, -1);
            return;
        }

        AppendBytes(hash, Encoding.UTF8.GetBytes(value));
    }

    private static void AppendBytes(IncrementalHash hash, byte[] value)
    {
        AppendInt32(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
