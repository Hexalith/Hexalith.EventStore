namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Requests read-only proof from one exact catalog route without invoking its handler.</summary>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="Domain">The exact domain.</param>
/// <param name="AggregateId">The exact source aggregate.</param>
/// <param name="ProjectionName">The exact named route.</param>
/// <param name="CatalogFingerprint">The verified catalog generation.</param>
public sealed record ProjectionOwnerProofRequest(string TenantId, string Domain, string AggregateId,
    string ProjectionName, string CatalogFingerprint);
