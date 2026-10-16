namespace Hexalith.EventStore.Server.Projections;

/// <summary>Captures only already observed authority, including attempts that fail before a tail plan exists.</summary>
internal sealed class ProjectionSourceGuardAttemptTelemetry
{
    private readonly Dictionary<string, (string Mode, long? Head, long? Checkpoint, string? Status, string? Reason)> _routes = new(StringComparer.Ordinal);

    internal long? FinalizationStartedTimestamp { get; private set; }
    internal void StartFinalization(TimeProvider clock) => FinalizationStartedTimestamp ??= clock.GetTimestamp();

    internal void Add(string route) => _routes.TryAdd(route, ("reconciliation", null, null, null, null));
    internal void Head(string route, long head)
    { var old = _routes[route]; _routes[route] = (old.Mode, head >= 0 ? head : null, old.Checkpoint, old.Status, old.Reason); }
    internal void Checkpoint(string route, long checkpoint)
    { var old = _routes[route]; _routes[route] = (old.Mode, old.Head, checkpoint, old.Status, old.Reason); }
    internal void Mode(string route, string mode)
    { var old = _routes[route]; _routes[route] = (mode, old.Head, old.Checkpoint, old.Status, old.Reason); }
    internal void Terminal(string route, string mode, string status, string reason, long head, long checkpoint)
        => _routes[route] = (mode, head, checkpoint, status, reason);

    internal void Record(bool current, string? reason)
    {
        foreach (var pair in _routes)
            ProjectionDeliveryDiagnostics.RecordGuarded(pair.Key, pair.Value.Mode,
                pair.Value.Status ?? (current ? "already_completed" : "indeterminate"),
                pair.Value.Reason ?? reason ?? (current ? "delivery_already_completed" : "delivery_reconciliation_required"),
                pair.Value.Head, pair.Value.Checkpoint);
    }
}
