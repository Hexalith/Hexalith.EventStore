namespace Hexalith.EventStore.Server.Events;

/// <summary>Separates proven actor logical commit, proven no-commit and unavailable/contradictory readback.</summary>
internal enum DaprReplayCommitOutcome
{
    /// <summary>Every expected atomic participant was freshly read back exactly.</summary>
    Proven,
    /// <summary>The exact predecessor and absence of every new participant were freshly observed.</summary>
    NoCommit,
    /// <summary>Readback was unavailable, partial or contradictory; no successor/effect may be admitted.</summary>
    Indeterminate,
}
