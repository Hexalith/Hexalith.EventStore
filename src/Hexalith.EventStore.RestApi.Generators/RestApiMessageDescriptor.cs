using System.Collections.Immutable;

namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiMessageDescriptor : IEquatable<RestApiMessageDescriptor>
{
    public RestApiMessageDescriptor(
        string typeName,
        string fullyQualifiedTypeName,
        string namespaceName,
        string simpleTypeName,
        bool isCommand,
        bool isQuery,
        RestApiRouteDescriptor? route,
        ImmutableArray<RestApiBindablePropertyDescriptor> properties,
        string? unsupportedReason = null)
    {
        TypeName = typeName;
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        NamespaceName = namespaceName;
        SimpleTypeName = simpleTypeName;
        IsCommand = isCommand;
        IsQuery = isQuery;
        Route = route;
        Properties = properties.IsDefault ? ImmutableArray<RestApiBindablePropertyDescriptor>.Empty : properties;
        UnsupportedReason = unsupportedReason;
    }

    public string TypeName { get; }

    public string FullyQualifiedTypeName { get; }

    public string NamespaceName { get; }

    public string SimpleTypeName { get; }

    public bool IsCommand { get; }

    public bool IsQuery { get; }

    public RestApiRouteDescriptor? Route { get; }

    public ImmutableArray<RestApiBindablePropertyDescriptor> Properties { get; }

    public string? UnsupportedReason { get; }

    public bool Equals(RestApiMessageDescriptor other)
        => string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
            && string.Equals(FullyQualifiedTypeName, other.FullyQualifiedTypeName, StringComparison.Ordinal)
            && string.Equals(NamespaceName, other.NamespaceName, StringComparison.Ordinal)
            && string.Equals(SimpleTypeName, other.SimpleTypeName, StringComparison.Ordinal)
            && IsCommand == other.IsCommand
            && IsQuery == other.IsQuery
            && Nullable.Equals(Route, other.Route)
            && Properties.SequenceEqual(other.Properties)
            && string.Equals(UnsupportedReason, other.UnsupportedReason, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiMessageDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(TypeName);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(FullyQualifiedTypeName);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(NamespaceName);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SimpleTypeName);
            hash = (hash * 397) ^ IsCommand.GetHashCode();
            hash = (hash * 397) ^ IsQuery.GetHashCode();
            hash = (hash * 397) ^ Route.GetHashCode();
            hash = (hash * 397) ^ (UnsupportedReason is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(UnsupportedReason));
            foreach (RestApiBindablePropertyDescriptor property in Properties)
            {
                hash = (hash * 397) ^ property.GetHashCode();
            }

            return hash;
        }
    }
}
