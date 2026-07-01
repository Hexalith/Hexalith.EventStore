namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiBindablePropertyDescriptor : IEquatable<RestApiBindablePropertyDescriptor>
{
    public RestApiBindablePropertyDescriptor(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public string Name { get; }

    public string TypeName { get; }

    public bool Equals(RestApiBindablePropertyDescriptor other)
        => string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiBindablePropertyDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Name) * 397)
                ^ StringComparer.Ordinal.GetHashCode(TypeName);
        }
    }
}
