// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6, 8, 14, and 15.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Reads and writes the closed pdenc-v2 binary grammar from normative section 6.2.
/// </summary>
internal static class EnvelopeCodec
{
    /// <summary>
    /// Serializes a validated envelope.
    /// </summary>
    internal static byte[] Write(PayloadProtectionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateFields(envelope);

        int length = checked(PayloadProtectionWireFormat.EnvelopeFixedOverheadBytes + envelope.Ciphertext.Length);
        byte[] result = new byte[length];
        try
        {
            PayloadProtectionWireFormat.EnvelopeMagic.CopyTo(result);
            result[PayloadProtectionWireFormat.EnvelopeVersionOffset] = PayloadProtectionWireFormat.EnvelopeVersion;
            result[PayloadProtectionWireFormat.AlgorithmIdentifierOffset] = PayloadProtectionWireFormat.AlgorithmIdentifier;
            result[PayloadProtectionWireFormat.NonceConstructionIdentifierOffset] = PayloadProtectionWireFormat.NonceConstructionIdentifier;
            result[PayloadProtectionWireFormat.KeyReferenceKindOffset] = PayloadProtectionWireFormat.KeyReferenceKind;
            BinaryPrimitives.WriteUInt16BigEndian(
                result.AsSpan(PayloadProtectionWireFormat.HeaderLengthOffset),
                PayloadProtectionLimits.HeaderBytes);
            BinaryPrimitives.WriteUInt16BigEndian(
                result.AsSpan(PayloadProtectionWireFormat.KeyReferenceLengthOffset),
                PayloadProtectionLimits.KeyReferenceBytes);
            BinaryPrimitives.WriteUInt32BigEndian(
                result.AsSpan(PayloadProtectionWireFormat.DekVersionOffset),
                envelope.DekVersion);
            BinaryPrimitives.WriteUInt32BigEndian(
                result.AsSpan(PayloadProtectionWireFormat.FieldOrdinalOffset),
                envelope.FieldOrdinal);
            result[PayloadProtectionWireFormat.NonceLengthOffset] = PayloadProtectionLimits.NonceBytes;
            result[PayloadProtectionWireFormat.TagLengthOffset] = PayloadProtectionLimits.TagBytes;
            BinaryPrimitives.WriteUInt32BigEndian(
                result.AsSpan(PayloadProtectionWireFormat.CiphertextLengthOffset),
                checked((uint)envelope.Ciphertext.Length));
            Encoding.ASCII.GetBytes(
                envelope.KeyReference,
                result.AsSpan(
                    PayloadProtectionWireFormat.KeyReferenceOffset,
                    PayloadProtectionLimits.KeyReferenceBytes));
            envelope.Nonce.CopyTo(result, PayloadProtectionWireFormat.NonceOffset);
            envelope.Ciphertext.CopyTo(result, PayloadProtectionWireFormat.CiphertextOffset);
            envelope.Tag.CopyTo(
                result,
                PayloadProtectionWireFormat.CiphertextOffset + envelope.Ciphertext.Length);
            return result;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(result);
            throw;
        }
    }

