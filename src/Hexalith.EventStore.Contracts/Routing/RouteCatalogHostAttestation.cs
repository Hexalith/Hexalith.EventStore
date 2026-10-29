namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>One host's readiness receipt bound to every lifecycle input and both canonical facets.</summary>
/// <param name="AttemptId">Exact activation attempt.</param>
/// <param name="BindingDigest">Complete lifecycle binding digest.</param>
/// <param name="Generation">Envelope generation.</param>
/// <param name="RootDigest">Envelope root.</param>
/// <param name="RouteFacetDigest">Routing facet.</param>
/// <param name="IdempotencyFacetDigest">Admission facet.</param>
/// <param name="Profile">Explicit profile.</param>
/// <param name="Host">Independently observed local host identity.</param>
public sealed record RouteCatalogHostAttestation(string AttemptId, string BindingDigest, long Generation,
    string RootDigest, string RouteFacetDigest, string IdempotencyFacetDigest, string Profile, RouteCatalogHostIdentity Host);
