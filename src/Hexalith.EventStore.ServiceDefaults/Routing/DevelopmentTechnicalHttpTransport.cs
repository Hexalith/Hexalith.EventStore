using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Reads the closed existing SDK administrative HTTP surface from actual compiled MVC actions.</summary>
public static class DevelopmentTechnicalHttpTransport
{
    private static readonly ServiceProvider RoutingServices = new ServiceCollection().AddRouting().BuildServiceProvider();
    private static readonly ConcurrentDictionary<(string Path, long Size, long Ticks), string> Code = new();
    private static readonly string[] Gateway = ["AdminCommandsQueryController", "AdminProjectionRebuildController", "AdminStorageCommandController",
        "AdminStreamQueryController", "AdminTraceQueryController", "CommandStatusController", "CommandValidationController",
        "CommandsController", "QueriesController", "QueryValidationController", "ReplayController", "RetainedIdentityHistoryController",
        "RoutingCatalogMetadataController", "StreamsController", "TrustedEffectsController"];
    private static readonly string[] Admin = ["AdminTypeCatalogController", "AdminTracesController", "AdminTenantsController", "AdminStreamsController",
        "AdminStorageController", "AdminProjectionsController", "AdminHealthController", "AdminDeadLettersController", "AdminDaprController",
        "AdminConsistencyController", "AdminBackupsController"];

