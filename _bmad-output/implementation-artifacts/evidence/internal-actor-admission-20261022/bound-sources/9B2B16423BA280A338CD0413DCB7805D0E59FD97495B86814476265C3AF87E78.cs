namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Describes tenant-scoped correlation compatibility resolution.
/// </summary>
public enum CommandCorrelationResolutionOutcome
{
    /// <summary>No live message mapping exists.</summary>
    NotFound,

    /// <summary>Exactly one live message mapping exists.</summary>
    Resolved,

    /// <summary>Multiple or unrepresentable live mappings exist.</summary>
    Ambiguous,
}
