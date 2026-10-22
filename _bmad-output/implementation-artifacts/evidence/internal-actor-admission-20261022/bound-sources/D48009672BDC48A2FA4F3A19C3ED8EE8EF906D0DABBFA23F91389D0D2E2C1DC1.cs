// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the immutable pdenc-v2 resource and wire-format limits from normative digest
/// de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 6-8 and 15.
/// </summary>
internal static class PayloadProtectionLimits
{
    /// <summary>Gets the maximum AAD byte count.</summary>
    internal const int AadBytes = 4096;

    /// <summary>Gets the maximum protected-value ciphertext byte count.</summary>
    internal const int CiphertextBytes = 1_048_576;

    /// <summary>Gets the maximum binary envelope byte count.</summary>
    internal const int EnvelopeBytes = 1_048_658;

    /// <summary>Gets the maximum base64url carrier character count.</summary>
    internal const int EnvelopeTextCharacters = 1_398_211;

    /// <summary>Gets the fixed HXP2 header byte count.</summary>
    internal const int HeaderBytes = 28;

    /// <summary>Gets the maximum JSON depth.</summary>
    internal const int JsonDepth = 64;

    /// <summary>Gets the maximum JSON node count.</summary>
    internal const int JsonNodes = 65_536;

    /// <summary>Gets the canonical key-reference byte count.</summary>
    internal const int KeyReferenceBytes = 26;

    /// <summary>Gets the maximum encoded manifest byte count.</summary>
    internal const int ManifestBytes = 8_405_001;

    /// <summary>Gets the fixed AES-GCM nonce byte count.</summary>
    internal const int NonceBytes = 12;

    /// <summary>Gets the maximum serialized payload byte count.</summary>
    internal const int PayloadBytes = 16_777_216;

    /// <summary>Gets the maximum canonical path byte count.</summary>
    internal const int PathBytes = 2048;

    /// <summary>Gets the maximum protected path count.</summary>
    internal const int ProtectedPaths = 4096;

    /// <summary>Gets the maximum cumulative selected plaintext byte count.</summary>
    internal const int SelectedPlaintextBytes = 8_388_608;

    /// <summary>Gets the fixed AES-GCM authentication-tag byte count.</summary>
    internal const int TagBytes = 16;
}
