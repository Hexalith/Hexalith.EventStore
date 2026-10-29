namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Separate lifecycle proposal; this does not alter the v1/v2 envelope codec or grant signing authority.</summary>
/// <param name="AttemptId">Unique immutable activation attempt.</param>
/// <param name="CanonicalCatalog">Retained canonical envelope bytes.</param>
/// <param name="RootDigest">Exact envelope root.</param>
/// <param name="PredecessorRootDigest">Exact committed predecessor, or null only for bootstrap.</param>
/// <param name="RequiredHosts">Complete, exact host set covered by the owner binding.</param>
/// <param name="Owner">Owner identity; Development fixtures carry no production authority.</param>
/// <param name="TrustProof">Opaque owner proof verified by an explicitly supplied trust authority.</param>
/// <param name="AuxiliaryOperations">Explicit inventory of existing auxiliary calls; absence grants none.</param>
/// <param name="ProjectionCapabilities">Exact existing named capability fingerprints bound to this generation.</param>
public sealed record RouteCatalogActivation(string AttemptId, byte[] CanonicalCatalog, string RootDigest,
    string? PredecessorRootDigest, IReadOnlyList<RouteCatalogHostIdentity> RequiredHosts, string Owner, string TrustProof,
    IReadOnlyList<RouteCatalogAuxiliaryOperation>? AuxiliaryOperations = null,
    IReadOnlyList<RouteCatalogProjectionCapability>? ProjectionCapabilities = null);
