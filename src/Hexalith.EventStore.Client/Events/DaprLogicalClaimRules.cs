namespace Hexalith.EventStore.Client.Events;

/// <summary>Applies the approved bounded event/key identity grammar to the new logical schemas.</summary>
internal static class DaprLogicalClaimRules
{
    /// <summary>Requires the existing 1..64 lower-case alphanumeric/interior-hyphen grammar.</summary>
    internal static void RequireCanonicalIdentity(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is < 1 or > 64) { throw new ArgumentException("Logical identity length exceeds the approved grammar."); }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is not (>= 'a' and <= 'z' or >= '0' and <= '9')
                && (character != '-' || index == 0 || index == value.Length - 1)) { throw new ArgumentException("Logical identity must use canonical lower-case kebab-case."); }
        }
    }

    /// <summary>Requires the existing approved payload-version interval.</summary>
    internal static void RequirePayloadVersion(int value) { if (value is < 1 or > 1024) { throw new ArgumentOutOfRangeException(nameof(value)); } }
}
