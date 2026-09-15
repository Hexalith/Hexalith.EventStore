using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the closed pdenc-v2 wire offsets, identifiers, and serialization-format labels from normative sections 6-8.
/// </summary>
internal static class PayloadProtectionWireFormat
{
    private static readonly byte[] _protectedSerializationFormatUtf8 = Encoding.UTF8.GetBytes(ProtectedSerializationFormat);

    /// <summary>Gets the unprotected JSON serialization format.</summary>
    internal const string UnprotectedSerializationFormat = "json";

    /// <summary>Gets the pdenc-v2 protected JSON serialization format.</summary>
    internal const string ProtectedSerializationFormat = "json+pdenc-v2";

    /// <summary>Gets the envelope-version field offset.</summary>
    internal const int EnvelopeVersionOffset = 4;

    /// <summary>Gets the AEAD-algorithm field offset.</summary>
    internal const int AlgorithmIdentifierOffset = 5;

    /// <summary>Gets the nonce-construction field offset.</summary>
    internal const int NonceConstructionIdentifierOffset = 6;

    /// <summary>Gets the key-reference-kind field offset.</summary>
    internal const int KeyReferenceKindOffset = 7;

    /// <summary>Gets the header-length field offset.</summary>
    internal const int HeaderLengthOffset = 8;

    /// <summary>Gets the key-reference-length field offset.</summary>
    internal const int KeyReferenceLengthOffset = 10;

    /// <summary>Gets the DEK-version field offset.</summary>
    internal const int DekVersionOffset = 12;

    /// <summary>Gets the field-ordinal field offset.</summary>
    internal const int FieldOrdinalOffset = 16;

    /// <summary>Gets the nonce-length field offset.</summary>
    internal const int NonceLengthOffset = 20;

    /// <summary>Gets the authentication-tag-length field offset.</summary>
    internal const int TagLengthOffset = 21;

    /// <summary>Gets the reserved-flags field offset.</summary>
    internal const int FlagsOffset = 22;

    /// <summary>Gets the ciphertext-length field offset.</summary>
    internal const int CiphertextLengthOffset = 24;

    /// <summary>Gets the variable key-reference field offset.</summary>
    internal const int KeyReferenceOffset = 28;

    /// <summary>Gets the variable nonce field offset.</summary>
    internal const int NonceOffset = 54;

    /// <summary>Gets the variable ciphertext field offset.</summary>
    internal const int CiphertextOffset = 66;

    /// <summary>Gets the complete fixed envelope overhead around ciphertext.</summary>
    internal const int EnvelopeFixedOverheadBytes = 82;

    /// <summary>Gets the minimum complete envelope length for one ciphertext byte.</summary>
    internal const int MinimumEnvelopeBytes = EnvelopeFixedOverheadBytes + 1;

    /// <summary>Gets the required envelope version.</summary>
    internal const byte EnvelopeVersion = 2;

    /// <summary>Gets the required AES-256-GCM algorithm identifier.</summary>
    internal const byte AlgorithmIdentifier = 1;

    /// <summary>Gets the required ordinal-derived nonce-construction identifier.</summary>
    internal const byte NonceConstructionIdentifier = 1;

    /// <summary>Gets the required canonical-ULID key-reference-kind identifier.</summary>
    internal const byte KeyReferenceKind = 1;

    /// <summary>Gets the exact protected serialization format as UTF-8 bytes.</summary>
    internal static ReadOnlySpan<byte> ProtectedSerializationFormatUtf8 => _protectedSerializationFormatUtf8;

    /// <summary>Gets the exact HXP2 envelope magic as ASCII bytes.</summary>
    internal static ReadOnlySpan<byte> EnvelopeMagic => "HXP2"u8;
}
