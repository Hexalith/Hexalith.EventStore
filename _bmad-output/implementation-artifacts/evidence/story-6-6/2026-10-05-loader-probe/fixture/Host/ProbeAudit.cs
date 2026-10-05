namespace Probe.Host;

/// <summary>Records fixture gate state and observation order without claiming to intercept nested calls.</summary>
internal sealed class ProbeAudit
{
    private readonly List<object> _events = [];

    /// <summary>Gets whether the fixture would admit its next host-controlled invocation.</summary>
    internal bool Ready { get; private set; } = true;

    /// <summary>Gets the ordered host/plugin observations retained by the runner.</summary>
    internal IReadOnlyList<object> Events => _events;

    /// <summary>Records one observation with the capability state at that observation.</summary>
    internal void Record(string kind, string detail = "")
        => _events.Add(new { sequence = _events.Count + 1, kind, detail, ready = Ready });

    /// <summary>Invalidates future host-controlled calls and records why; it cannot stop a running callback.</summary>
    internal void Invalidate(string detail)
    {
        Ready = false;
        Record("capability-invalidated", detail);
    }
}
