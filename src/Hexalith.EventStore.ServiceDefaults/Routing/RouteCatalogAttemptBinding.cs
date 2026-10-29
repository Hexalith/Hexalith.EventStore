namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Non-reusable activation identity retained after every abort, commit and rollback.</summary>
/// <param name="AttemptId">The unique activation identity.</param>
/// <param name="BindingDigest">Its immutable full lifecycle binding.</param>
public sealed record RouteCatalogAttemptBinding(string AttemptId, string BindingDigest);
