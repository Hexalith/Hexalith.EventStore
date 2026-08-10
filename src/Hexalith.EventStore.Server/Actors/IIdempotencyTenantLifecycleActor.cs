using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes managed-tenant deletion, legal hold, and final purge governance.</summary>
public interface IIdempotencyTenantLifecycleActor : IActor
{
    /// <summary>Registers protected admission and directory references before admission state creation.</summary>
    Task RegisterAsync(IdempotencyTenantLifecycleReference[] references);

    /// <summary>Revalidates active lifecycle authority and admits one exact registered reference in the same turn.</summary>
    Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyTenantLifecycleAdmissionRequest request);

    /// <summary>Starts the fixed 400-day countdown from approved deletion-workflow entry.</summary>
    Task<IdempotencyTenantLifecycleRecord> EnterDeletionAsync(DateTimeOffset approvedAt);

    /// <summary>Pauses the countdown and persists the remaining interval.</summary>
    Task<IdempotencyTenantLifecycleRecord> PlaceLegalHoldAsync(DateTimeOffset observedAt);

    /// <summary>Resumes the countdown from the persisted remaining interval.</summary>
    Task<IdempotencyTenantLifecycleRecord> ReleaseLegalHoldAsync(DateTimeOffset observedAt);

    /// <summary>Returns current lifecycle state, advancing retention to purge-eligible at the inclusive boundary.</summary>
    Task<IdempotencyTenantLifecycleRecord> GetAsync();

    /// <summary>Purges a bounded reference batch inside the lifecycle actor's serialized turn.</summary>
    Task<IdempotencyTenantLifecycleRecord> PurgeAsync(int maximumCount);

    /// <summary>Rejects legacy direct acknowledgement so deletion evidence cannot be bypassed.</summary>
    Task<IdempotencyTenantLifecycleRecord> AcknowledgePurgeAsync(string actorId);
}
