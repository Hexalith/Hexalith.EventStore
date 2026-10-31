using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Server.Routing;

/// <summary>Observes real SDK actor registration and native Dapr endpoint delegates; message guards remain actor-owned.</summary>
internal sealed class DevelopmentNativeActorReceiver(ActorRuntime runtime, IOptions<ActorRuntimeOptions> options, HealthCheckService healthChecks, IConfiguration configuration) : IDevelopmentNativeActorReceiver
{
    private static readonly IReadOnlyDictionary<string, (string Kind, string Verb)> Paths = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["dapr/config"] = ("config", "GET"), ["healthz"] = ("health", "GET"),
        ["actors/{actorTypeName}/{actorId}"] = ("deactivate", "DELETE"),
        ["actors/{actorTypeName}/{actorId}/method/{methodName}"] = ("method", "PUT"),
        ["actors/{actorTypeName}/{actorId}/method/remind/{reminderName}"] = ("reminder", "PUT"),
        ["actors/{actorTypeName}/{actorId}/method/timer/{timerName}"] = ("timer", "PUT"),
    };

    public RouteCatalogNativeActorTransport? Observe(RouteEndpoint endpoint, TechnicalHttpObservationPass? pass = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!Paths.TryGetValue(endpoint.RoutePattern.RawText!.Trim('/'), out var path)
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(policy => policy.Policy)
                .SequenceEqual([EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy], StringComparer.Ordinal)
            || (path.Kind == "health" ? endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Count is > 0
                : endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual([path.Verb], StringComparer.Ordinal) != true)
            || endpoint.RequestDelegate is not { } invoke || invoke.GetInvocationList().Length != 1) return null;
        if (path.Kind == "health")
        {
            if (endpoint.DisplayName != "Dapr Actors Health Check" || !HealthDelegate(invoke)) return null;
        }
        else
        {
            if (invoke.Method.Module.Assembly != typeof(ActorsEndpointRouteBuilderExtensions).Assembly || invoke.Target is not { } closure
                || closure.GetType().DeclaringType != typeof(ActorsEndpointRouteBuilderExtensions)
                || invoke.Method.DeclaringType != closure.GetType()) return null;
            var fields = closure.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.FieldType == typeof(ActorRuntime)).ToArray();
            if (fields.Length != 1 || !ReferenceEquals(fields[0].GetValue(closure), runtime)) return null;
        }
        var actors = Registrations();
        string identity = string.Join('\n', actors.OrderBy(actor => actor.Type.ActorTypeName, StringComparer.Ordinal).Select(actor =>
            actor.Type.ActorTypeName + "|" + actor.Type.ImplementationType.FullName + "|" + actor.Type.IsRemindable + "|"
            + string.Join(';', actor.Type.InterfaceTypes.SelectMany(type => type.GetMethods()).Select(method => method.ToString()).Order(StringComparer.Ordinal))));
        string code = Digest(typeof(AggregateActor).Assembly, pass);
        string framework = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            Digest(typeof(ActorsEndpointRouteBuilderExtensions).Assembly, pass) + "|" + Digest(typeof(ActorRuntime).Assembly, pass) + "|" + Digest(typeof(HealthCheckMiddleware).Assembly, pass))));
        return new(path.Kind, path.Verb, code, framework, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity))));
    }

    public bool Allows(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var caller = context.Request.Headers["dapr-caller-app-id"];
        var callerNamespace = context.Request.Headers["dapr-caller-namespace"];
        var callee = context.Request.Headers["dapr-callee-app-id"];
        string? ownApp = configuration["EventStore:DomainService:AppId"];
        if (caller.Count > 1 || callerNamespace.Count > 1 || callee.Count > 1
            || caller.Count == 1 && (string.IsNullOrEmpty(ownApp) || caller[0] != ownApp || callerNamespace.Count != 1 || string.IsNullOrWhiteSpace(callerNamespace[0]))
            || caller.Count == 0 && callerNamespace.Count != 0
            || callerNamespace.Count == 1 && configuration["NAMESPACE"] is { } ownNamespace && callerNamespace[0] != ownNamespace
            || callee.Count == 1 && (string.IsNullOrEmpty(ownApp) || callee[0] != ownApp)
            || context.GetEndpoint() is not RouteEndpoint endpoint || Observe(endpoint) is not { } native) return false;
        // Pinned Dapr1.18.7 strips HTTP caller identity and stamps its actual actor caller;
        // infrastructure callbacks may omit it. API-Call is telemetry and supplies no authority.
        // Native and local self service calls retain the SDK's existing same-app channel trust.
        if (native.Kind is "config" or "health") return true;
        var values = context.Request.RouteValues;
        if (values["actorTypeName"] is not string type || values["actorId"] is not string { Length: > 0 }) return false;
        var actor = Registrations().SingleOrDefault(actor => actor.Type.ActorTypeName == type);
        if (actor is null) return false;
        return native.Kind switch
        {
            "method" => values["methodName"] is string method && actor.Type.InterfaceTypes.SelectMany(contract => contract.GetMethods()).Any(candidate => candidate.Name == method),
            "reminder" => actor.Type.IsRemindable && values["reminderName"] is string { Length: > 0 },
            "timer" => values["timerName"] is string { Length: > 0 }, // The real runtime requires its actual per-instance registered timer.
            "deactivate" => true,
            _ => false,
        };
    }

    public bool AllowsSnapshot(HttpContext context, CommittedRouteCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(snapshot);
        return context.Request.RouteValues["actorTypeName"] as string != RouteCatalogProjectionActorPolicy.ActorType
            || context.Request.RouteValues["methodName"] as string != RouteCatalogProjectionActorPolicy.Method
            || snapshot.Envelope.Declarations.Any(declaration => RouteCatalogProjectionActorPolicy.IsQuery(declaration)
                && snapshot.Envelope.Routes.Any(route => route.RouteId == declaration.RouteId && route.AppId == snapshot.LocalAppId));
    }

    private ActorRegistration[] Registrations()
    {
        var actors = options.Value.Actors.ToArray();
        if (actors.Length == 0 || actors.Select(actor => actor.Type.ActorTypeName).Distinct(StringComparer.Ordinal).Count() != actors.Length
            || actors.Any(actor => actor.Type.ActorTypeName == RouteCatalogProjectionActorPolicy.ActorType && actor.Type.ImplementationType != typeof(EventReplayProjectionActor)
                || actor.Type.ImplementationType.Assembly != typeof(AggregateActor).Assembly || actor.Type.IsAbstract
                || actor.Activator is not null && actor.Activator.GetType().FullName != "Dapr.Actors.Runtime.DefaultActorActivator"))
            throw new RouteCatalogUnavailableException("The actual native receiver requires the closed installed SDK actor implementations and activator.");
        return actors;
    }

    private bool HealthDelegate(RequestDelegate invoke)
    {
        Type type = typeof(HealthCheckMiddleware);
        object? middleware = invoke.Target?.GetType() == type ? invoke.Target : null;
        if (middleware is null && invoke.Method.Module.Assembly == typeof(UseMiddlewareExtensions).Assembly
            && invoke.Method.DeclaringType?.FullName?.StartsWith("Microsoft.AspNetCore.Builder.UseMiddlewareExtensions+ReflectionMiddlewareBinder+", StringComparison.Ordinal) == true)
            middleware = invoke.Target!.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.GetValue(invoke.Target)).SingleOrDefault(value => value?.GetType() == type);
        return middleware is not null && middleware.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(HealthCheckService) && ReferenceEquals(field.GetValue(middleware), healthChecks));
    }

    private static string Digest(Assembly assembly, TechnicalHttpObservationPass? pass)
        => (pass?.AssemblyIdentity(assembly) ?? TechnicalHttpActionObservation.AssemblyIdentity(assembly))[^64..];
}
