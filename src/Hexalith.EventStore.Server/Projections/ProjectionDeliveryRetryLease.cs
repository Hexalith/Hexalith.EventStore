namespace Hexalith.EventStore.Server.Projections;

/// <summary>Aggregate-scoped distributed lease preventing cross-head concurrent projection writes.</summary>
/// <param name="Owner">The opaque lease owner.</param>
/// <param name="ExpiresUtc">The exclusive lease expiry.</param>
public sealed record ProjectionDeliveryRetryLease(string Owner, DateTimeOffset ExpiresUtc);
