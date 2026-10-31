using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Observes an explicitly selected Development caller that owns no EventStore SDK receiver.</summary>
public static class DevelopmentAuxiliaryClientCatalog
{
    /// <summary>Captures actual endpoint and loaded deny-all ACL facts; never constructs catalog operations.</summary>
    public static async Task<RouteCatalogHostObservation> CaptureAsync(IServiceProvider services,
        IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!services.GetRequiredService<IHostEnvironment>().IsDevelopment()
            || configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] != "aux-client")
            throw Refusal();
        string appId = configuration["EventStore:DomainService:AppId"] ?? throw Refusal();
        if (string.IsNullOrWhiteSpace(appId)
            || configuration["DAPR_APP_ID"] is { Length: > 0 } sidecarAppId && sidecarAppId != appId)
            throw Refusal();

        // Use the routing service's actual materialized sources, including MVC/minimal/Razor endpoints.
        Endpoint[] endpoints = [.. services.GetRequiredService<EndpointDataSource>().Endpoints];
        var installed = services.GetRequiredService<IServiceProviderIsService>();
        string[] receiverServices = ["Hexalith.EventStore.DomainService.EventStoreDomainServiceEndpointSource",
            "Hexalith.EventStore.Server.Commands.ICommandRouter", "Hexalith.EventStore.Server.Queries.IQueryRouter",
            "Dapr.Actors.Runtime.ActorRuntime"];
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (string name in receiverServices)
                if (assembly.GetType(name, throwOnError: false) is { } type && installed.IsService(type))
                    throw Refusal();

        var inventory = new List<RouteCatalogReceiverEndpoint>();
        foreach (Endpoint endpoint in endpoints)
        {
            if (endpoint is not RouteEndpoint route || route.RoutePattern.RawText is not { } pattern)
                throw Refusal();
            string path = "/" + pattern.Trim('/');
            if (route.Metadata.GetMetadata<RouteCatalogEndpointAttribute>() is not null
                || route.Metadata.Any(metadata => metadata.GetType().FullName is
                    "Hexalith.EventStore.DomainService.DomainRouteCatalogGuardMetadata"
                    or "Hexalith.EventStore.Extensions.EventStoreActorCallbackEndpointMetadata")
                || IsSdkReceiver(path))
                throw Refusal();
            inventory.Add(new(path,
                route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [],
                null, route.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray(),
                route.Metadata.GetMetadata<IAllowAnonymous>() is not null, route.RoutePattern.Parameters.Count > 0, false));
        }
        RouteCatalogHostObservation actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration,
            appId, [], [], [], [], [], cancellationToken).ConfigureAwait(false);
        using JsonDocument loaded = JsonDocument.Parse(actual.TopologyDocuments[1]);
        JsonElement acl = loaded.RootElement.GetProperty("spec").GetProperty("accessControl");
        if (acl.GetProperty("defaultAction").GetString() != "deny"
            || acl.GetProperty("policies").EnumerateArray().Any(policy =>
                policy.TryGetProperty("defaultAction", out var action) && action.GetString() != "deny"
                || policy.GetProperty("operations").EnumerateArray().Any(operation => operation.GetProperty("action").GetString() != "deny"))
            || actual.DeclaredInvocations is not { Count: 0 })
            throw Refusal();
        return actual with { ReceiverSurface = new("eventstore-development-client-only-v1",
            inventory.OrderBy(endpoint => endpoint.Pattern, StringComparer.Ordinal).ToArray()) };
    }

    private static bool IsSdkReceiver(string path)
        => path.ToLowerInvariant() is "/process" or "/replay-state" or "/query" or "/project" or "/admin/operational-index-metadata"
            or "/dapr/config" or "/dapr/subscribe"
            || path.StartsWith("/project/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/actors/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/commands", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/queries", StringComparison.OrdinalIgnoreCase);

    private static RouteCatalogUnavailableException Refusal()
        => new("The explicit Development auxiliary client must own no SDK receiver or incoming Dapr grant.", RouteCatalogRefusalReason.Inventory);
}
