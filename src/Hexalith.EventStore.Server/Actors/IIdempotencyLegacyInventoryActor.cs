using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Owns versioned protected legacy inventory and durable migration redirects per tenant.</summary>
public interface IIdempotencyLegacyInventoryActor : IActor
{
    /// <summary>Registers validated protected source evidence before migration is enabled.</summary>
    Task InventoryAsync(IdempotencyLegacyInventoryEntry entry);

    /// <summary>Inspects every retained digest-key alias before fresh authority is created.</summary>
    Task<IdempotencyLegacyInventoryInspection> InspectAsync(
        IdempotencyAdmissionDirectoryAlias[] aliases);

    /// <summary>Advances an exact entry after the corresponding target phase completed durably.</summary>
    Task<IdempotencyLegacyInventoryEntry> AdvanceAsync(
        string digestKeyVersion,
        string keyDigest,
        IdempotencyLegacyMigrationPhase expectedPhase,
        string targetAdmissionActorId);
}
