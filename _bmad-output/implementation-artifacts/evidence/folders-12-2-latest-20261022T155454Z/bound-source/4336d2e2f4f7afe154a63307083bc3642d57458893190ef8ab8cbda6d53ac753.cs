namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Opts a domain assembly into REST controller generation and sets shared options for the
/// generated controllers.
/// </summary>
/// <param name="routePrefix">The shared route prefix for the domain's generated controllers (convention: "api/{domain}").</param>
/// <param name="tag">The optional OpenAPI tag for the generated controllers; defaults to null.</param>
/// <param name="tenantSource">The source used to resolve the tenant; defaults to <see cref="RestTenantSource.Claims"/>.</param>
/// <remarks>
/// The default route-prefix convention is <c>api/{domain}</c>; pass that value (or a custom prefix)
/// as the route prefix. Messages without a <see cref="RestRouteAttribute"/> fall back to convention
/// routing relative to <see cref="RoutePrefix"/> (see <see cref="RestRouteAttribute"/>).
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class RestApiAttribute(
    string routePrefix,
    string? tag = null,
    RestTenantSource tenantSource = RestTenantSource.Claims) : Attribute
{
    /// <summary>Gets the shared route prefix for the domain's generated controllers.</summary>
    public string RoutePrefix { get; } = ValidateRoutePrefix(routePrefix);

    /// <summary>Gets the optional OpenAPI tag for the generated controllers.</summary>
    public string? Tag { get; } = NormalizeTag(tag);

    /// <summary>Gets the source used to resolve the tenant for the generated endpoints.</summary>
    public RestTenantSource TenantSource { get; } = tenantSource;

    private static string? NormalizeTag(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
    }

    private static string ValidateRoutePrefix(string routePrefix)
    {
        ArgumentNullException.ThrowIfNull(routePrefix);
        return routePrefix;
    }
}
