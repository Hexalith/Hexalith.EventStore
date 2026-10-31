using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Captures one actual owner endpoint and validates its immutable request scope.</summary>
internal sealed class DomainRouteCatalogAuxiliaryMetadata(Type requestType, EventStoreDomainServiceRoute route,
    string domain, MethodInfo scopeMethod, Func<object, DomainRouteCatalogAuxiliaryScope?> scope, RequestDelegate? actualRequest)
{
    /// <summary>Gets the exact request-owner type.</summary>
    internal Type RequestType { get; } = requestType;
    /// <summary>Gets the existing canonical workload route policy.</summary>
    internal EventStoreDomainServiceRoute Route { get; } = route;
    /// <summary>Gets the declared owning domain.</summary>
    internal string Domain { get; } = domain;

    /// <summary>Extracts a canonical owner scope after code and generation admission.</summary>
    internal DomainRouteCatalogAuxiliaryScope Scope(object request)
    {
        if (request.GetType() != RequestType) throw new RouteCatalogUnavailableException("The auxiliary request type differs from its owner binding.");
        var selected = scope(request);
        if (selected is null || string.IsNullOrWhiteSpace(selected.Tenant) || string.IsNullOrWhiteSpace(selected.Domain))
            throw new DomainRouteCatalogBadAuxiliaryRequestException();
        try
        {
            var identity = new AggregateIdentity(selected.Tenant, selected.Domain, "auxiliary-scope");
            if (identity.TenantId != selected.Tenant || identity.Domain != selected.Domain)
                throw new DomainRouteCatalogBadAuxiliaryRequestException();
        }
        catch (ArgumentException) { throw new DomainRouteCatalogBadAuxiliaryRequestException(); }
        if (selected.Domain != Domain) throw new DomainRouteCatalogForbiddenAuxiliaryRequestException();
        return selected;
    }

    /// <summary>Observes the actual protected endpoint delegate and owner assembly.</summary>
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

    /// <summary>Returns an actual owner witness only when installed on this endpoint.</summary>
    internal static RouteCatalogDomainAuxiliaryTransport? ObserveEndpoint(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>()?.Observe(endpoint);
}
