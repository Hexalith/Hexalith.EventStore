namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Describes the result of an idempotency lookup without conflating unsafe records with misses.
/// </summary>
public enum IdempotencyCheckOutcome
{
    /// <summary>No current or bounded legacy record exists.</summary>
    Miss,

    /// <summary>An exact terminal record exists and its original result may be returned.</summary>
    ExactTerminalDuplicate,

    /// <summary>An exact record expired and its removal was staged.</summary>
    Expired,

    /// <summary>An exact recoverable record exists and domain execution must not restart.</summary>
    RetryableRecoverable,

    /// <summary>The stored identity differs or cannot be proved.</summary>
    IdentityConflict,

    /// <summary>An exact legacy-keyed record was staged for migration to its message key.</summary>
    LegacyMigration,
}
