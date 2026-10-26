using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Metadata-only enrollment of a custom legacy dispatcher; it grants no named-dispatch capability.</summary>
/// <param name="Declarations">The owner's exact callable legacy routes and their non-callable outputs.</param>
public sealed record LegacyProjectionCatalogRegistration(IReadOnlyList<RouteContractDeclaration> Declarations)
{
    /// <summary>Attaches explicit legacy metadata without changing dispatch or existing fingerprint protocols.</summary>
    public static AdminOperationalIndexMetadata.Response Bind(AdminOperationalIndexMetadata.Response response,
        IEnumerable<LegacyProjectionCatalogRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(registrations);
        RouteContractDeclaration[] routes = registrations.SelectMany(registration => registration.Declarations).ToArray();
        foreach (RouteContractDeclaration route in routes)
        {
            RouteCatalogValidator.ValidateDeclaration(route);
            if (route.Kind != "projection" || route.Execution?.TargetKind != "domain-service")
                throw new InvalidOperationException("Only explicit legacy projection routes can enroll metadata.");
        }
        if (routes.Select(route => (route.Domain, route.ProjectionType)).Distinct().Count() != routes.Length)
            throw new InvalidOperationException("Ambiguous legacy projection metadata.");
        return response with
        {
            Domains = response.Domains.Select(domain =>
            {
                RouteContractDeclaration[] declared = routes.Where(route => route.Domain == domain.Domain).ToArray();
                return declared.Length == 0 ? domain : domain with
                {
                    ProjectionNames = domain.ProjectionNames.Concat(declared.Select(route => route.ProjectionType!)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    LegacyProjectionOutputs = declared.ToDictionary(route => route.ProjectionType!, route => route.Execution!.ProjectionOutputs, StringComparer.Ordinal),
                };
            }).ToArray(),
        };
    }
}
