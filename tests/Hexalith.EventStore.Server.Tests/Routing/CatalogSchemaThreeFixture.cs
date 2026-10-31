using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Upgrades the existing owner fixture without changing declarations, policies or target identities.</summary>
internal static class CatalogSchemaThreeFixture
{
    internal static RouteCatalogEnvelope Create(RouteCatalogEnvelope original, Func<RouteContractDeclaration, string>? eligibility = null)
    {
        var operations = original.Declarations.Select(d => new RouteOperationContract(d.RouteId, d.ContractVersion,
            d.Kind == "projection" || d.Execution!.RequiresDelegatedContext ? "internal" : "public", eligibility?.Invoke(d) ?? "agent-eligible",
            JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
            d.Kind, d.Domain, d.MessageType ?? d.ProjectionType))
            .Select(operation => operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) }).ToArray();
        return VersionedRouteCatalogBuilder.Build(new(original.Generation, "Development", original.Declarations, operations,
            original.Routes.Select(route => new RouteCompositionBinding(route.RouteId, route.AppId, route.Method, route.ContractVersion,
                route.IsRuntimeOverride, route.ResolverTenantPattern)).ToArray(), original.Idempotency));
    }
}
