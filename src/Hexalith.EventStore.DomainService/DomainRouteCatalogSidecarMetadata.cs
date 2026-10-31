using System.Security.Cryptography;

using Dapr;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Private witness created only by the actual SDK subscription mapper.</summary>
internal sealed class DomainRouteCatalogSidecarMetadata(RequestDelegate? actualRequest, string path, string pubsub, string topic, bool declarative)
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
        var topics = endpoint.Metadata.GetOrderedMetadata<ITopicMetadata>();
        if (endpoint.Metadata.GetOrderedMetadata<IOriginalTopicMetadata>().Count != 0
            || endpoint.Metadata.GetOrderedMetadata<Dapr.AspNetCore.IBulkSubscribeMetadata>().Count != 0
            || (declarative ? topics.Count != 0 : topics.Count != 1 || topics[0] is not TopicAttribute actual
                || actual.GetType() != typeof(TopicAttribute) || actual.PubsubName != pubsub || actual.Name != topic
                || actual.EnableRawPayload != false || actual.DeadLetterTopic is not null || actual.MetadataSeparator is not null
                || actual.OwnedMetadatas is { Length: > 0 } || !string.IsNullOrEmpty(actual.Match) || actual.Priority != 0))
            throw new RouteCatalogUnavailableException("The actual SDK subscription metadata differs from its configured component, topic or mode.");
        var assembly = typeof(EventStoreDomainEventsEndpointExtensions).Assembly;
        return new(typeof(EventStoreDomainEventsEndpointExtensions).FullName!,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))), pubsub, topic);
    }

    internal static RouteCatalogSidecarTransport? ObserveEndpoint(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<DomainRouteCatalogSidecarMetadata>()?.Observe(endpoint);
}