    /// <summary>Returns a code-bound owner only for an actual existing authenticated SDK MVC action.</summary>
    public static RouteCatalogHttpTransport? Observe(RouteEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Metadata.GetMetadata<IDevelopmentSdkHttpWitness>() is { } witness
            && witness.GetType().Assembly.GetName().Name == "Hexalith.EventStore.Operations"
            && witness.GetType().FullName == "Hexalith.EventStore.Operations.Routing.DevelopmentOperationsHttpWitness")
            return witness.Observe(endpoint);
        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null || !ExecutesAction(endpoint.RequestDelegate, action)
            || action.MethodInfo.DeclaringType != action.ControllerTypeInfo.AsType()
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) return null;
        Type type = action.ControllerTypeInfo.AsType();
        string assembly = type.Assembly.GetName().Name!;
        bool gateway = assembly == "Hexalith.EventStore.Gateway" && type.Namespace == "Hexalith.EventStore.Controllers" && Gateway.Contains(type.Name, StringComparer.Ordinal);
        bool admin = assembly == "Hexalith.EventStore.Admin.Server" && type.Namespace == "Hexalith.EventStore.Admin.Server.Controllers" && Admin.Contains(type.Name, StringComparer.Ordinal);
        if (!gateway && !admin) return null;
        string[] policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(policy => policy.Policy ?? "").ToArray();
        var compiledPolicies = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true)).Select(attribute => attribute.Policy).Where(policy => policy is not null);
        if (compiledPolicies.Any(policy => !policies.Contains(policy!, StringComparer.Ordinal))) return null;
        if (policies.Any(string.IsNullOrWhiteSpace) || admin && !policies.Any(policy => policy is "AdminReadOnly" or "AdminOperator" or "AdminFull")) return null;
        string[] verbs = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
        if (verbs.Length == 0 || verbs.Any(verb => verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"))) return null;
        string pattern = (endpoint.RoutePattern.RawText ?? "").Trim('/');
        if (!RouteCatalogSdkHttpPolicy.Allows(type.FullName!, pattern, verbs, policies)) return null;
        var prefix = type.GetCustomAttribute<RouteAttribute>()?.Template;
        if (prefix is null || !action.MethodInfo.GetCustomAttributes<HttpMethodAttribute>().Any(attribute =>
            (prefix.Trim('/') + "/" + (attribute.Template ?? "").Trim('/')).Trim('/') == pattern
            && attribute.HttpMethods.Order(StringComparer.Ordinal).SequenceEqual(verbs, StringComparer.Ordinal))) return null;
        if (endpoint.RoutePattern.Parameters.Any(parameter => parameter.IsCatchAll || parameter.IsOptional || parameter.Default is not null)) return null;
        var file = new FileInfo(type.Assembly.Location);
        string digest = Code.GetOrAdd((file.FullName, file.Length, file.LastWriteTimeUtc.Ticks),
            key => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(key.Path))));
        return new(type.FullName!, string.Join(' ', verbs), digest);
    }

    /// <summary>Verifies the actual selected MVC delegate captures the same action descriptor.</summary>
    internal static bool ExecutesAction(RequestDelegate? request, ControllerActionDescriptor action)
    {
        if (request is null || request.GetInvocationList().Length != 1 || request.Target is not { } closure
            || request.Method.Module.Assembly != typeof(ControllerBase).Assembly
            || request.Method.DeclaringType != closure.GetType()
            || closure.GetType().DeclaringType?.FullName != "Microsoft.AspNetCore.Mvc.Routing.ControllerRequestDelegateFactory") return false;
        // ASP.NET's official MVC delegate captures the actual descriptor it executes. Metadata
        // alone can be copied onto another real MVC delegate and supplies no provenance.
        var fields = closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(ControllerActionDescriptor)).ToArray();
        return fields.Length == 1 && ReferenceEquals(fields[0].GetValue(closure), action);
    }

    /// <summary>Groups only actual admitted endpoint metadata into immutable activation bindings.</summary>
    public static IReadOnlyList<RouteCatalogAuxiliaryOperation> Auxiliary(RouteCatalogReceiverSurface surface, string appId, string domain)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return surface.Endpoints.Where(endpoint => endpoint.Admitted).GroupBy(endpoint => endpoint.Pattern, StringComparer.Ordinal)
            .Select(group =>
            {
                var endpoints = group.ToArray();
                if (endpoints.Select(endpoint => endpoint.Operation).Distinct(StringComparer.Ordinal).Count() != 1)
                    throw new RouteCatalogUnavailableException("The same technical route has conflicting compiled operations.");
                var first = endpoints[0];
                RouteCatalogHttpTransport? http = null;
                if (first.Http is { } owner)
                {
                    if (endpoints.Any(endpoint => endpoint.Http?.Owner != owner.Owner || endpoint.Http.CodeDigest != owner.CodeDigest))
                        throw new RouteCatalogUnavailableException("The same technical route has conflicting compiled code owners.");
                    http = owner with { Verbs = string.Join(' ', endpoints.SelectMany(endpoint => endpoint.Methods).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                        Grants = endpoints.SelectMany(endpoint => endpoint.Http!.Grants ?? []).Distinct().OrderBy(grant => grant.Method, StringComparer.Ordinal).ThenBy(grant => grant.Verbs, StringComparer.Ordinal).ToArray() };
                }
                if (endpoints.Any(endpoint => endpoint.DaprCallback != first.DaprCallback))
                    throw new RouteCatalogUnavailableException("The same Dapr callback has conflicting actual subscription inventories.");
                if (endpoints.Any(endpoint => endpoint.NativeActor != first.NativeActor))
                    throw new RouteCatalogUnavailableException("The same native route has conflicting installed framework inventories.");
                return new RouteCatalogAuxiliaryOperation(appId, first.Pattern.TrimStart('/'), first.Operation!, domain) { Http = http, NativeActor = first.NativeActor, DaprCallback = first.DaprCallback };
            }).ToArray();
    }

    /// <summary>Names transport only; this identity supplies no message ingress/eligibility.</summary>
    public static string? Operation(RouteEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        string method = endpoint.RoutePattern.RawText?.Trim('/') ?? "";
        foreach (string operation in new[] { EventStoreOperationIdentifiers.RouteCatalogRead, EventStoreOperationIdentifiers.RouteCatalogActivate })
            if (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy ==
                    EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, operation))
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: 1 } verbs
                && RouteCatalogEndpointExtensions.IsControlMethod(method, new HttpMethod(verbs[0]))) return operation;
        return Observe(endpoint) is null ? null : "sdk-http:" + method;
    }

    /// <summary>Matches one retained MVC route shape and exact verb; the receiver still verifies the actual selected endpoint.</summary>
    public static bool Matches(RouteCatalogAuxiliaryOperation operation, string path, string verb)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);
        if (!CanonicalPath(path) || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment is "." or "..")
            || operation.Http is not { } http || !http.Verbs.Split(' ').Contains(verb, StringComparer.Ordinal)) return false;
        return (http.Grants is null || http.Grants.Any(grant => grant.Verbs.Split(' ').Contains(verb, StringComparer.Ordinal) && GrantAllows(grant.Method, Uri.UnescapeDataString(path))))
            && MatchesShape(operation.Method, path);
    }

    /// <summary>Matches an actual MVC shape with its framework inline constraints.</summary>
    public static bool MatchesShape(string pattern, string path)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(path);

        if (!CanonicalPath(path)) return false;
        try
        {
            var template = TemplateParser.Parse(pattern);
            if (template.Parameters.Any(parameter => parameter.IsCatchAll || parameter.IsOptional || parameter.DefaultValue is not null)) return false;
            var values = new RouteValueDictionary();
            if (!new TemplateMatcher(template, new RouteValueDictionary()).TryMatch(new PathString("/" + Uri.UnescapeDataString(path).Trim('/')), values)) return false;
            var resolver = RoutingServices.GetRequiredService<IInlineConstraintResolver>();
            return template.Parameters.All(parameter => parameter.InlineConstraints.All(inline =>
                resolver.ResolveConstraint(inline.Constraint)?.Match(null, null, parameter.Name!, values, RouteDirection.IncomingRequest) == true));
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Applies the independently observed Dapr literal/prefix grant to one concrete canonical path.</summary>
    public static bool GrantAllows(string grant, string path)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(path);

        return CanonicalPath(path) && RouteCatalogDaprGrant.Allows(grant, Uri.UnescapeDataString(path));
    }

    /// <summary>Binds a compiled route to an intersecting or exactly described retained ACL declaration; concrete requests still require GrantAllows.</summary>
    public static bool GrantReaches(string grant, string pattern)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(pattern);

        return RouteCatalogDaprGrant.Intersects(grant, pattern) || RouteCatalogDaprGrant.Describes(grant, pattern);
    }

    /// <summary>Accepts escaped scalar data but refuses encoded path structure, double escapes and ambiguous segments before decoding.</summary>
    public static bool CanonicalPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Contains('\\', StringComparison.Ordinal)) return false;
        foreach (string segment in path.Split('/'))
        {
            string value = Uri.UnescapeDataString(segment);
            if (value is "." or ".." || value.IndexOfAny(['/', '\\', '%', '\0']) >= 0
                || value.Any(char.IsControl)) return false;
            for (int index = 0; index < segment.Length; index++)
                if (segment[index] == '%' && (index + 2 >= segment.Length || !Uri.IsHexDigit(segment[index + 1]) || !Uri.IsHexDigit(segment[index + 2]))) return false;
        }
        return true;
    }
}
