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

                int close = template.IndexOf('}', index + 1);
                if (close < 0)
                {
                    return "Route template has an unclosed route parameter.";
                }

                string name = CleanParameterName(template.Substring(index + 1, close - index - 1));
                if (name.Length == 0)
                {
                    return "Route template has an empty route parameter.";
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

            int close = template.IndexOf('}', open + 1);
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

    private static string CleanParameterName(string value)
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

        return name.Trim();
    }
}
