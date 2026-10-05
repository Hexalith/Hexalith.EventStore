using System.Buffers;
using System.Text;

using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService;

/// <summary>Admits the complete V1 response before any bytes reach the HTTP response.</summary>
internal static class BoundedV1WireResultAdmission
{
    private const long MaximumResultBytes = 128L * 1024 * 1024;

    /// <summary>Snapshots event references and checks the exact renderer framing and string lengths.</summary>
    internal static DomainServiceWireResult Admit(DomainServiceWireResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DomainServiceWireEvent> source = result.Events
            ?? throw new InvalidOperationException("CapabilityMismatch: a V1 response requires its event collection.");
        int count = source.Count;
        if (count is < 0 or > 1000
            || result.WriterMode is not null || result.RegistryFingerprint is not null)
        {
            throw new InvalidOperationException("CapabilityMismatch: this renderer supports only the selected implicit V1 result.");
        }

        var events = new DomainServiceWireEvent[count];
        // Framing: {"isRejection":false,"events":[],"resultPayload":null}
        long encoded = "{\"isRejection\":"u8.Length + (result.IsRejection ? 4 : 5)
            + ",\"events\":["u8.Length + "],\"resultPayload\":"u8.Length + 1
            + (result.ResultPayload is null ? 4 : StringLength(result.ResultPayload, cancellationToken));
        RequireResultCapacity(encoded);
        for (int index = 0; index < events.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DomainServiceWireEvent item = source[index]
                ?? throw new InvalidOperationException("CapabilityMismatch: a V1 response contains a null event.");
            if (item.MetadataVersion is not null || item.EventContractType is not null || item.PayloadVersion is not null
                || item.Payload is null || string.IsNullOrEmpty(item.EventTypeName) || string.IsNullOrEmpty(item.SerializationFormat))
            {
                throw new InvalidOperationException("CapabilityMismatch: the V1 renderer requires unversioned payloads with an exact alias and format.");
            }
            if (item.Payload.Length > 64 * 1024 * 1024)
            {
                throw new InvalidOperationException("PayloadLimit: a V1 response payload exceeds 64 MiB.");
            }

            long metadata = "{\"eventTypeName\":"u8.Length + StringLength(item.EventTypeName, cancellationToken)
                + ",\"payload\":\""u8.Length + "\",\"serializationFormat\":"u8.Length
                + StringLength(item.SerializationFormat, cancellationToken) + 1;
            if (metadata > 512 * 1024)
            {
                throw new InvalidOperationException("MetadataLimit: encoded V1 event metadata exceeds 512 KiB.");
            }

            encoded = checked(encoded + (index == 0 ? 0 : 1) + metadata + 4L * ((item.Payload.Length + 2L) / 3));
            RequireResultCapacity(encoded);
            events[index] = item;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result with { Events = events };
    }

    private static long StringLength(string value, CancellationToken cancellationToken)
    {
        long length = 2;
        for (int offset = 0; offset < value.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(value.AsSpan(offset), out Rune rune, out int consumed) != OperationStatus.Done)
            {
                throw new ArgumentException("Invalid Unicode cannot enter the V1 renderer.");
            }

            offset += consumed;
            length = checked(length + (rune.Value is 34 or 92 ? 2 : rune.Value < 32 ? 6 : rune.Utf8SequenceLength));
            RequireResultCapacity(length);
        }

        return length;
    }

    private static void RequireResultCapacity(long encoded)
    {
        if (encoded > MaximumResultBytes)
        {
            throw new InvalidOperationException("ResultLimit: complete encoded V1 output exceeds 128 MiB.");
        }
    }
}
