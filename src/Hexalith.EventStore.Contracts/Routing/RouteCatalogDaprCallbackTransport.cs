namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed SDK subscription discovery or signed projection notification callback, never message ingress eligibility.</summary>
/// <param name="Kind">The closed native SDK callback kind.</param>
/// <param name="Verbs">Its actual HTTP verb.</param>
/// <param name="CodeDigest">The owning loaded SDK code digest.</param>
/// <param name="FrameworkDigest">The installed mapper and MVC framework digest.</param>
/// <param name="InventoryDigest">The actual configured component, mode and topic inventory digest.</param>
public sealed record RouteCatalogDaprCallbackTransport(string Kind, string Verbs, string CodeDigest, string FrameworkDigest, string InventoryDigest);
