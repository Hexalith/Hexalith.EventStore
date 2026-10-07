using Dapr;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Verifies the authorization metadata of every endpoint a domain-service host exposes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Only <c>/health</c>, <c>/alive</c>, and <c>/ready</c> may carry anonymous metadata.</description></item>
/// <item><description>Every effective canonical operational route, including a host override, must carry its exact
/// catalog policy (<see cref="EventStoreDomainServiceRoutes"/>) and no anonymous metadata.</description></item>
/// <item><description>Every sidecar-originated route — <c>dapr/subscribe</c>, every pub/sub subscription, and every
/// Dapr actor route — must carry <see cref="EventStoreDomainServicePolicies.SidecarChannel"/>, including one a host
/// mapped itself before the SDK.</description></item>
/// <item><description>Every other endpoint must carry explicit authorization metadata unless a fallback policy that
/// denies anonymous callers protects it.</description></item>
/// </list>
/// </remarks>
public static class EventStoreDomainServiceEndpointInventory
{
    private static readonly string[] SidecarExactRoutes = ["/dapr/subscribe", "/dapr/config", "/healthz"];

    /// <summary>
    /// Returns every support-safe inventory violation.
    /// </summary>
    /// <param name="endpoints">The endpoints exposed by the host.</param>
    /// <param name="fallbackPolicy">The host fallback policy, or <see langword="null"/> when none is configured.</param>
    /// <returns>The violations; empty when the inventory is compliant.</returns>
    public static IReadOnlyList<string> Validate(IEnumerable<Endpoint> endpoints, AuthorizationPolicy? fallbackPolicy)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        bool fallbackProtects = DeniesAnonymous(fallbackPolicy);
        var violations = new List<string>();
        foreach (RouteEndpoint endpoint in endpoints.OfType<RouteEndpoint>())
        {
            string route = EventStoreDomainServiceRoutes.Normalize(endpoint.RoutePattern.RawText);
            bool isProbe = EventStoreDomainServiceRoutes.AnonymousProbeRoutes.Contains(route, StringComparer.OrdinalIgnoreCase);
            bool isAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            IReadOnlyList<IAuthorizeData> authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (isProbe)
            {
                continue;
            }

            if (isAnonymous)
            {
                violations.Add($"{route}: anonymous access is limited to {string.Join(", ", EventStoreDomainServiceRoutes.AnonymousProbeRoutes)}.");
                continue;
            }

            if (IsSidecarOriginated(endpoint, route))
            {
                if (!authorizeData.Any(static data => string.Equals(data.Policy, EventStoreDomainServicePolicies.SidecarChannel, StringComparison.Ordinal)))
                {
                    violations.Add($"{route}: a sidecar-originated route must require policy '{EventStoreDomainServicePolicies.SidecarChannel}' (call RequireEventStoreSidecarChannel()).");
                }

                continue;
            }

            if (EventStoreDomainServiceRoutes.TryGet(route, out EventStoreDomainServiceRoute? catalogRoute)
                && SupportsPost(endpoint)
                && !authorizeData.Any(data => string.Equals(data.Policy, catalogRoute!.Policy, StringComparison.Ordinal)))
            {
                violations.Add($"{route}: the effective endpoint must require policy '{catalogRoute!.Policy}'.");
                continue;
            }

            if (!fallbackProtects && authorizeData.Count == 0)
            {
                violations.Add($"{route}: the endpoint has no authorization metadata and no fallback policy that denies anonymous callers protects it.");
            }
        }

        return violations;
    }

    /// <summary>
    /// Gets whether a fallback policy denies anonymous callers. A fallback that admits anonymous callers protects
    /// nothing, so the inventory never counts it.
    /// </summary>
    /// <param name="policy">The fallback policy.</param>
    /// <returns><see langword="true"/> when the policy requires an authenticated user.</returns>
    public static bool DeniesAnonymous(AuthorizationPolicy? policy)
        => policy?.Requirements.Any(static requirement => requirement is DenyAnonymousAuthorizationRequirement) == true;

    /// <summary>
    /// Gets whether an endpoint receives deliveries the Dapr sidecar itself originates: subscription discovery, pub/sub
    /// subscriptions, and actor runtime calls.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="normalizedRoute">The endpoint route normalized with <see cref="EventStoreDomainServiceRoutes.Normalize"/>.</param>
    /// <returns><see langword="true"/> for a sidecar-originated route.</returns>
    public static bool IsSidecarOriginated(RouteEndpoint endpoint, string normalizedRoute)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(normalizedRoute);
        return SidecarExactRoutes.Contains(normalizedRoute, StringComparer.OrdinalIgnoreCase)
            || normalizedRoute.StartsWith("/actors/", StringComparison.OrdinalIgnoreCase)
            || endpoint.Metadata.GetMetadata<ITopicMetadata>() is not null;
    }

    private static bool SupportsPost(RouteEndpoint endpoint)
    {
        IHttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
        return methods is null || methods.HttpMethods.Contains(HttpMethods.Post, StringComparer.OrdinalIgnoreCase);
    }
}
