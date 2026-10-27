namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed schema v1/v2 construction envelope. Possession of this envelope grants no activation or signing authority.</summary>
/// <param name="SchemaVersion">Exactly 1 for the original shape, or 2 for explicit handler and resolver-scope metadata.</param>
/// <param name="Generation">Positive complete catalog generation.</param>
/// <param name="Profile">Explicit composition profile.</param>
/// <param name="AllowRuntimeOverrides">May be true only for the exact Development profile.</param>
/// <param name="Declarations">Complete route declarations.</param>
/// <param name="Routes">One binding per declaration.</param>
/// <param name="Idempotency">One entry per command declaration.</param>
/// <param name="RouteFacetDigest">Digest of the routing facet, including declarations.</param>
/// <param name="IdempotencyFacetDigest">Digest of the idempotency facet.</param>
public sealed record RouteCatalogEnvelope(
    int SchemaVersion,
    long Generation,
    string Profile,
    bool AllowRuntimeOverrides,
    IReadOnlyList<RouteContractDeclaration> Declarations,
    IReadOnlyList<RouteCatalogEntry> Routes,
    IReadOnlyList<IdempotencyCatalogEntry> Idempotency,
    string RouteFacetDigest,
    string IdempotencyFacetDigest);
