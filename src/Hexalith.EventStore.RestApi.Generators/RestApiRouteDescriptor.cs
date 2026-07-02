using System.Collections.Immutable;

namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiRouteDescriptor : IEquatable<RestApiRouteDescriptor>
{
    public RestApiRouteDescriptor(string verb, string template)
    {
        Verb = verb;
        Template = template;
        IsAbsolute = template.StartsWith("~/", StringComparison.Ordinal);
        Parameters = RestApiRouteTemplateParser.ParseParameters(template);
        TemplateError = RestApiRouteTemplateParser.GetTemplateError(template);
    }

    public string Verb { get; }

    public string Template { get; }

    public bool IsAbsolute { get; }

    public ImmutableArray<RestApiRouteParameterDescriptor> Parameters { get; }

    public string? TemplateError { get; }

    public bool Equals(RestApiRouteDescriptor other)
        => string.Equals(Verb, other.Verb, StringComparison.Ordinal)
            && string.Equals(Template, other.Template, StringComparison.Ordinal)
            && IsAbsolute == other.IsAbsolute
            && Parameters.SequenceEqual(other.Parameters)
            && string.Equals(TemplateError, other.TemplateError, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiRouteDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (StringComparer.Ordinal.GetHashCode(Verb) * 397)
                ^ StringComparer.Ordinal.GetHashCode(Template);
            hash = (hash * 397) ^ IsAbsolute.GetHashCode();
            hash = (hash * 397) ^ (TemplateError is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(TemplateError));
            foreach (RestApiRouteParameterDescriptor parameter in Parameters)
            {
                hash = (hash * 397) ^ parameter.GetHashCode();
            }

            return hash;
        }
    }
}
