namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exact pending attempt for ready/commit, or exact candidate root for rollback.</summary>
/// <param name="Identity">The immutable expected attempt or root.</param>
public sealed record RouteCatalogLifecycleRequest(string Identity);
