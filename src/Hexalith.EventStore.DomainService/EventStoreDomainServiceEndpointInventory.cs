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
/// <item><description>Only the exact health, alive, and ready probes may carry anonymous metadata.</description></item>
/// <item><description>Every canonical operational route must carry its exact catalog policy (<see cref="EventStoreDomainServiceRoutes"/>).
/// No canonical route may be public.</description></item>
/// <item><description>Every sidecar-originated route — <c>dapr/subscribe</c>, every pub/sub subscription, and every
/// Dapr actor route — must carry <see cref="EventStoreDomainServicePolicies.SidecarChannel"/>, including one a host
/// mapped itself before the SDK.</description></item>
/// <item><description>Every other endpoint must name an explicit authorization policy. The fallback never classifies it.</description></item>
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
        var violations = new List<string>();
        foreach (RouteEndpoint endpoint in endpoints.OfType<RouteEndpoint>())
        {
            string route = EventStoreDomainServiceRoutes.Normalize(endpoint.RoutePattern.RawText);
            bool isProbe = Hexalith.EventStore.ServiceDefaults.Authentication.EventStoreHttpEndpointInventory.IsProbe(endpoint.RoutePattern.RawText);
            bool isAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            IReadOnlyList<IAuthorizeData> authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            IReadOnlyList<EventStorePublicEndpointMetadata> publicMarkers = endpoint.Metadata.GetOrderedMetadata<EventStorePublicEndpointMetadata>();
            if (isProbe)
            {
                if (publicMarkers.Count > 0)
                {
                    violations.Add($"{route}: a probe cannot carry a public-endpoint marker.");
                }

                continue;
            }

            if (IsSidecarOriginated(endpoint, route))
            {
                if (isAnonymous || publicMarkers.Count > 0)
                {
                    violations.Add($"{route}: a sidecar-originated route cannot allow anonymous access or carry a public marker.");
                }

                if (!authorizeData.Any(static data => string.Equals(data.Policy, EventStoreDomainServicePolicies.SidecarChannel, StringComparison.Ordinal)))
                {
                    violations.Add($"{route}: a sidecar-originated route must require policy '{EventStoreDomainServicePolicies.SidecarChannel}' (call RequireEventStoreSidecarChannel()).");
                }

                continue;
            }

            if (EventStoreDomainServiceRoutes.TryGet(route, out EventStoreDomainServiceRoute? catalogRoute))
            {
                if (isAnonymous || publicMarkers.Count > 0)
                {
                    violations.Add($"{route}: a canonical route cannot be public.");
                }

                if (!authorizeData.Any(data => string.Equals(data.Policy, catalogRoute!.Policy, StringComparison.Ordinal)))
                {
                    violations.Add($"{route}: the effective endpoint must require policy '{catalogRoute!.Policy}'.");
                }


                continue;
            }

            if (isAnonymous)
            {
                violations.Add($"{route}: only exact health, alive, and ready probes may be anonymous.");
                continue;
            }

            if (publicMarkers.Count > 0)
            {
                violations.Add($"{route}: a public-endpoint marker requires anonymous metadata.");
                continue;
            }

            if (authorizeData.Count == 0 || authorizeData.Any(static data => string.IsNullOrWhiteSpace(data.Policy)))
            {
                violations.Add($"{route}: the endpoint must name an explicit authorization policy.");
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

}
