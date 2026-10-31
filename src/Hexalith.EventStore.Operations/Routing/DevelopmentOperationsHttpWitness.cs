using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Operations.Endpoints;
using Hexalith.EventStore.Operations.Security;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Operations.Routing;

/// <summary>Private delegate witness for the five existing SDK dead-letter operator endpoints.</summary>
internal sealed class DevelopmentOperationsHttpWitness(RequestDelegate? actualRequest, MethodInfo handler, string operation)
    : IDevelopmentSdkHttpWitness
{
    public RouteCatalogHttpTransport? Observe(RouteEndpoint endpoint)
    {
        (string path, string verb, string method) = operation switch
        {
            "count" => ("/internal/dead-letters/count", "GET", "CountAsync"),
            "list" => ("/internal/dead-letters", "GET", "ListAsync"),
            "retry" => ("/internal/dead-letters/retry", "POST", "RetryAsync"),
            "skip" => ("/internal/dead-letters/skip", "POST", "SkipAsync"),
            "archive" => ("/internal/dead-letters/archive", "POST", "ArchiveAsync"),
            _ => throw new RouteCatalogUnavailableException("The SDK Operations endpoint is not in its closed inventory."),
        };
        if (actualRequest is null || !ReferenceEquals(actualRequest, endpoint.RequestDelegate)
            || handler.DeclaringType != typeof(DeadLetterOperationsEndpointExtensions) || !handler.IsStatic || handler.Name != method
            || endpoint.RoutePattern.RawText != path || endpoint.RoutePattern.Parameters.Count != 0
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual([verb], StringComparer.Ordinal) != true
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy == OperationsHttpSecurityExtensions.PolicyPrefix + operation)
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy == EventStoreHttpHostSecurityExtensions.HumanPolicy))
            return null;
        return new(typeof(DeadLetterOperationsEndpointExtensions).FullName!, verb,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(DeadLetterOperationsEndpointExtensions).Assembly.Location))));
    }
}
