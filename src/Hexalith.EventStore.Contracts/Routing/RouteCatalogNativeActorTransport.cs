namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed existing SDK native actor callback inventory, independent of message ingress eligibility.</summary>
public sealed record RouteCatalogNativeActorTransport(string Kind, string Verbs, string CodeDigest, string FrameworkDigest, string InventoryDigest);
