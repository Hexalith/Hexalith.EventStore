namespace Hexalith.EventStore.Controllers;

/// <summary>The exact admitted domain-service route catalog for an operator rebuild.</summary>
/// <param name="CatalogFingerprint">The registered catalog fingerprint for the tenant domain.</param>
public sealed record SharedSourceRebuildRequest(string CatalogFingerprint);
