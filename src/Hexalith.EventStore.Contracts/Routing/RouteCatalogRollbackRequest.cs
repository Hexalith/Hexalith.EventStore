namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exact lifecycle transition; a pending abort can never become a committed rollback.</summary>
/// <param name="AttemptId">The immutable activation attempt.</param>
/// <param name="BindingDigest">The full owner-bound activation digest.</param>
/// <param name="ExpectedEpoch">The expected monotonic authority epoch.</param>
/// <param name="Phase">The exact expected phase: pending or committed.</param>
public sealed record RouteCatalogRollbackRequest(string AttemptId, string BindingDigest, long ExpectedEpoch, string Phase);
