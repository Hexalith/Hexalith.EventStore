using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns versioned protected legacy inventory and durable migration redirects per tenant.</summary>
public interface IIdempotencyLegacyInventoryActor : IActor
{
    /// <summary>Registers validated protected source evidence before migration is enabled.</summary>
    Task InventoryAsync(IdempotencyLegacyInventoryEntry entry);

    /// <summary>Closes the exact immutable tenant inventory before absence may mean no legacy evidence.</summary>
    Task CloseAsync(IdempotencyLegacyInventoryClosure closure);

    /// <summary>Inspects every retained digest-key alias before fresh authority is created.</summary>
    Task<IdempotencyLegacyInventoryInspection> InspectAsync(
        IdempotencyAdmissionDirectoryAlias[] aliases);

    /// <summary>Advances an exact entry after the corresponding target phase completed durably.</summary>
    Task<IdempotencyLegacyInventoryEntry> AdvanceAsync(IdempotencyLegacyMigrationAdvanceRequest request);

    /// <summary>Rolls back only an exact unactivated target before the source redirect boundary.</summary>
    Task<IdempotencyLegacyInventoryEntry> RollbackAsync(IdempotencyLegacyMigrationRollbackRequest request);

    /// <summary>Removes an inventory reference only from the governed tenant purge turn.</summary>
    Task PurgeAsync(IdempotencyAdmissionDirectoryAlias alias);
}
