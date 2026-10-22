namespace Hexalith.EventStore.Server.Actors;

/// <summary>Classifies tenant-scoped protected legacy inventory before fresh admission.</summary>
public enum IdempotencyLegacyInventoryDecision
{
    /// <summary>A clean-install policy or completed inventory proves no matching legacy evidence.</summary>
    NoLegacy,

    /// <summary>An exact protected source entry must be migrated before admission.</summary>
    Migrate,

    /// <summary>The entry already redirects to activated admission authority.</summary>
    Migrated,

    /// <summary>The source evidence cannot be migrated safely.</summary>
    Unsafe,

    /// <summary>Upgrade policy requires inventory but no proof exists for this key.</summary>
    Uninventoried,
}
