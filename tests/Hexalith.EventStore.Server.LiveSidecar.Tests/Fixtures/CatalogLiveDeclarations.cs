using Hexalith.EventStore.Contracts.Routing;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures.CatalogLiveDeclarations))]

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Complete owner declarations of the catalog fixture's additional computed handler.</summary>
public static class CatalogLiveDeclarations
{
    public static IReadOnlyList<RouteContractDeclaration> Routes => [CatalogLiveQueryHandler.CatalogDeclaration];
}
