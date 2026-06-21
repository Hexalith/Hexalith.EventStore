namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Identifies the source used to resolve the tenant for a generated REST endpoint.
/// </summary>
public enum RestTenantSource {
    /// <summary>Resolve the tenant from the authenticated caller's claims.</summary>
    Claims,

    /// <summary>Resolve the tenant from a route parameter.</summary>
    Route,

    /// <summary>Use the fixed "system" tenant (no per-request resolution).</summary>
    System,
}

/// <summary>
/// Opts a domain assembly into REST controller generation and sets shared options for the
/// generated controllers.
/// </summary>
/// <remarks>
/// The default route-prefix convention is <c>api/{domain}</c>; pass that value (or a custom prefix)
/// as the route prefix. Messages without a <see cref="RestRouteAttribute"/> fall back to convention
/// routing relative to <see cref="RoutePrefix"/> (see <see cref="RestRouteAttribute"/>).
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class RestApiAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="RestApiAttribute"/> class.
    /// </summary>
    /// <param name="routePrefix">The shared route prefix for the domain's generated controllers (convention: "api/{domain}").</param>
    /// <param name="tag">The optional OpenAPI tag for the generated controllers; defaults to null.</param>
    /// <param name="tenantSource">The source used to resolve the tenant; defaults to <see cref="RestTenantSource.Claims"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="routePrefix"/> is null.</exception>
    public RestApiAttribute(
        string routePrefix,
        string? tag = null,
        RestTenantSource tenantSource = RestTenantSource.Claims) {
        ArgumentNullException.ThrowIfNull(routePrefix);
        RoutePrefix = routePrefix;
        Tag = tag;
        TenantSource = tenantSource;
    }

    /// <summary>Gets the shared route prefix for the domain's generated controllers.</summary>
    public string RoutePrefix { get; }

    /// <summary>Gets the optional OpenAPI tag for the generated controllers.</summary>
    public string? Tag { get; }

    /// <summary>Gets the source used to resolve the tenant for the generated endpoints.</summary>
    public RestTenantSource TenantSource { get; }
}
