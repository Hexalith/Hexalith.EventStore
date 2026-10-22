using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Exports actual protocol work without scope, state-key, payload or provider labels.</summary>
internal static class ProjectionDeliveryCostDiagnostics
{
    internal const string CounterName = "eventstore.projection.delivery.cost";
    internal const string CategoryTag = "projection.cost";
    private static readonly Meter _meter = new("Hexalith.EventStore");
    private static readonly Counter<long> _cost = _meter.CreateCounter<long>(CounterName);
    private static readonly Counter<long> _cleanupFailures = _meter.CreateCounter<long>("eventstore.projection.cleanup.failures");

    /// <summary>Reports bounded maintenance failure and durable retry disposition without source identifiers.</summary>
    internal static void RecordCleanupFailure(bool timedOut)
    {
        try
        {
            _cleanupFailures.Add(1, new KeyValuePair<string, object?>("reason", timedOut ? "budget-expired" : "failure"),
                new KeyValuePair<string, object?>("disposition", "foreground-retry"));
        }
        catch { /* Exporter failures never alter durable maintenance intent. */ }
    }

    private static readonly AsyncLocal<ProjectionDeliveryCostScope?> _current = new();

    /// <summary>Observes actual production calls for one execution, without changing protocol behavior.</summary>
    internal static ProjectionDeliveryCostScope BeginScope()
    {
        var scope = new ProjectionDeliveryCostScope(_current.Value);
        _current.Value = scope;
        return scope;
    }

    /// <summary>Charges each actually visited digest or receipt, including repeated validation and set construction.</summary>
    internal static IEnumerable<T> VisitProof<T>(IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            Record(ProjectionDeliveryCost.ProofVisits);
            yield return value;
        }
    }

    /// <summary>Restores the enclosing observer after a sequential execution completes.</summary>
    internal static void Restore(ProjectionDeliveryCostScope scope, ProjectionDeliveryCostScope? parent)
    {
        if (ReferenceEquals(_current.Value, scope)) _current.Value = parent;
    }

    /// <summary>Charges work when it occurs; observer or exporter failure can never alter persistence.</summary>
    internal static void Record(ProjectionDeliveryCost category, long amount = 1)
    {
        if (amount < 0) return;
        try
        {
            _current.Value?.Add(category, amount);
            _cost.Add(amount, new KeyValuePair<string, object?>(CategoryTag, category.ToString()));
        }
        catch { /* Diagnostics never authorize or interrupt a protocol transition. */ }
    }
}
