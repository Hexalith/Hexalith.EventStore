using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Private witness created only by the actual SDK subscription mapper.</summary>
internal sealed class DomainRouteCatalogSidecarMetadata(RequestDelegate? actualRequest, string path, string pubsub, string topic)
{
    internal const string Operation = "sdk-sidecar:domain-events";

    internal RouteCatalogSidecarTransport Observe(RouteEndpoint endpoint)
    {
        if (actualRequest is null || !ReferenceEquals(actualRequest, endpoint.RequestDelegate)
            || endpoint.RoutePattern.RawText != path || endpoint.RoutePattern.Parameters.Count != 0
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy == EventStoreDomainServicePolicies.SidecarChannel))
            throw new RouteCatalogUnavailableException("The SDK subscription delegate or channel policy differs from its actual receiver.");
        var assembly = typeof(EventStoreDomainEventsEndpointExtensions).Assembly;
        return new(typeof(EventStoreDomainEventsEndpointExtensions).FullName!,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))), pubsub, topic);
    }

    internal static RouteCatalogSidecarTransport? ObserveEndpoint(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>()?.Observe(endpoint);
}
