namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiOptions : IEquatable<RestApiOptions>
{
    public RestApiOptions(bool found, string routePrefix, string tag, string tenantSource)
    {
        Found = found;
        RoutePrefix = routePrefix;
        Tag = tag;
        TenantSource = tenantSource;
    }

    public bool Found { get; }

    public string RoutePrefix { get; }

    public string Tag { get; }

    public string TenantSource { get; }

    public bool Equals(RestApiOptions other)
        => Found == other.Found
            && string.Equals(RoutePrefix, other.RoutePrefix, StringComparison.Ordinal)
            && string.Equals(Tag, other.Tag, StringComparison.Ordinal)
            && string.Equals(TenantSource, other.TenantSource, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiOptions other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Found.GetHashCode();
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(RoutePrefix);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Tag);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TenantSource);
            return hash;
        }
    }
}
