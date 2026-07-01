namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiMessageDescriptor : IEquatable<RestApiMessageDescriptor>
{
    public RestApiMessageDescriptor(
        string typeName,
        bool isCommand,
        bool isQuery,
        RestApiRouteDescriptor? route)
    {
        TypeName = typeName;
        IsCommand = isCommand;
        IsQuery = isQuery;
        Route = route;
    }

    public string TypeName { get; }

    public bool IsCommand { get; }

    public bool IsQuery { get; }

    public RestApiRouteDescriptor? Route { get; }

    public bool Equals(RestApiMessageDescriptor other)
        => string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)
            && IsCommand == other.IsCommand
            && IsQuery == other.IsQuery
            && Nullable.Equals(Route, other.Route);

    public override bool Equals(object? obj) => obj is RestApiMessageDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(TypeName);
            hash = (hash * 397) ^ IsCommand.GetHashCode();
            hash = (hash * 397) ^ IsQuery.GetHashCode();
            hash = (hash * 397) ^ Route.GetHashCode();
            return hash;
        }
    }
}
