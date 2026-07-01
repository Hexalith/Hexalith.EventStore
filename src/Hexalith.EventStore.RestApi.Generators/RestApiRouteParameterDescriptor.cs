namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiRouteParameterDescriptor : IEquatable<RestApiRouteParameterDescriptor>
{
    public RestApiRouteParameterDescriptor(string name, string identifier)
    {
        Name = name;
        Identifier = identifier;
    }

    public string Name { get; }

    public string Identifier { get; }

    public bool Equals(RestApiRouteParameterDescriptor other)
        => string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiRouteParameterDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Name) * 397)
                ^ StringComparer.Ordinal.GetHashCode(Identifier);
        }
    }
}
