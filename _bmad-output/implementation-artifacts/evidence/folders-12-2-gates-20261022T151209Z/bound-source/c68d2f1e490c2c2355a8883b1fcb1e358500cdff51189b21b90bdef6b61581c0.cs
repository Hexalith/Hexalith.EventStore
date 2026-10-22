namespace Hexalith.EventStore.DomainService;

/// <summary>Proof that a tenant's first candidate page was linked before tenant publication.</summary>
public sealed record ProcessTenantMarker(int Version, string Tenant);
