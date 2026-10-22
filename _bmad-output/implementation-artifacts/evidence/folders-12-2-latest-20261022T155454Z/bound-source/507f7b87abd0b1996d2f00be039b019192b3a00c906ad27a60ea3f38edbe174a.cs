namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Represents the durable lifecycle of a captured subscriber dead letter.
/// </summary>
public enum DeadLetterReplayState
{
    /// <summary>The item is retained and awaiting an operator decision.</summary>
    Pending,

    /// <summary>An operator durably requested replay.</summary>
    ReplayRequested,

    /// <summary>A replay delivery may be in flight.</summary>
    Replaying,

    /// <summary>The target acknowledged replay delivery.</summary>
    Replayed,

    /// <summary>The item was archived and is no longer actionable.</summary>
    Archived,
}
