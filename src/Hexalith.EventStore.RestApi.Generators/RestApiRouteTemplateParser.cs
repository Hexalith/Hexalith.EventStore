using System.Collections.Immutable;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiRouteTemplateParser
{
    public static string? GetTemplateError(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        if (template.StartsWith("/", StringComparison.Ordinal))
        {
            return "Route template must be relative or start with '~/' for an absolute route.";
        }

        if (template.StartsWith("~", StringComparison.Ordinal)
            && !template.StartsWith("~/", StringComparison.Ordinal))
        {
            return "Route template cannot start with '~' unless it starts with '~/'.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        while (index < template.Length)
        {
            char current = template[index];
            if (current == '{')
            {
                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    index += 2;
                    continue;
                }

                int close = FindParameterClose(template, index);
                if (close < 0)
                {
                    return "Route template has an unclosed route parameter.";
                }

                string rawParameter = template.Substring(index + 1, close - index - 1);
                if (HasUnescapedOpenBrace(rawParameter))
                {
                    return "Route template has an unescaped brace inside a route parameter.";
                }

                if (HasBraceInParameterName(rawParameter))
                {
                    return "Route template has a brace inside a route parameter name.";
                }

                string name = CleanParameterName(rawParameter);
                if (name.Length == 0)
                {
                    return "Route template has an empty route parameter.";
                }

                if (IsCatchAllParameter(rawParameter) && close != template.Length - 1)
                {
                    return "Route template catch-all parameter must be the final route segment.";
                }

                if (!seen.Add(name))
                {
                    return "Route template contains duplicate route parameter '" + name + "'.";
                }

                index = close + 1;
                continue;
            }

            if (current == '}')
            {
                if (index + 1 < template.Length && template[index + 1] == '}')
                {
                    index += 2;
                    continue;
                }

                return "Route template has an unopened route parameter.";
            }

            index++;
        }

        return null;
    }

    public static ImmutableArray<RestApiRouteParameterDescriptor> ParseParameters(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return ImmutableArray<RestApiRouteParameterDescriptor>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<RestApiRouteParameterDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        while (index < template.Length)
        {
            int open = template.IndexOf('{', index);
            if (open < 0 || open == template.Length - 1)
            {
                break;
            }

            if (template[open + 1] == '{')
            {
                index = open + 2;
                continue;
            }

            int close = FindParameterClose(template, open);
            if (close < 0)
            {
                break;
            }

            string name = CleanParameterName(template.Substring(open + 1, close - open - 1));
            if (name.Length > 0 && seen.Add(name))
            {
                parameters.Add(new RestApiRouteParameterDescriptor(
                    name,
                    RestApiNameSanitizer.ToIdentifier(name, "routeValue", camelCase: true)));
            }

            index = close + 1;
        }

        return parameters.ToImmutable();
    }

    internal static int FindParameterClose(string template, int open)
    {
        int index = open + 1;
        while (index < template.Length)
        {
            char current = template[index];
            if (current == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                index += 2;
                continue;
            }

            if (current == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                index += 2;
                continue;
            }

            if (current == '}')
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static bool HasUnescapedOpenBrace(string value)
    {
        int index = 0;
        while (index < value.Length)
        {
            if (value[index] == '{')
            {
                if (index + 1 < value.Length && value[index + 1] == '{')
                {
                    index += 2;
                    continue;
                }

                return true;
            }

            index++;
        }

        return false;
    }

    private static bool HasBraceInParameterName(string value)
    {
        string name = GetParameterNamePart(value);
        return name.IndexOf('{') >= 0 || name.IndexOf('}') >= 0;
    }

    private static string CleanParameterName(string value)
        => GetParameterNamePart(value).Trim();

    private static string GetParameterNamePart(string value)
    {
        string name = value.Trim();
        while (name.StartsWith("*", StringComparison.Ordinal))
        {
            name = name.Substring(1);
        }

        int terminator = name.IndexOfAny(new[] { ':', '=', '?' });
        if (terminator >= 0)
        {
            name = name.Substring(0, terminator);
        }

        return name;
    }

    private static bool IsCatchAllParameter(string value)
        => value.TrimStart().StartsWith("*", StringComparison.Ordinal);
}
