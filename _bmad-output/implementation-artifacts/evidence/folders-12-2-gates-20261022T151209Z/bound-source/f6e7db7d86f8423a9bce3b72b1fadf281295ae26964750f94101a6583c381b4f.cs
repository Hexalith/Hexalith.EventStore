namespace Hexalith.EventStore.DomainService;

/// <summary>Counts produced by one reconciliation pass.</summary>
/// <param name="Tenants">Tenants scanned.</param>
/// <param name="Candidates">Candidates converged or attempted.</param>
/// <param name="Armed">Future reminders armed.</param>
/// <param name="Submitted">Due reminders that reached a durable receipt.</param>
/// <param name="Cancelled">Obsolete reminders cancelled.</param>
/// <param name="Unresolved">Retained reminders without a durable outcome.</param>
/// <param name="Quarantined">Retained quarantine evidence.</param>
/// <param name="Incomplete">Unreadable scans or candidates plus capacity-limited tenants that degrade readiness.</param>
/// <param name="CapacityLimitedTenants">Full or over-capacity tenant indexes; these alone do not shorten the hosted cadence.</param>
internal sealed record ReminderReconciliationPass(
    int Tenants,
    int Candidates,
    int Armed,
    int Submitted,
    int Cancelled,
    int Unresolved,
    int Quarantined,
    int Incomplete,
    int CapacityLimitedTenants = 0);
