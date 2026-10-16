using System.Diagnostics.Metrics;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Observes owning protocol measurements across the in-process fixture's HTTP and actor execution contexts.</summary>
internal sealed class ProjectionDeliveryCostListener : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly long[] _values = new long[Enum.GetValues<ProjectionDeliveryCost>().Length];

    internal ProjectionDeliveryCostListener()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Hexalith.EventStore" && instrument.Name == ProjectionDeliveryCostDiagnostics.CounterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
                if (tag.Key == ProjectionDeliveryCostDiagnostics.CategoryTag
                    && tag.Value is string name && Enum.TryParse(name, out ProjectionDeliveryCost category))
                    Interlocked.Add(ref _values[(int)category], value);
        });
        _listener.Start();
    }

    internal long this[ProjectionDeliveryCost category] => Interlocked.Read(ref _values[(int)category]);

    public void Dispose() => _listener.Dispose();
}
