using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using ByteAether.Ulid;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Implements the approved binary source-v1 codec without normalizing stored bytes.</summary>
internal static class SourceProofCodec
{
    /// <summary>The only supported source codec version.</summary>
    internal const int Version = 1;

    /// <summary>Creates the scoped chain seed for a new stream incarnation.</summary>
    internal static SourceProof CreateSeed(AggregateIdentity identity, string incarnation)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!Ulid.TryParse(incarnation, null, out _))
        {
            throw new ArgumentException("The source incarnation must be a ULID.", nameof(incarnation));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "hexalith.projection.source.seed.v1");
        AppendString(hash, identity.TenantId);
        AppendString(hash, identity.Domain);
        AppendString(hash, identity.AggregateId);
        AppendString(hash, incarnation);
        return new(Version, identity.TenantId, identity.Domain, identity.AggregateId,
            incarnation, 0, null, Convert.ToHexStringLower(hash.GetHashAndReset()), 1, false);
    }

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

    /// <summary>Extends using an already measured raw-envelope digest, avoiding a duplicate source hash.</summary>
    internal static SourceProof ExtendVerified(SourceProof previous, EventEnvelope envelope, string eventDigest)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(envelope);
        var identity = new AggregateIdentity(envelope.TenantId, envelope.Domain, envelope.AggregateId);
        if (!IsValid(previous, identity, previous.HeadSequence, previous.RetainedFloor)
            || previous.Erased || envelope.SequenceNumber != checked(previous.HeadSequence + 1)
            || string.IsNullOrWhiteSpace(envelope.MessageId) || !IsDigest(eventDigest))
        {
            throw new ArgumentException("The source envelope does not extend the proven contiguous stream.", nameof(envelope));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "hexalith.projection.source.step.v1");
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceChainExtensions);
        hash.AppendData(Convert.FromHexString(previous.CumulativeDigest));
        hash.AppendData(Convert.FromHexString(eventDigest));
        return previous with
        {
            HeadSequence = envelope.SequenceNumber,
            HeadMessageId = envelope.MessageId,
            CumulativeDigest = Convert.ToHexStringLower(hash.GetHashAndReset()),
        };
    }

    /// <summary>Validates metadata evidence without reading any event envelope.</summary>
    internal static bool IsValid(SourceProof proof, AggregateIdentity identity, long head, long floor)
        => proof.CodecVersion == Version && proof.HeadSequence == head && head >= 0
            && floor >= 1 && floor - 1 <= head && proof.RetainedFloor == floor
            && string.Equals(proof.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(proof.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(proof.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            && Ulid.TryParse(proof.SourceIncarnation, null, out _)
            && IsDigest(proof.CumulativeDigest)
            && (head == 0 ? proof.HeadMessageId is null : !string.IsNullOrWhiteSpace(proof.HeadMessageId));

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
