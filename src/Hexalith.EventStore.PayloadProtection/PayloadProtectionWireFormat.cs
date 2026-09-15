// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the closed pdenc-v2 wire offsets, identifiers, and serialization-format labels from normative sections 6-8.
/// </summary>
internal static class PayloadProtectionWireFormat
{
    /// <summary>Gets the unprotected JSON serialization format.</summary>
    internal const string UnprotectedSerializationFormat = "json";

    /// <summary>Gets the pdenc-v2 protected JSON serialization format.</summary>
    internal const string ProtectedSerializationFormat = "json+pdenc-v2";

    /// <summary>Gets the canonical Crockford-base32 alphabet used by ULID key references.</summary>
    internal const string CrockfordBase32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Gets the exact number of ASCII characters in a canonical ULID key reference.</summary>
    internal const int KeyReferenceCharacters = PayloadProtectionLimits.KeyReferenceBytes;

    /// <summary>Gets the required HXAD schema version.</summary>
    internal const byte AadSchemaVersion = 1;

    /// <summary>Gets the required HXAD field count.</summary>
    internal const byte AadFieldCount = 11;

    /// <summary>Gets the fixed HXAD header byte count that precedes the first field.</summary>
    internal const int AadHeaderBytes = 8;

    /// <summary>Gets the fixed per-field HXAD header byte count (identifier, type, and u32 length).</summary>
    internal const int AadFieldHeaderBytes = 6;

    /// <summary>Gets the HXAD reserved-byte offset, which is always zero.</summary>
    internal const int AadReservedOffset = 7;

    /// <summary>Gets the durable HXAD payload-kind byte for an event.</summary>
    internal const byte AadPayloadKindEvent = 1;

    /// <summary>Gets the durable HXAD payload-kind byte for a snapshot.</summary>
    internal const byte AadPayloadKindSnapshot = 2;

    /// <summary>Gets the required HXPM schema version.</summary>
    internal const byte ManifestSchemaVersion = 1;

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
    internal const int KeyReferenceOffset = PayloadProtectionLimits.HeaderBytes;

    /// <summary>Gets the variable nonce field offset.</summary>
    internal const int NonceOffset = KeyReferenceOffset + PayloadProtectionLimits.KeyReferenceBytes;

    /// <summary>Gets the variable ciphertext field offset.</summary>
    internal const int CiphertextOffset = NonceOffset + PayloadProtectionLimits.NonceBytes;

    /// <summary>Gets the complete fixed envelope overhead around ciphertext.</summary>
    internal const int EnvelopeFixedOverheadBytes = CiphertextOffset + PayloadProtectionLimits.TagBytes;

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
    internal static ReadOnlySpan<byte> ProtectedSerializationFormatUtf8 => "json+pdenc-v2"u8;

    /// <summary>Gets the exact HXAD authenticated-data magic as ASCII bytes.</summary>
    internal static ReadOnlySpan<byte> AadMagic => "HXAD"u8;

    /// <summary>Gets the exact HXP2 envelope magic as ASCII bytes.</summary>
    internal static ReadOnlySpan<byte> EnvelopeMagic => "HXP2"u8;

    /// <summary>Gets the exact HXPM protected-path manifest magic as ASCII bytes.</summary>
    internal static ReadOnlySpan<byte> ManifestMagic => "HXPM"u8;
}
