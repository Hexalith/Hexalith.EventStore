using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes managed-tenant deletion, legal hold, and final purge governance.</summary>
public interface IIdempotencyTenantLifecycleActor : IActor
{
    /// <summary>Registers protected admission and directory references before admission state creation.</summary>
    Task RegisterAsync(IdempotencyTenantLifecycleReference[] references);

    /// <summary>Starts the fixed 400-day countdown from approved deletion-workflow entry.</summary>
    Task<IdempotencyTenantLifecycleRecord> EnterDeletionAsync(DateTimeOffset approvedAt);

    /// <summary>Pauses the countdown and persists the remaining interval.</summary>
    Task<IdempotencyTenantLifecycleRecord> PlaceLegalHoldAsync(DateTimeOffset observedAt);

    /// <summary>Resumes the countdown from the persisted remaining interval.</summary>
    Task<IdempotencyTenantLifecycleRecord> ReleaseLegalHoldAsync(DateTimeOffset observedAt);

    /// <summary>Returns current lifecycle state, advancing retention to purge-eligible at the inclusive boundary.</summary>
    Task<IdempotencyTenantLifecycleRecord> GetAsync();

    /// <summary>Acknowledges one successfully purged protected reference.</summary>
    Task<IdempotencyTenantLifecycleRecord> AcknowledgePurgeAsync(string actorId);
}
