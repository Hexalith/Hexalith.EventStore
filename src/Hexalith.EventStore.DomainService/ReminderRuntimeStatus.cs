namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Host-local view of reminder work that feeds readiness. Convergence and callbacks record per-item totals;
/// each completed reconciliation pass records its scan completeness and prunes items it no longer discovers.
/// Durable truth stays in the persisted item state; this view is rebuilt by the first pass after a restart.
/// </summary>
internal sealed class ReminderRuntimeStatus
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (int Unresolved, int Quarantined)> _items = new(StringComparer.Ordinal);
    private bool _passCompleted;
    private DateTimeOffset? _lastPassAt;
    private int _incompleteScans;

    /// <summary>Records the retained totals of one item after convergence or a callback.</summary>
    /// <param name="actorId">The <c>wra-</c> actor identifier.</param>
    /// <param name="unresolved">Retained reminders without a durable outcome.</param>
    /// <param name="quarantined">Retained quarantine evidence.</param>
    public void RecordItem(string actorId, int unresolved, int quarantined)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        lock (_gate)
        {
            if (unresolved <= 0 && quarantined <= 0)
            {
                _ = _items.Remove(actorId);
            }
            else
            {
                _items[actorId] = (Math.Max(unresolved, 0), Math.Max(quarantined, 0));
            }
        }
    }

    /// <summary>Records a completed reconciliation pass.</summary>
    /// <param name="completedAt">When the pass completed.</param>
    /// <param name="incompleteScans">Scans or candidates the pass could not process.</param>
    /// <param name="observedActorIds">Actor identifiers the pass discovered; others are pruned after a complete pass.</param>
    public void CompletePass(DateTimeOffset completedAt, int incompleteScans, IReadOnlyCollection<string> observedActorIds)
    {
        ArgumentNullException.ThrowIfNull(observedActorIds);
        lock (_gate)
        {
            _passCompleted = true;
            _lastPassAt = completedAt;
            _incompleteScans = Math.Max(incompleteScans, 0);
            if (_incompleteScans == 0)
            {
                var observed = new HashSet<string>(observedActorIds, StringComparer.Ordinal);
                foreach (string actorId in _items.Keys.Where(actorId => !observed.Contains(actorId)).ToList())
                {
                    _ = _items.Remove(actorId);
                }
            }
        }
    }

    /// <summary>Returns the current view.</summary>
    /// <returns>A point-in-time snapshot.</returns>
    public ReminderRuntimeSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ReminderRuntimeSnapshot(
                _passCompleted,
                _lastPassAt,
                _incompleteScans,
                _items.Count,
                _items.Values.Sum(static item => item.Unresolved),
                _items.Values.Sum(static item => item.Quarantined));
        }
    }
}
