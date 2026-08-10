namespace Hexalith.EventStore.Server.Actors;

/// <summary>Describes crash-resumable protected legacy migration progress.</summary>
public enum IdempotencyLegacyMigrationPhase
{
    /// <summary>Protected source evidence and exact logical result were inventoried.</summary>
    Inventoried,

    /// <summary>The exact target contains a durable non-executable imported state.</summary>
    TargetPrepared,

    /// <summary>The target inspection acknowledged the hash-bound imported state.</summary>
    TargetAcknowledged,

    /// <summary>The exact legacy source contains the irreversible payload-free redirect.</summary>
    SourceRedirected,

    /// <summary>The tenant directory names the pinned target as canonical authority.</summary>
    AuthorityFlipped,

    /// <summary>The redirect, directory, and activated target were independently reproved.</summary>
    Migrated,

    /// <summary>The source is ambiguous, corrupt, or cannot preserve an exact logical result.</summary>
    Unsafe,
}
