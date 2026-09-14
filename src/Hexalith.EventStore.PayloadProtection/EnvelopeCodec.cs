using System.Buffers.Binary;
using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Reads and writes the closed pdenc-v2 binary grammar from normative section 6.2.
/// </summary>
internal static class EnvelopeCodec {
    private static ReadOnlySpan<byte> Magic => "HXP2"u8;

    /// <summary>
    /// Serializes a validated envelope.
    /// </summary>
    internal static byte[] Write(PayloadProtectionEnvelope envelope) {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateFields(envelope);

        int length = checked(82 + envelope.Ciphertext.Length);
        byte[] result = new byte[length];
        try
        {
            Magic.CopyTo(result);
            result[4] = 2;
            result[5] = 1;
            result[6] = 1;
            result[7] = 1;
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8), PayloadProtectionLimits.HeaderBytes);
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10), PayloadProtectionLimits.KeyReferenceBytes);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(12), envelope.DekVersion);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(16), envelope.FieldOrdinal);
            result[20] = PayloadProtectionLimits.NonceBytes;
            result[21] = PayloadProtectionLimits.TagBytes;
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(24), checked((uint)envelope.Ciphertext.Length));
            Encoding.ASCII.GetBytes(envelope.KeyReference, result.AsSpan(28, PayloadProtectionLimits.KeyReferenceBytes));
            envelope.Nonce.CopyTo(result, 54);
            envelope.Ciphertext.CopyTo(result, 66);
            envelope.Tag.CopyTo(result, 66 + envelope.Ciphertext.Length);
            return result;
        }
        catch
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(result);
            throw;
        }
    }

    /// <summary>
    /// Parses an envelope after validating its fixed header and checked total length.
    /// </summary>
    internal static PayloadProtectionEnvelope Read(ReadOnlySpan<byte> value) {
        if (value.Length < 83 || value.Length > PayloadProtectionLimits.EnvelopeBytes) {
            throw new PayloadProtectionFormatException();
        }

        if (!value[..4].SequenceEqual(Magic)
            || value[4] != 2
            || value[5] != 1
            || value[6] != 1
            || value[7] != 1
            || BinaryPrimitives.ReadUInt16BigEndian(value[8..]) != PayloadProtectionLimits.HeaderBytes
            || BinaryPrimitives.ReadUInt16BigEndian(value[10..]) != PayloadProtectionLimits.KeyReferenceBytes
            || value[20] != PayloadProtectionLimits.NonceBytes
            || value[21] != PayloadProtectionLimits.TagBytes
            || value[22] != 0
            || value[23] != 0) {
            throw new PayloadProtectionFormatException();
        }

        uint dekVersion = BinaryPrimitives.ReadUInt32BigEndian(value[12..]);
        uint ordinal = BinaryPrimitives.ReadUInt32BigEndian(value[16..]);
        uint ciphertextLength = BinaryPrimitives.ReadUInt32BigEndian(value[24..]);
        if (dekVersion == 0
            || ordinal >= PayloadProtectionLimits.ProtectedPaths
            || ciphertextLength is 0 or > PayloadProtectionLimits.CiphertextBytes) {
            throw new PayloadProtectionFormatException();
        }

        long expectedLength = 82L + ciphertextLength;
        if (expectedLength != value.Length) {
            throw new PayloadProtectionFormatException();
        }

        string keyReference = CanonicalText.Decode(value.Slice(28, PayloadProtectionLimits.KeyReferenceBytes), 26, 26);
        if (!CanonicalUlid.IsValid(keyReference)) {
            throw new PayloadProtectionFormatException();
        }

        int ciphertextBytes = checked((int)ciphertextLength);
        byte[]? nonce = null;
        byte[]? ciphertext = null;
        byte[]? tag = null;
        try
        {
            nonce = value.Slice(54, PayloadProtectionLimits.NonceBytes).ToArray();
            ciphertext = value.Slice(66, ciphertextBytes).ToArray();
            tag = value.Slice(66 + ciphertextBytes, PayloadProtectionLimits.TagBytes).ToArray();
            var envelope = new PayloadProtectionEnvelope(keyReference, dekVersion, ordinal, nonce, ciphertext, tag);
            nonce = null;
            ciphertext = null;
            tag = null;
            return envelope;
        }
        finally
        {
            if (nonce is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(nonce);
            }

            if (ciphertext is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(ciphertext);
            }

            if (tag is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(tag);
            }
        }
    }

    private static void ValidateFields(PayloadProtectionEnvelope envelope) {
        if (!CanonicalUlid.IsValid(envelope.KeyReference)
            || envelope.DekVersion == 0
            || envelope.FieldOrdinal >= PayloadProtectionLimits.ProtectedPaths
            || envelope.Nonce is null
            || envelope.Ciphertext is null
            || envelope.Tag is null
            || envelope.Nonce.Length != PayloadProtectionLimits.NonceBytes
            || envelope.Tag.Length != PayloadProtectionLimits.TagBytes
            || envelope.Ciphertext.Length is 0 or > PayloadProtectionLimits.CiphertextBytes) {
            throw new PayloadProtectionFormatException();
        }

        Span<byte> expectedNonce = stackalloc byte[PayloadProtectionLimits.NonceBytes];
        BinaryPrimitives.WriteUInt64BigEndian(expectedNonce[4..], envelope.FieldOrdinal);
        if (!envelope.Nonce.AsSpan().SequenceEqual(expectedNonce)) {
            throw new PayloadProtectionFormatException();
        }
    }
}