    /// <summary>
    /// Parses an envelope after validating its fixed header and checked total length.
    /// </summary>
    /// <remarks>
    /// Ownership of the returned nonce, ciphertext, and tag transfers to the caller, which must zero
    /// them on every exit. Each is zeroed here before an unsuccessful return.
    /// </remarks>
    internal static PayloadProtectionEnvelope Read(ReadOnlySpan<byte> value)
    {
        if (value.Length < PayloadProtectionWireFormat.MinimumEnvelopeBytes
            || value.Length > PayloadProtectionLimits.EnvelopeBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        if (!value[..PayloadProtectionWireFormat.EnvelopeVersionOffset]
                .SequenceEqual(PayloadProtectionWireFormat.EnvelopeMagic)
            || value[PayloadProtectionWireFormat.EnvelopeVersionOffset] != PayloadProtectionWireFormat.EnvelopeVersion
            || value[PayloadProtectionWireFormat.AlgorithmIdentifierOffset] != PayloadProtectionWireFormat.AlgorithmIdentifier
            || value[PayloadProtectionWireFormat.NonceConstructionIdentifierOffset] != PayloadProtectionWireFormat.NonceConstructionIdentifier
            || value[PayloadProtectionWireFormat.KeyReferenceKindOffset] != PayloadProtectionWireFormat.KeyReferenceKind
            || BinaryPrimitives.ReadUInt16BigEndian(value[PayloadProtectionWireFormat.HeaderLengthOffset..]) != PayloadProtectionLimits.HeaderBytes
            || BinaryPrimitives.ReadUInt16BigEndian(value[PayloadProtectionWireFormat.KeyReferenceLengthOffset..]) != PayloadProtectionLimits.KeyReferenceBytes
            || value[PayloadProtectionWireFormat.NonceLengthOffset] != PayloadProtectionLimits.NonceBytes
            || value[PayloadProtectionWireFormat.TagLengthOffset] != PayloadProtectionLimits.TagBytes
            || value[PayloadProtectionWireFormat.FlagsOffset] != 0
            || value[PayloadProtectionWireFormat.FlagsOffset + 1] != 0)
        {
            throw new PayloadProtectionFormatException();
        }

        uint dekVersion = BinaryPrimitives.ReadUInt32BigEndian(value[PayloadProtectionWireFormat.DekVersionOffset..]);
        uint ordinal = BinaryPrimitives.ReadUInt32BigEndian(value[PayloadProtectionWireFormat.FieldOrdinalOffset..]);
        uint ciphertextLength = BinaryPrimitives.ReadUInt32BigEndian(value[PayloadProtectionWireFormat.CiphertextLengthOffset..]);
        // The complete-envelope bounds above subsume the ciphertext endpoints for the current
        // fixed overhead. Retain the field checks as defense in depth and for writer symmetry.
        if (dekVersion == 0
            || ordinal >= PayloadProtectionLimits.ProtectedPaths
            || ciphertextLength is 0 or > PayloadProtectionLimits.CiphertextBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        long expectedLength = PayloadProtectionWireFormat.EnvelopeFixedOverheadBytes + (long)ciphertextLength;
        if (expectedLength != value.Length)
        {
            throw new PayloadProtectionFormatException();
        }

        string keyReference = CanonicalText.Decode(
            value.Slice(
                PayloadProtectionWireFormat.KeyReferenceOffset,
                PayloadProtectionLimits.KeyReferenceBytes),
            PayloadProtectionLimits.KeyReferenceBytes,
            PayloadProtectionLimits.KeyReferenceBytes);
        if (!CanonicalUlid.IsValid(keyReference))
        {
            throw new PayloadProtectionFormatException();
        }

        int ciphertextBytes = checked((int)ciphertextLength);
        byte[]? nonce = null;
        byte[]? ciphertext = null;
        byte[]? tag = null;
        try
        {
            nonce = value.Slice(
                PayloadProtectionWireFormat.NonceOffset,
                PayloadProtectionLimits.NonceBytes).ToArray();
            ciphertext = value.Slice(
                PayloadProtectionWireFormat.CiphertextOffset,
                ciphertextBytes).ToArray();
            tag = value.Slice(
                PayloadProtectionWireFormat.CiphertextOffset + ciphertextBytes,
                PayloadProtectionLimits.TagBytes).ToArray();
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
                CryptographicOperations.ZeroMemory(nonce);
            }

            if (ciphertext is not null)
            {
                CryptographicOperations.ZeroMemory(ciphertext);
            }

            if (tag is not null)
            {
                CryptographicOperations.ZeroMemory(tag);
            }
        }
    }

    private static void ValidateFields(PayloadProtectionEnvelope envelope)
    {
        if (!CanonicalUlid.IsValid(envelope.KeyReference)
            || envelope.DekVersion == 0
            || envelope.FieldOrdinal >= PayloadProtectionLimits.ProtectedPaths
            || envelope.Nonce is null
            || envelope.Ciphertext is null
            || envelope.Tag is null
            || envelope.Nonce.Length != PayloadProtectionLimits.NonceBytes
            || envelope.Tag.Length != PayloadProtectionLimits.TagBytes
            || envelope.Ciphertext.Length is 0 or > PayloadProtectionLimits.CiphertextBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        Span<byte> expectedNonce = stackalloc byte[PayloadProtectionLimits.NonceBytes];
        expectedNonce.Clear();
        BinaryPrimitives.WriteUInt64BigEndian(expectedNonce[4..], envelope.FieldOrdinal);
        if (!envelope.Nonce.AsSpan().SequenceEqual(expectedNonce))
        {
            throw new PayloadProtectionFormatException();
        }
    }
}
