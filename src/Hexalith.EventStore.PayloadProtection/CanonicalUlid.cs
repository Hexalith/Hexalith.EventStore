// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8 and 14.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Validates the closed canonical Crockford-base32 ULID spelling used by pdenc-v2.
/// </summary>
internal static class CanonicalUlid
{
    /// <summary>
    /// Determines whether a value is the canonical 26-character uppercase representation of 128 ULID bits.
    /// </summary>
    internal static bool IsValid(string? value)
    {
        if (value is null || value.Length != PayloadProtectionWireFormat.KeyReferenceCharacters || value[0] > '7')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!PayloadProtectionWireFormat.CrockfordBase32Alphabet.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
