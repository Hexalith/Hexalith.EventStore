namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Represents the status of a consistency check.
/// </summary>
public enum ConsistencyCheckStatus
{
    /// <summary>The consistency check is queued and waiting to start.</summary>
    Pending,

    /// <summary>The consistency check is currently running.</summary>
    Running,

    /// <summary>The consistency check completed successfully.</summary>
    Completed,

    /// <summary>The consistency check failed due to an error.</summary>
    Failed,

    /// <summary>The consistency check was cancelled by the user.</summary>
    Cancelled,
}
