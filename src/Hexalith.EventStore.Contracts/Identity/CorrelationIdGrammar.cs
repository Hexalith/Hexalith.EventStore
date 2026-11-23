using System.Diagnostics.CodeAnalysis;

namespace Hexalith.EventStore.Contracts.Identity;

/// <summary>Defines the diagnostic identifier accepted in X-Correlation-ID and event metadata.</summary>
public static class CorrelationIdGrammar
{
    /// <summary>Checks for 1–128 ASCII letters, digits or hyphens in the supplied identifier.</summary>
    /// <param name="value">The identifier as supplied by the caller.</param>
    /// <returns>Whether the entire value is a valid diagnostic identifier.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        if (value is null || value.Length is < 1 or > 128)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}
