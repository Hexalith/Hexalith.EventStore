using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Dapr;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Routing;

/// <summary>Closes private Development callback observations over the actual installed Dapr mapper and SDK notification action.</summary>
internal sealed class DevelopmentDaprCallbackReceiver(EndpointDataSource endpoints, IConfiguration configuration,
    IOptions<ProjectionChangeNotifierOptions> options) : IDevelopmentDaprCallbackReceiver
{
    private static readonly MethodInfo Notify = typeof(ProjectionNotificationController).GetMethod(nameof(ProjectionNotificationController.OnProjectionChanged))!;

    public RouteCatalogDaprCallbackTransport? Observe(RouteEndpoint endpoint, TechnicalHttpObservationPass? pass = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        string path = endpoint.RoutePattern.RawText?.Trim('/') ?? "";
        string? kind = path == "dapr/subscribe" && !Declarative && Discovery(endpoint) ? "subscribe"
            : path == "projections/changed" && Notification(endpoint) ? "projection-notification" : null;
        if (kind is null || !Channel(endpoint) || !Inventory()) return null;
        string verb = kind == "subscribe" ? "GET" : "POST";
        if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual([verb], StringComparer.Ordinal) != true) return null;
        string identity = "projection-notification|" + options.Value.PubSubName + "|*.*.projection-changed|" + Declarative;
        string code = Digest(typeof(ProjectionNotificationController).Assembly, pass);
        string framework = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            Digest(typeof(DaprEndpointRouteBuilderExtensions).Assembly, pass) + "|" + Digest(typeof(ControllerBase).Assembly, pass))));
        return new(kind, verb, code, framework, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity))));
    }

    public bool IsAbsentDiscovery(HttpContext context) => Declarative && Inventory() && context.GetEndpoint() is null
        && HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/dapr/subscribe"
        && !endpoints.Endpoints.OfType<RouteEndpoint>().Any(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "dapr/subscribe");

    private bool Declarative => configuration.GetValue<bool>("Dapr:DeclarativeSubscriptions");

    private bool Inventory()
    {
        if (string.IsNullOrWhiteSpace(options.Value.PubSubName)) return false;
        var routes = endpoints.Endpoints.OfType<RouteEndpoint>().ToArray();
        var receivers = routes.Where(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "projections/changed").ToArray();
        if (receivers.Length != 1 || !Notification(receivers[0]) || !Channel(receivers[0])) return false;
        var topics = routes.Where(endpoint => endpoint.Metadata.GetOrderedMetadata<ITopicMetadata>().Count > 0).ToArray();
        if (Declarative) return topics.Length == 0;
        return topics.Length == 1 && ReferenceEquals(topics[0], receivers[0]);
    }

    private bool Notification(RouteEndpoint endpoint)
    {
        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerTypeInfo.AsType() != typeof(ProjectionNotificationController) || action.MethodInfo != Notify
            || !DevelopmentTechnicalHttpTransport.ExecutesAction(endpoint.RequestDelegate, action)
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true
            || endpoint.Metadata.GetOrderedMetadata<IOriginalTopicMetadata>().Count != 0
            || endpoint.Metadata.GetOrderedMetadata<Dapr.AspNetCore.IBulkSubscribeMetadata>().Count != 0) return false;
        var topics = endpoint.Metadata.GetOrderedMetadata<ITopicMetadata>();
        if (Declarative) return topics.Count == 0;
        return topics.Count == 1 && topics[0] is TopicAttribute topic && topic.GetType() == typeof(TopicAttribute)
            && topic.EnableRawPayload is null && topic.DeadLetterTopic is null && topic.MetadataSeparator is null
            && topic.OwnedMetadatas is not { Length: > 0 } && topic.Name == "*.*.projection-changed"
            && topics[0].PubsubName == options.Value.PubSubName && string.IsNullOrEmpty(topics[0].Match) && topics[0].Priority == 0;
    }

    private static bool Channel(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
        && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(policy => policy.Policy)
            .SequenceEqual([EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy], StringComparer.Ordinal);

    private static bool Discovery(RouteEndpoint endpoint)
    {
        if (endpoint.RequestDelegate is not { } invoke || invoke.GetInvocationList().Length != 1
            || invoke.Method.Module.Assembly != typeof(DaprEndpointRouteBuilderExtensions).Assembly
            || invoke.Target is not { } closure || closure.GetType().DeclaringType != typeof(DaprEndpointRouteBuilderExtensions)
            || invoke.Method.DeclaringType != closure.GetType() || !invoke.Method.Name.StartsWith("<CreateSubscribeEndPoint>", StringComparison.Ordinal)) return false;
        var fields = closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType.FullName == "Dapr.SubscribeOptions").ToArray();
        return fields.Length == 1 && fields[0].GetValue(closure) is null;
    }

    private static string Digest(Assembly assembly, TechnicalHttpObservationPass? pass)
        => (pass?.AssemblyIdentity(assembly) ?? TechnicalHttpActionObservation.AssemblyIdentity(assembly))[^64..];
}
