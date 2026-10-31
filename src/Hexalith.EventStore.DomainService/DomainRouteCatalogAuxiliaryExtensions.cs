using Microsoft.AspNetCore.Builder;

namespace Hexalith.EventStore.DomainService;

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
