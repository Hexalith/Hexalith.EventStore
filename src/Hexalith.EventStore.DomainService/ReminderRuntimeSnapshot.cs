namespace Hexalith.EventStore.DomainService;

/// <summary>A point-in-time view of reminder work observed by this host.</summary>
/// <param name="PassCompleted">Whether at least one reconciliation pass completed.</param>
/// <param name="LastPassAt">When the last pass completed.</param>
/// <param name="IncompleteScans">Scans or candidates the last pass could not process.</param>
/// <param name="UnresolvedItems">Items holding unresolved or quarantined work.</param>
/// <param name="Unresolved">Retained reminders without a durable outcome.</param>
/// <param name="Quarantined">Retained quarantine evidence.</param>
internal sealed record ReminderRuntimeSnapshot(
    bool PassCompleted,
    DateTimeOffset? LastPassAt,
    int IncompleteScans,
    int UnresolvedItems,
    int Unresolved,
    int Quarantined);
