namespace Hexalith.EventStore.Server.Actors;

/// <summary>Describes crash-resumable protected legacy migration progress.</summary>
public enum IdempotencyLegacyMigrationPhase
{
    /// <summary>Protected source evidence and exact logical result were inventoried.</summary>
    Inventoried,

    /// <summary>The non-executable target acknowledged the imported state.</summary>
    TargetPrepared,

    /// <summary>The inventory is a durable redirect to the activated target authority.</summary>
    Migrated,

    /// <summary>The source is ambiguous, corrupt, or cannot preserve an exact logical result.</summary>
    Unsafe,
}
