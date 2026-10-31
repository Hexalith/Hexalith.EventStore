namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>One exact actual owner ACL grant retained for a finite SDK technical transport.</summary>
public sealed record RouteCatalogHttpGrant(string Method, string Verbs);
