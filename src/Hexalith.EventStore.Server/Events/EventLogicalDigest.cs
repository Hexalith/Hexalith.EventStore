using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Computes an application digest over exact logical payload bytes and retained event metadata.</summary>
/// <remarks>This digest is not a provider signature or a physical database representation claim.</remarks>
internal static class EventLogicalDigest
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Hashes the logical payload once before optional payload protection.</summary>
    internal static byte[] HashPayload(ReadOnlySpan<byte> payload) => SHA256.HashData(payload);

    /// <summary>Binds the payload hash to addressed identity, message lineage, type, version and original format.</summary>
    internal static string Compute(EventEnvelope envelope, string applicationFormat, ReadOnlySpan<byte> payloadHash)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationFormat);
        if (payloadHash.Length != 32)
        {
            throw new ArgumentException("A logical payload hash must be SHA-256.", nameof(payloadHash));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Frame(hash, "eventstore.logical-payload.v1");
        Frame(hash, envelope.TenantId);
        Frame(hash, envelope.Domain);
        Frame(hash, envelope.AggregateId);
        Frame(hash, envelope.MessageId);
        Frame(hash, envelope.CorrelationId);
        Frame(hash, envelope.CausationId);
        Frame(hash, envelope.EventTypeName);
        Frame(hash, applicationFormat);
        Frame(hash, envelope.EventContractType ?? string.Empty);
        Span<byte> number = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(number, envelope.SequenceNumber);
        hash.AppendData(number);
        BinaryPrimitives.WriteInt32BigEndian(number, envelope.MetadataVersion);
        hash.AppendData(number[..4]);
        BinaryPrimitives.WriteInt32BigEndian(number, envelope.PayloadVersion ?? 0);
        hash.AppendData(number[..4]);
        hash.AppendData(payloadHash);
        byte[] digest = hash.GetHashAndReset();
        try
        {
            return Convert.ToHexStringLower(digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void Frame(IncrementalHash hash, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = StrictUtf8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        try
        {
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
