using System.Diagnostics.CodeAnalysis;

using ByteAether.Ulid;

namespace Hexalith.EventStore.Contracts.Identity;

/// <summary>Validates a command <c>MessageId</c> under the grammar version its contract declares.</summary>
/// <remarks>
/// Version 1 is the legacy grammar: 1 to 128 ASCII letters, digits or hyphens, with no leading or trailing
/// hyphen. Version 2 is the canonical ULID grammar: exactly 26 uppercase Crockford Base32 characters that
/// parse as a ULID whose text form is byte-identical to the input. Validation never normalizes the value.
/// </remarks>
public static class MessageIdGrammar
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int MaxLegacyLength = 128;
    private const int UlidLength = 26;

    /// <summary>Checks one <c>MessageId</c> against an explicit grammar version.</summary>
    /// <param name="messageId">The identifier to check, unchanged.</param>
    /// <param name="version">The declared grammar version: 1 (legacy) or 2 (canonical ULID).</param>
    /// <returns>Whether the version is 1 or 2 and the identifier is valid under it.</returns>
    public static bool IsValid([NotNullWhen(true)] string? messageId, int version)
        => messageId is not null && version switch
        {
            1 => IsLegacy(messageId),
            2 => IsCanonicalUlid(messageId),
            _ => false,
        };

    private static bool IsLegacy(string messageId)
    {
        if (messageId.Length is < 1 or > MaxLegacyLength || messageId[0] == '-' || messageId[^1] == '-')
        {
            return false;
        }

        foreach (char character in messageId)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCanonicalUlid(string messageId)
    {
        if (messageId.Length != UlidLength)
        {
            return false;
        }

        foreach (char character in messageId)
        {
            if (!CrockfordAlphabet.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return Ulid.TryParse(messageId, null, out Ulid parsed)
            && string.Equals(parsed.ToString(), messageId, StringComparison.Ordinal);
    }
}
