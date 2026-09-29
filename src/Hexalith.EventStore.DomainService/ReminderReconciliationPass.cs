namespace Hexalith.EventStore.DomainService;

/// <summary>Counts produced by one reconciliation pass.</summary>
/// <param name="Tenants">Tenants scanned.</param>
/// <param name="Candidates">Candidates converged or attempted.</param>
/// <param name="Armed">Future reminders armed.</param>
/// <param name="Submitted">Due reminders that reached a durable receipt.</param>
/// <param name="Cancelled">Obsolete reminders cancelled.</param>
/// <param name="Unresolved">Retained reminders without a durable outcome.</param>
/// <param name="Quarantined">Retained quarantine evidence.</param>
/// <param name="Incomplete">Scans or candidates that could not be processed.</param>
internal sealed record ReminderReconciliationPass(
    int Tenants,
    int Candidates,
    int Armed,
    int Submitted,
    int Cancelled,
    int Unresolved,
    int Quarantined,
    int Incomplete);
