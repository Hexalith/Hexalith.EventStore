using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

using OpenTelemetry;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Snapshots every actual exported span before its Activity is disposed.</summary>
internal sealed class ReadPrivacyTraceExporter : BaseExporter<Activity>
{
    /// <summary>Gets all exported spans, with no test-side request or source filtering.</summary>
    internal ConcurrentQueue<string> Records { get; } = new();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (Activity activity in batch)
        {
            Records.Enqueue(JsonSerializer.Serialize(new
            {
                activity.OperationName, activity.Kind, activity.Status,
                Source = activity.Source.Name, activity.Tags, activity.Events,
                Links = activity.Links.Select(static link => new { Trace = link.Context.TraceId.ToString(), Span = link.Context.SpanId.ToString(), link.Tags }),
            }));
        }
        return ExportResult.Success;
    }
}
