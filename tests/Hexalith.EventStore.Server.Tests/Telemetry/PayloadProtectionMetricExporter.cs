using System.Collections.Concurrent;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Captures metric points before the SDK reuses the exported metric batch.</summary>
/// <param name="meterName">The engine meter whose exported points are captured.</param>
internal sealed class PayloadProtectionMetricExporter(string meterName) : BaseExporter<Metric>
{
    /// <summary>Gets the instrument and tags of each exported point.</summary>
    internal ConcurrentQueue<(string Instrument, IReadOnlyDictionary<string, object?> Tags)> Records { get; } = new();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (Metric metric in batch)
        {
            if (metric.MeterName != meterName)
            {
                continue;
            }

            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                Dictionary<string, object?> tags = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object?> tag in point.Tags)
                {
                    tags.Add(tag.Key, tag.Value);
                }

                Records.Enqueue((metric.Name, tags));
            }
        }

        return ExportResult.Success;
    }
}
