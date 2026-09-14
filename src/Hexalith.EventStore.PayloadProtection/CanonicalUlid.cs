namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Validates the closed canonical Crockford-base32 ULID spelling used by pdenc-v2.
/// </summary>
internal static class CanonicalUlid {
    private const string _alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Determines whether a value is the canonical 26-character uppercase representation of 128 ULID bits.
    /// </summary>
    internal static bool IsValid(string? value) {
        if (value is null || value.Length != PayloadProtectionLimits.KeyReferenceBytes || value[0] > '7') {
            return false;
        }

        foreach (char character in value) {
            if (!_alphabet.Contains(character, StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }
}
