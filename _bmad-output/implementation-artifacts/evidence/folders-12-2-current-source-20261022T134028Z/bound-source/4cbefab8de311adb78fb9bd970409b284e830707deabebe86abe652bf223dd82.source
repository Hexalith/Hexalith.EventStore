namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Durable private metadata protocol outcomes; none substitutes for target append authority.</summary>
public enum DirectoryBoundaryOutcomeState
{
    /// <summary>Missing qualified backend/current exact authority.</summary>
    Unavailable = 0,
    /// <summary>Exact installation retained.</summary>
    Installed = 1,
    /// <summary>Exact atomic repair checkpoint retained.</summary>
    RepairFenced = 2,
    /// <summary>Exact original terminal drain receipt retained.</summary>
    DrainRecorded = 3,
    /// <summary>Fully drained successor activation retained.</summary>
    Activated = 4,
    /// <summary>Conditional compare lost; exact outcome is retained and cannot later change to success.</summary>
    Stale = 5,
    /// <summary>Immutable identity conflicts.</summary>
    Conflict = 6,
}
