namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiRouteDescriptor : IEquatable<RestApiRouteDescriptor>
{
    public RestApiRouteDescriptor(string verb, string template)
    {
        Verb = verb;
        Template = template;
    }

    public string Verb { get; }

    public string Template { get; }

    public bool Equals(RestApiRouteDescriptor other)
        => string.Equals(Verb, other.Verb, StringComparison.Ordinal)
            && string.Equals(Template, other.Template, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiRouteDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Verb) * 397)
                ^ StringComparer.Ordinal.GetHashCode(Template);
        }
    }
}
