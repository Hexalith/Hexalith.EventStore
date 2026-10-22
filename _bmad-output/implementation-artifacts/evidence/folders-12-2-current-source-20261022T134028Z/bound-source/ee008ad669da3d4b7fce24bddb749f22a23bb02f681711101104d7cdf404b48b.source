using System.Text;

namespace Hexalith.McpCli.Abstractions;

/// <summary>
/// Converts CLR-style names to deterministic ASCII lowercase kebab-case operation parts.
/// </summary>
public static class KebabCase
{
    /// <summary>
    /// Converts a name to kebab-case without removing any suffix.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The kebab-case name.</returns>
    public static string Convert(string name)
    {
#if NETSTANDARD2_0
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", nameof(name));
        }
#else
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
#endif

        var result = new StringBuilder(name.Length + 4);
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (current is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'))
            {
                throw new ArgumentException("Only ASCII letters and digits are supported in operation names.", nameof(name));
            }

            bool upper = current is >= 'A' and <= 'Z';
            if (upper && index > 0)
            {
                char previous = name[index - 1];
                bool precedingLowerOrDigit = previous is >= 'a' and <= 'z' or >= '0' and <= '9';
                bool acronymBoundary = previous is >= 'A' and <= 'Z'
                    && index + 1 < name.Length && name[index + 1] is >= 'a' and <= 'z';
                if (precedingLowerOrDigit || acronymBoundary)
                {
                    result.Append('-');
                }
            }

            result.Append(upper ? (char)(current + ('a' - 'A')) : current);
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a CLR operation type name after removing only a trailing Command or Query suffix.
    /// </summary>
    /// <param name="typeName">The CLR type name.</param>
    /// <returns>The canonical operation name part.</returns>
    public static string FromTypeName(string typeName)
    {
#if NETSTANDARD2_0
        if (typeName is null)
        {
            throw new ArgumentNullException(nameof(typeName));
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", nameof(typeName));
        }
#else
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
#endif
        string name = typeName.EndsWith("Command", StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - "Command".Length)
            : typeName.EndsWith("Query", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - "Query".Length)
                : typeName;
        if (name.Length == 0)
        {
            throw new ArgumentException("An operation type name must have a name before its suffix.", nameof(typeName));
        }

        return Convert(name);
    }
}
