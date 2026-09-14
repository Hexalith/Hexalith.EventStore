namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the immutable pdenc-v2 resource and wire-format limits from normative digest
/// de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 6-8 and 15.
/// </summary>
internal static class PayloadProtectionLimits {
    internal const int AadBytes = 4096;
    internal const int CiphertextBytes = 1_048_576;
    internal const int EnvelopeBytes = 1_048_658;
    internal const int EnvelopeTextCharacters = 1_398_211;
    internal const int HeaderBytes = 28;
    internal const int JsonDepth = 64;
    internal const int JsonNodes = 65_536;
    internal const int KeyReferenceBytes = 26;
    internal const int ManifestBytes = 8_405_001;
    internal const int NonceBytes = 12;
    internal const int PayloadBytes = 16_777_216;
    internal const int ProtectedPaths = 4096;
    internal const int SelectedPlaintextBytes = 8_388_608;
    internal const int TagBytes = 16;
}
