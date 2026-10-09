namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Closed exact joint-commit observation; unknown outcomes perform no new effect.</summary>
public enum GuardedStateCommitStatus
{
    /// <summary>Original exact joint transaction is durably confirmed.</summary>
    Committed,
    /// <summary>Current expected guard/source comparison is stale before any transaction effect.</summary>
    Stale,
    /// <summary>An original operation id is bound to a different immutable request.</summary>
    Conflict,
    /// <summary>A commit may have happened but exact authenticated lookup cannot confirm it.</summary>
    Unknown,
    /// <summary>Required installation, authority, source basis or backend qualification is unavailable.</summary>
    Unavailable,
}
