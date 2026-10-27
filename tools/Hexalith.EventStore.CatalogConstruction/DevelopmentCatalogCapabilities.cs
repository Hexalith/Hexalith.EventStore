using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.CatalogConstruction;

/// <summary>Compiled owner handler and custom-dispatcher enrollment required before profile construction.</summary>
/// <param name="HandlerQueries">Complete enrolled SDK query routes, verified against actual handlers in their owning repositories.</param>
/// <param name="Projections">Actual callable legacy projection declarations and their outputs.</param>
public sealed record DevelopmentCatalogCapabilities(
    IReadOnlyList<RouteContractDeclaration> HandlerQueries,
    IReadOnlyList<RouteContractDeclaration> Projections);
