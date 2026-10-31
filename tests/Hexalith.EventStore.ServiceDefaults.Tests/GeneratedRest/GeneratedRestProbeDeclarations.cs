using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest.GeneratedRestProbeDeclarations))]

namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Compiled owner declarations and schemas independently read by both host and authority fixture.</summary>
public static class GeneratedRestProbeDeclarations
{
    /// <summary>The complete test owner's callable query inventory.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } =
    [new("generated-probe-read", "query", GeneratedRestProbeQuery.Domain, GeneratedRestProbeQuery.QueryType,
        GeneratedRestProbeQuery.ProjectionType, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(GeneratedRestProbeQuery).FullName!, "exact", [GeneratedRestProbeQuery.QueryType], "domain-service", "projection", []))];

    /// <summary>Constructs the canonical fixture generation from the same complete compiled owner declarations.</summary>
    public static RouteCatalogEnvelope Catalog()
    {
        RouteContractDeclaration declaration = Routes.Single();
        return RouteCatalogCodec.Seal(new(3, 1, "Development", false, [declaration],
            [new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "domain", "query", "v1", declaration.CredentialKind,
                declaration.Operation, false, "*", Operations.Single().Digest)], [], string.Empty, string.Empty, Operations, []));
    }

    /// <summary>The real request/result schemas and intentionally UI-only policy.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations { get; } =
        [RouteOperationSchema.Create(Routes[0], typeof(GeneratedRestProbeQuery), typeof(string), "public", "UI-only", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))];
}
