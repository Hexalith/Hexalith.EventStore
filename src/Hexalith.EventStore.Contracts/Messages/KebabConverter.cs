using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Messages;

/// <summary>
/// Converts PascalCase strings to kebab-case. Internal helper for MessageType assembly.
/// Unlike NamingConventionEngine, this does NOT strip suffixes — raw type name conversion only.
/// </summary>
internal static partial class KebabConverter {
    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])|(?<=[a-zA-Z])([0-9])", RegexOptions.Compiled)]
    private static partial Regex WordBoundaryRegex();

    /// <summary>
    /// Converts a PascalCase string to kebab-case.
    /// </summary>
    /// <param name="pascalCase">The PascalCase input string.</param>
    /// <returns>The kebab-case equivalent.</returns>
    internal static string ConvertToKebab(string pascalCase)
        => WordBoundaryRegex().Replace(pascalCase, "-$1$2$3").ToLowerInvariant();
}
