using System.Buffers;
using System.Globalization;
using System.Text;

namespace Hexalith.Tenants.Contracts.Identity;

/// <summary>Defines the literal global-administrator user identifier boundary.</summary>
public static class GlobalAdministratorUserId
{
    /// <summary>Gets the maximum supported literal user identifier length in UTF-16 code units.</summary>
    public const int MaximumLength = 256;

    /// <summary>Returns whether a literal user identifier is safe to carry without normalization.</summary>
    /// <param name="userId">Literal user identifier.</param>
    /// <returns><see langword="true"/> when the identifier satisfies the shared boundary.</returns>
    public static bool IsSupported(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > MaximumLength)
        {
            return false;
        }

        ReadOnlySpan<char> remaining = userId.AsSpan();
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed);
            if (status is not OperationStatus.Done
                || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }
}
