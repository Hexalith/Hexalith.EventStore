namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Persisted lifecycle state for a single (tenant, domain, aggregate, projection) scope.
/// </summary>
/// <param name="Phase">The current lifecycle phase.</param>
/// <param name="OperationId">The in-flight operation identifier, or null when idle.</param>
/// <param name="ManifestDigest">The erase target manifest digest, or null when idle.</param>
/// <param name="PerTargetOutcomes">Per-target erase outcomes recorded during the operation.</param>
/// <param name="Revision">The durable lifecycle revision.</param>
/// <param name="DeliveryLeaseExpiresAtUtc">The delivery lease expiry, when delivering.</param>
/// <param name="PromotionFenced">Whether rebuild completion is fenced during promotion.</param>
internal sealed record ProjectionLifecycleActorState(
    ProjectionLifecyclePhase Phase,
    string? OperationId,
    string? ManifestDigest,
    Dictionary<string, string> PerTargetOutcomes,
    long Revision = 0,
    DateTimeOffset? DeliveryLeaseExpiresAtUtc = null,
    bool PromotionFenced = false);
