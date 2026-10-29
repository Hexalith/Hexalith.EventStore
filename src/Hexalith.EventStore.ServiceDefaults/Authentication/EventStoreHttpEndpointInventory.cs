using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Validates explicit endpoint classification and the actual policies and schemes it resolves.</summary>
public static class EventStoreHttpEndpointInventory
{
    /// <summary>Tests an exact support-safe anonymous probe route, without prefix or template exemptions.</summary>
    /// <param name="route">The literal route.</param>
    /// <returns>Whether the route is an approved probe.</returns>
    public static bool IsProbe(string? route) => route is "/health" or "/alive" or "/ready";

    /// <summary>Validates every endpoint against the host's registered policy and scheme providers.</summary>
    /// <param name="endpoints">The effective endpoints.</param>
    /// <param name="services">The host services.</param>
    /// <returns>Support-safe inventory failures.</returns>
    public static async Task<IReadOnlyList<string>> ValidateAsync(IEnumerable<Endpoint> endpoints, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(services);
        var policies = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>();
        List<string> failures = [];
        AuthorizationPolicy? fallback = await policies.GetFallbackPolicyAsync().ConfigureAwait(false);
        if (fallback is null || !fallback.Requirements.Any(static requirement => requirement is EventStoreUnclassifiedEndpointRequirement))
        {
            failures.Add("The host fallback must deny unclassified endpoints independently of credentials.");
        }
        foreach (Endpoint endpoint in endpoints)
        {
            string route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.DisplayName ?? "endpoint";
            string label = route.StartsWith('/') ? route : "/" + route;
            bool anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (anonymous)
            {
                if (!IsProbe(route) || endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
                {
                    failures.Add($"{label}: only exact probes may be anonymous.");
                }
                continue;
            }

            IReadOnlyList<IAuthorizeData> data = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (data.Count == 0 || data.Any(static item => string.IsNullOrWhiteSpace(item.Policy)))
            {
                failures.Add($"{label}: every protected endpoint must name a policy.");
                continue;
            }

            bool framework = route.TrimStart('/') is "dapr/subscribe" or "dapr/config" or "healthz"
                || route.TrimStart('/').StartsWith("actors/", StringComparison.OrdinalIgnoreCase)
                || endpoint.Metadata.Any(static item => item.GetType().GetInterfaces()
                    .Any(static type => type.FullName == "Dapr.ITopicMetadata"));
            if (framework && (data.Count != 1
                || data[0].Policy != EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy))
            {
                failures.Add($"{label}: framework deliveries must require only the named channel policy.");
            }

            foreach (IAuthorizeData item in data)
            {
                AuthorizationPolicy? policy = await policies.GetPolicyAsync(item.Policy!).ConfigureAwait(false);
                if (policy is null || policy.AuthenticationSchemes.Count == 0
                    || !policy.Requirements.Any(static requirement => requirement is DenyAnonymousAuthorizationRequirement))
                {
                    failures.Add($"{label}: named policies must require authentication and explicit schemes.");
                    continue;
                }

                foreach (string scheme in policy.AuthenticationSchemes.Concat((item.AuthenticationSchemes ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
                {
                    if (await schemes.GetSchemeAsync(scheme).ConfigureAwait(false) is null)
                    {
                        failures.Add($"{label}: an authentication scheme is not registered.");
                    }
                }
            }
        }
        return failures;
    }
}
