using System.Text;

using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiNameSanitizer
{
    public static string ToIdentifier(string value, string fallback, bool camelCase)
    {
        string identifier = ToPascalIdentifier(value, fallback);
        if (!camelCase || identifier.Length == 0)
        {
            return identifier;
        }

        return EscapeKeyword(char.ToLowerInvariant(identifier[0]) + identifier.Substring(1));
    }

    public static string ToTypeName(string value, string fallback)
    {
        string identifier = ToPascalIdentifier(value, fallback);
        return identifier.Length == 0 ? fallback : identifier;
    }

    public static string ToHintPart(string value, string fallback)
    {
        var builder = new StringBuilder();
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length == 0 || builder[builder.Length - 1] != '.')
            {
                builder.Append('.');
            }
        }

        string hint = builder.ToString().Trim('.');
        return hint.Length == 0 ? fallback : hint;
    }

    public static string ToNamespace(string value, string fallback)
    {
        string source = string.IsNullOrWhiteSpace(value) ? fallback : value;
        string[] parts = source.Split('.');
        var sanitized = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            sanitized.Add(ToPascalIdentifier(part, "Generated"));
        }

        return string.Join(".", sanitized);
    }

    private static string ToPascalIdentifier(string value, string fallback)
    {
        var builder = new StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                if (builder.Length == 0 && char.IsDigit(c))
                {
                    builder.Append('_');
                }

                builder.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        string identifier = builder.ToString();
        return identifier.Length == 0 ? fallback : identifier;
    }

    private static string EscapeKeyword(string identifier)
        => SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
                ? "@" + identifier
                : identifier;
}
