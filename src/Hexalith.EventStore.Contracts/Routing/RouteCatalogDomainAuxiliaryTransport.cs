namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Code-owned typed scope extraction for one finite domain auxiliary endpoint, never a message route.</summary>
public sealed record RouteCatalogDomainAuxiliaryTransport(string Owner, string RequestType, string ScopeMethod, string CodeDigest);
