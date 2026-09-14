using System.Buffers;
using System.Buffers.Binary;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Writes the injective eleven-field HXAD version 1 record from normative section 7.1.
/// </summary>
internal static class AadCodec {
    /// <summary>
    /// Encodes authenticated identity and envelope fields into canonical AAD.
    /// </summary>
    internal static byte[] Write(
        PayloadProtectionContext context,
        string propertyPath,
        string keyReference,
        uint dekVersion,
        uint fieldOrdinal,
        ReadOnlySpan<byte> manifestCommitment) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Identity);
        if (propertyPath is null
            || !Enum.IsDefined(context.PayloadKind)
            || !CanonicalUlid.IsValid(keyReference)
            || dekVersion == 0
            || fieldOrdinal >= PayloadProtectionLimits.ProtectedPaths
            || manifestCommitment.Length != 32) {
            throw new PayloadProtectionFormatException();
        }

        bool snapshot = context.PayloadKind == PayloadProtectionPayloadKind.Snapshot;
        if (snapshot != (propertyPath.Length == 0)) {
            throw new PayloadProtectionFormatException();
        }

        _ = JsonPointer.Decode(propertyPath, allowRoot: snapshot);

        byte[] tenant = CanonicalText.Encode(context.Identity.TenantId, 1, 256);
        byte[] domain = CanonicalText.Encode(context.Identity.Domain, 1, 128);
        byte[] aggregate = CanonicalText.Encode(context.Identity.AggregateId, 1, 256);
        byte[] payloadType = CanonicalText.Encode(context.PayloadTypeId, 1, 1024);
        byte[] path = CanonicalText.Encode(propertyPath, snapshot ? 0 : 1, 2048);
        byte[] key = CanonicalText.Encode(keyReference, 26, 26);
        byte[] format = "json+pdenc-v2"u8.ToArray();
        Span<byte> version = stackalloc byte[4];
        Span<byte> ordinal = stackalloc byte[4];
        Span<byte> sequence = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(version, dekVersion);
        BinaryPrimitives.WriteUInt32BigEndian(ordinal, fieldOrdinal);
        BinaryPrimitives.WriteUInt64BigEndian(sequence, context.RecordSequence);

        var writer = new ArrayBufferWriter<byte>();
        Write(writer, "HXAD"u8);
        Write(writer, [(byte)1, (byte)context.PayloadKind, (byte)11, (byte)0]);
        WriteField(writer, 1, 1, tenant);
        WriteField(writer, 2, 1, domain);
        WriteField(writer, 3, 1, aggregate);
        WriteField(writer, 4, 1, payloadType);
        WriteField(writer, 5, 1, path);
        WriteField(writer, 6, 1, key);
        WriteField(writer, 7, 2, version);
        WriteField(writer, 8, 1, format);
        WriteField(writer, 9, 2, ordinal);
        WriteField(writer, 10, 3, sequence);
        WriteField(writer, 11, 4, manifestCommitment);
        if (writer.WrittenCount > PayloadProtectionLimits.AadBytes) {
            throw new PayloadProtectionFormatException();
        }

        return writer.WrittenSpan.ToArray();
    }

    private static void WriteField(ArrayBufferWriter<byte> writer, byte identifier, byte type, ReadOnlySpan<byte> value) {
        Span<byte> header = stackalloc byte[6];
        header[0] = identifier;
        header[1] = type;
        BinaryPrimitives.WriteUInt32BigEndian(header[2..], checked((uint)value.Length));
        Write(writer, header);
        Write(writer, value);
    }

    private static void Write(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> value) {
        value.CopyTo(writer.GetSpan(value.Length));
        writer.Advance(value.Length);
    }
}
