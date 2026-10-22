namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Classifies whether an idempotency record is terminal or represents committed recoverable work.
/// </summary>
public enum IdempotencyRecordDisposition
{
    /// <summary>The original result is terminal and may be returned for an exact duplicate.</summary>
    Terminal = 1,

    /// <summary>Events were committed and recovery must continue without domain re-execution.</summary>
    Recoverable = 2,
}
