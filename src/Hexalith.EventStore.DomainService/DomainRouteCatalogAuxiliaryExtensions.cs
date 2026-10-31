using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Exact request scope for an existing domain-owned auxiliary operation.</summary>
public sealed record DomainRouteCatalogAuxiliaryScope(string Tenant, string Domain);

/// <summary>Enrolls a finite owner endpoint under an existing SDK workload policy and the same catalog fence.</summary>
public static class DomainRouteCatalogAuxiliaryExtensions
{
    /// <summary>Registers an exact POST receiver and its code-owned typed tenant/domain extraction.</summary>
    /// <remarks>The extractor must be an ordinary static method in the request owner's assembly. It cannot capture mutable closure policy.
    /// Returning null classifies a malformed request as 400 after the endpoint's code and generation have been admitted.</remarks>
    public static RouteHandlerBuilder RequireEventStoreDomainServiceAuxiliaryPolicy<TRequest>(this RouteHandlerBuilder builder,
        string canonicalRoute, string domain, Func<TRequest, DomainRouteCatalogAuxiliaryScope?> scope)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(scope);
        if (!EventStoreDomainServiceRoutes.TryGet(canonicalRoute, out var route)
            || !scope.Method.IsStatic || scope.Target is not null || scope.Method.DeclaringType?.Assembly != typeof(TRequest).Assembly
            || typeof(TRequest).FullName is null || domain.Contains('*', StringComparison.Ordinal))
            throw new ArgumentException("A canonical policy and static request-owner scope extractor are required.", nameof(scope));
        builder.RequireEventStoreDomainServicePolicy(canonicalRoute);
        builder.Finally(endpoint => endpoint.Metadata.Add(new DomainRouteCatalogAuxiliaryMetadata(typeof(TRequest), route!, domain, scope.Method,
            request => scope((TRequest)request), endpoint.RequestDelegate)));
        return builder;
    }
}

internal sealed class DomainRouteCatalogAuxiliaryMetadata(Type requestType, EventStoreDomainServiceRoute route,
    string domain, MethodInfo scopeMethod, Func<object, DomainRouteCatalogAuxiliaryScope?> scope, RequestDelegate? actualRequest)
{
    internal Type RequestType { get; } = requestType;
    internal EventStoreDomainServiceRoute Route { get; } = route;
    internal string Domain { get; } = domain;

    internal DomainRouteCatalogAuxiliaryScope Scope(object request)
    {
        if (request.GetType() != RequestType) throw new RouteCatalogUnavailableException("The auxiliary request type differs from its owner binding.");
        var selected = scope(request);
        if (selected is null) throw new DomainRouteCatalogBadAuxiliaryRequestException();
        if (string.IsNullOrWhiteSpace(selected.Tenant) || selected.Tenant == "*" || selected.Domain != Domain)
            throw new RouteCatalogUnavailableException("The auxiliary request has no exact owner tenant/domain scope.");
        return selected;
    }

    internal RouteCatalogDomainAuxiliaryTransport Observe(RouteEndpoint endpoint)
    {
        string? path = endpoint.RoutePattern.RawText;
        if (actualRequest is null || !ReferenceEquals(actualRequest, endpoint.RequestDelegate)
            || endpoint.Metadata.GetOrderedMetadata<DomainRouteCatalogAuxiliaryMetadata>().Count != 1
            || string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || endpoint.RoutePattern.Parameters.Count > 0
            || path.IndexOfAny(['{', '}', '*', '?']) >= 0
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy == Route.Policy)
            || endpoint.Metadata.GetMetadata<DomainRouteCatalogGuardMetadata>() is null)
            throw new RouteCatalogUnavailableException("A domain auxiliary receiver must be one concrete guarded POST endpoint with its canonical workload policy.");
        string owner = scopeMethod.DeclaringType!.Assembly.FullName!;
        string assemblyPath = scopeMethod.DeclaringType.Assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            throw new RouteCatalogUnavailableException("The actual auxiliary owner code is unavailable.");
        return new(owner, RequestType.AssemblyQualifiedName!, scopeMethod.DeclaringType.FullName + ":" + scopeMethod.Name,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assemblyPath))));
    }

    internal static RouteCatalogDomainAuxiliaryTransport? ObserveEndpoint(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>()?.Observe(endpoint);
}

internal sealed class DomainRouteCatalogBadAuxiliaryRequestException : Exception;
