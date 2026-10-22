namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The persisted reminder state of one tenant item, keyed by its <c>wra-</c> actor identifier. Every write is
/// an ETag compare-and-swap that also advances <see cref="Version"/>.
/// </summary>
/// <param name="Tenant">The canonical tenant.</param>
/// <param name="Domain">The target stream domain.</param>
/// <param name="Aggregate">The target stream aggregate, which is the reminder item.</param>
/// <param name="Version">The monotonically increasing write version.</param>
/// <param name="Entries">The persisted reminder witnesses.</param>
/// <param name="Quarantine">The retained quarantine evidence.</param>
internal sealed record ReminderItemState(
    string Tenant,
    string Domain,
    string Aggregate,
    long Version,
    IReadOnlyList<ReminderEntry> Entries,
    IReadOnlyList<ReminderQuarantineRecord> Quarantine);
