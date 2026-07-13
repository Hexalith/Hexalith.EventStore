namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Versioned request for dispatching one aggregate projection request to an explicit set of named projections.
/// </summary>
/// <param name="Request">The aggregate projection request.</param>
/// <param name="ProjectionTypes">The non-empty server-admitted projection types.</param>
/// <param name="DispatchId">The stable dispatch identity reused by durable retries.</param>
/// <param name="CatalogFingerprint">The exact admitted route-catalog fingerprint.</param>
public sealed record ProjectionDispatchRequest(
    ProjectionRequest Request,
    IReadOnlyList<string> ProjectionTypes,
    string DispatchId,
    string CatalogFingerprint);
