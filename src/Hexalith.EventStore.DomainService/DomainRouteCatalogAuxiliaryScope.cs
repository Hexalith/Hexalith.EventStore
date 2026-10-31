namespace Hexalith.EventStore.DomainService;

/// <summary>Exact request scope for an existing domain-owned auxiliary operation.</summary>
/// <param name="Tenant">The requested canonical tenant.</param>
/// <param name="Domain">The requested canonical domain.</param>
public sealed record DomainRouteCatalogAuxiliaryScope(string Tenant, string Domain);
