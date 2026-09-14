using System.Buffers.Binary;
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Writes the injective eleven-field HXAD version 1 record from normative section 7.1.
/// </summary>
internal static class AadCodec
{
    private static ReadOnlySpan<byte> Format => "json+pdenc-v2"u8;

    /// <summary>
    /// Validates every context-derived AAD source without allocating the final AAD record.
    /// </summary>
    internal static void ValidateContext(PayloadProtectionContext context, PayloadProtectionPayloadKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Identity);
        if (context.PayloadKind != expectedKind || !Enum.IsDefined(context.PayloadKind))
        {
            throw new PayloadProtectionFormatException();
        }

        _ = CanonicalText.GetByteCount(context.Identity.TenantId, 1, 256);
        _ = CanonicalText.GetByteCount(context.Identity.Domain, 1, 128);
        _ = CanonicalText.GetByteCount(context.Identity.AggregateId, 1, 256);
        if (expectedKind == PayloadProtectionPayloadKind.Snapshot)
        {
            ValidateSnapshotTypeId(context.PayloadTypeId);
        }
        else
        {
            _ = CanonicalText.GetByteCount(context.PayloadTypeId, 1, 1024);
        }
    }

    /// <summary>
    /// Validates every source and computes the complete AAD length before key lookup or allocation.
    /// </summary>
    internal static int Validate(
        PayloadProtectionContext context,
        string propertyPath,
        string keyReference,
        uint dekVersion,
        uint fieldOrdinal,
        ReadOnlySpan<byte> manifestCommitment,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Identity);
        if (!CanonicalUlid.IsValid(keyReference) || dekVersion == 0)
        {
            throw new PayloadProtectionFormatException();
        }

        return ValidateBeforeMaterial(
            context,
            propertyPath,
            fieldOrdinal,
            manifestCommitment,
            cancellationToken,
            checkpoint);
    }

    /// <summary>
    /// Validates every predictable AAD source and total length before material creation or lookup.
    /// </summary>
    internal static int ValidateBeforeMaterial(
        PayloadProtectionContext context,
        string propertyPath,
        uint fieldOrdinal,
        ReadOnlySpan<byte> manifestCommitment,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Identity);
        if (propertyPath is null
            || !Enum.IsDefined(context.PayloadKind)
            || fieldOrdinal >= PayloadProtectionLimits.ProtectedPaths
            || manifestCommitment.Length != 32)
        {
            throw new PayloadProtectionFormatException();
        }

        bool snapshot = context.PayloadKind == PayloadProtectionPayloadKind.Snapshot;
        ValidateContext(context, context.PayloadKind);
        if (snapshot != (propertyPath.Length == 0))
        {
            throw new PayloadProtectionFormatException();
        }

        _ = JsonPointer.Decode(
            propertyPath,
            allowRoot: snapshot,
            cancellationToken,
            checkpoint);
        int valuesLength = checked(
            CanonicalText.GetByteCount(context.Identity.TenantId, 1, 256)
            + CanonicalText.GetByteCount(context.Identity.Domain, 1, 128)
            + CanonicalText.GetByteCount(context.Identity.AggregateId, 1, 256)
            + CanonicalText.GetByteCount(context.PayloadTypeId, snapshot ? 16 : 1, snapshot ? 128 : 1024)
            + CanonicalText.GetByteCount(propertyPath, snapshot ? 0 : 1, PayloadProtectionLimits.PathBytes)
            + PayloadProtectionLimits.KeyReferenceBytes
            + 4
            + Format.Length
            + 4
            + 8
            + manifestCommitment.Length);
        int totalLength = checked(8 + (11 * 6) + valuesLength);
        if (totalLength > PayloadProtectionLimits.AadBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        return totalLength;
    }

    /// <summary>
    /// Encodes authenticated identity and envelope fields into canonical AAD.
    /// </summary>
    internal static byte[] Write(
        PayloadProtectionContext context,
        string propertyPath,
        string keyReference,
        uint dekVersion,
        uint fieldOrdinal,
        ReadOnlySpan<byte> manifestCommitment,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null)
    {
        int totalLength = Validate(
            context,
            propertyPath,
            keyReference,
            dekVersion,
            fieldOrdinal,
            manifestCommitment,
            cancellationToken,
            checkpoint);
        byte[]? tenant = null;
        byte[]? domain = null;
        byte[]? aggregate = null;
        byte[]? payloadType = null;
        byte[]? path = null;
        byte[]? key = null;
        byte[]? result = null;
        try
        {
            tenant = CanonicalText.Encode(context.Identity.TenantId, 1, 256);
            domain = CanonicalText.Encode(context.Identity.Domain, 1, 128);
            aggregate = CanonicalText.Encode(context.Identity.AggregateId, 1, 256);
            bool snapshot = context.PayloadKind == PayloadProtectionPayloadKind.Snapshot;
            payloadType = CanonicalText.Encode(context.PayloadTypeId, snapshot ? 16 : 1, snapshot ? 128 : 1024);
            path = CanonicalText.Encode(propertyPath, snapshot ? 0 : 1, PayloadProtectionLimits.PathBytes);
            key = CanonicalText.Encode(keyReference, 26, 26);
            result = new byte[totalLength];
            "HXAD"u8.CopyTo(result);
            result[4] = 1;
            result[5] = (byte)context.PayloadKind;
            result[6] = 11;
            int offset = 8;
            offset = WriteField(result, offset, 1, 1, tenant);
            offset = WriteField(result, offset, 2, 1, domain);
            offset = WriteField(result, offset, 3, 1, aggregate);
            offset = WriteField(result, offset, 4, 1, payloadType);
            offset = WriteField(result, offset, 5, 1, path);
            offset = WriteField(result, offset, 6, 1, key);
            Span<byte> version = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(version, dekVersion);
            offset = WriteField(result, offset, 7, 2, version);
            offset = WriteField(result, offset, 8, 1, Format);
            Span<byte> ordinal = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(ordinal, fieldOrdinal);
            offset = WriteField(result, offset, 9, 2, ordinal);
            Span<byte> sequence = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(sequence, context.RecordSequence);
            offset = WriteField(result, offset, 10, 3, sequence);
            offset = WriteField(result, offset, 11, 4, manifestCommitment);
            if (offset != result.Length)
            {
                throw new PayloadProtectionFormatException();
            }

            byte[] transferred = result;
            result = null;
            return transferred;
        }
        finally
        {
            Clear(tenant);
            Clear(domain);
            Clear(aggregate);
            Clear(payloadType);
            Clear(path);
            Clear(key);
            Clear(result);
        }
    }

    private static int WriteField(
        Span<byte> destination,
        int offset,
        byte identifier,
        byte type,
        ReadOnlySpan<byte> value)
    {
        Span<byte> header = destination.Slice(offset, 6);
        header[0] = identifier;
        header[1] = type;
        BinaryPrimitives.WriteUInt32BigEndian(header[2..], checked((uint)value.Length));
        value.CopyTo(destination[(offset + 6)..]);
        return checked(offset + 6 + value.Length);
    }

    private static void ValidateSnapshotTypeId(string? value)
    {
        const string prefix = "hx-snapshot-v1:";
        int length = CanonicalText.GetByteCount(value, 16, 128);
        if (value is null
            || length != value.Length
            || !value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length == prefix.Length)
        {
            throw new PayloadProtectionFormatException();
        }

        ReadOnlySpan<char> suffix = value.AsSpan(prefix.Length);
        if (suffix[0] == '-' || suffix[^1] == '-')
        {
            throw new PayloadProtectionFormatException();
        }

        for (int index = 0; index < suffix.Length; index++)
        {
            char character = suffix[index];
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            {
                throw new PayloadProtectionFormatException();
            }

            if (character == '-' && index > 0 && suffix[index - 1] == '-')
            {
                throw new PayloadProtectionFormatException();
            }
        }
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
