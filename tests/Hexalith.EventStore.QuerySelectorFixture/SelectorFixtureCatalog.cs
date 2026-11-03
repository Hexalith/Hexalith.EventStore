using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.QuerySelectorFixture.SelectorFixtureCatalog))]

namespace Hexalith.EventStore.QuerySelectorFixture;

/// <summary>Attributed owner isolated from every ordinary test-assembly declaration inventory.</summary>
public static class SelectorFixtureCatalog
{
    /// <summary>Mutable original owner collection used to prove that the SDK copies its inventory.</summary>
    public static List<string> SelectorNames { get; } = ["LegacyWidgetQueryActor", "OlderWidgetQueryActor"];
    /// <summary>Genuine compiled owner declaration of the installed HTTP query handler.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => [new("widget-query", "query", "widget", "get-widget", "widgets", "v1", null,
        "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(SelectorFixtureQueryHandler).FullName!, "exact", ["get-widget"], "domain-service", "projection", [])
        { CompatibilityProjectionActorTypes = SelectorNames })];
    /// <summary>Typed schema-three operation inventory belonging to the same compiled owner.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route =>
        RouteOperationSchema.Create(route, typeof(SelectorFixtureQuery), typeof(SelectorFixtureQuery), "public", "agent-eligible", System.Text.Json.JsonSerializerOptions.Web)).ToArray();
}
