using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Captures local named handler metadata and the canonical operation policy installed on actual receiver endpoints.</summary>
public static class RouteCatalogDomainObservation
{
    /// <summary>Uses the same registered handlers, options and capability protocol as the receiver metadata endpoint.</summary>
    public static IReadOnlyList<RouteCatalogProjectionCapability> Capabilities(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var named = services.GetServices<IAsyncDomainProjectionHandler>().ToArray();
        if (named.Length == 0) return [];
        var identity = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.ServiceVersion);
        string appId = identity.AppId;
        string version = identity.ServiceVersion;
        var domains = Domains(services);
        var legacy = services.GetServices<IDomainProjectionHandler>().ToArray();
        var keyed = services.GetRequiredService<IServiceProviderIsKeyedService>();
        var result = new List<RouteCatalogProjectionCapability>();
        foreach (string domain in domains)
        {
            var response = AdminOperationalIndexMetadata.Create(services.GetRequiredService<DiscoveryResult>(), [domain],
                services.GetServices<IDomainQueryHandler>(), named, appId, version,
                services.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value, domains.Where(candidate => keyed.IsKeyedService(typeof(IDomainProcessor), candidate)
                    || keyed.IsKeyedService(typeof(IAsyncDomainProcessor), candidate)).ToArray(), legacy);
            response = DomainProjectionOwnerProof.BindMetadata(response, services.GetServices<DomainProjectionOwnerProofRegistration>(), legacy, named);
            response = DomainProjectionReplayRecovery.BindMetadata(services, response, named);
            response = LegacyProjectionCatalogRegistration.Bind(response, services.GetServices<LegacyProjectionCatalogRegistration>());
            foreach (var entry in response.Domains.Where(d => d.NamedProjectionTypes is { Count: > 0 }))
            {
                if (string.IsNullOrWhiteSpace(response.CatalogFingerprint))
                    throw new RouteCatalogUnavailableException("Actual named handlers lack their capability fingerprint.");
                result.Add(new(appId, domain, version, response.CatalogFingerprint, entry.NamedProjectionTypes!));
            }
        }
        return result.AsReadOnly();
    }

    /// <summary>Reads effective endpoint policies; domains come from independently discovered local registrations.</summary>
    public static IReadOnlyList<RouteCatalogAuxiliaryOperation> Auxiliary(IServiceProvider services, IEnumerable<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoints);
        string appId = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value.AppId;
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var domains = Domains(services);
        var result = new List<RouteCatalogAuxiliaryOperation>();
        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            if (endpoint.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>() is { } subscription)
            {
                var owner = subscription.Observe(endpoint);
                foreach (string domain in domains) result.Add(new(appId, endpoint.RoutePattern.RawText!.TrimStart('/'),
                    DomainRouteCatalogSidecarMetadata.Operation, domain) { Sidecar = owner });
                continue;
            }
            var route = EventStoreDomainServiceRoutes.Operational.SingleOrDefault(r => r.Route == endpoint.RoutePattern.RawText);
            if (route is null) continue;
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == route.Policy)
                || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true)
                throw new RouteCatalogUnavailableException("An actual receiver endpoint disagrees with its canonical operation policy.");
            foreach (string domain in domains) result.Add(new(appId, route.Route.TrimStart('/'), route.Operation, domain));
        }
        return result.AsReadOnly();
    }
    private static string[] Domains(IServiceProvider services) => [.. services.GetRequiredService<DiscoveryResult>().Aggregates.Select(d => d.DomainName)
        .Concat(services.GetServices<IDomainQueryHandler>().Select(h => h.Domain))
        .Concat(services.GetServices<IAsyncDomainProjectionHandler>().Select(h => h.Domain))
        .Concat(services.GetServices<IDomainProjectionHandler>().Select(h => h.Domain)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

}
