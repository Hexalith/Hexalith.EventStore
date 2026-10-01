namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Host-local view of reminder work that feeds readiness. Convergence and callbacks record per-item totals;
/// each completed reconciliation pass records its scan completeness and prunes older items it no longer discovers.
/// Records updated after the pass began remain until a later pass can discover them.
/// Durable truth stays in the persisted item state; this view is rebuilt by the first pass after a restart.
/// </summary>
internal sealed class ReminderRuntimeStatus
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (int Unresolved, int Quarantined, long Version)> _items = new(StringComparer.Ordinal);
    private long _version;
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
            long version = ++_version;
            if (unresolved <= 0 && quarantined <= 0)
            {
                _ = _items.Remove(actorId);
            }
            else
            {
                _items[actorId] = (Math.Max(unresolved, 0), Math.Max(quarantined, 0), version);
            }
        }
    }

    /// <summary>Captures the record boundary before a reconciliation pass reads discovery.</summary>
    /// <returns>The last recorded version, used to preserve updates made during the pass.</returns>
    public long BeginPass()
    {
        lock (_gate)
        {
            return _version;
        }
    }

    /// <summary>Records a completed reconciliation pass.</summary>
    /// <param name="completedAt">When the pass completed.</param>
    /// <param name="incompleteScans">Scans or candidates the pass could not process.</param>
    /// <param name="observedActorIds">Actor identifiers the pass discovered; older records for others are pruned after a complete pass.</param>
    /// <param name="passVersion">The record boundary returned by <see cref="BeginPass"/> before discovery started.</param>
    public void CompletePass(DateTimeOffset completedAt, int incompleteScans, IReadOnlyCollection<string> observedActorIds, long passVersion)
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
                foreach (string actorId in _items.Where(item => item.Value.Version <= passVersion && !observed.Contains(item.Key))
                    .Select(static item => item.Key).ToList())
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
