using System.Collections.Concurrent;
using System.Text;

using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Snapshots every emitted log field and scope at the real OpenTelemetry export boundary.</summary>
internal sealed class ReadPrivacyLogExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _boundaries = new(StringComparer.Ordinal);

    /// <summary>Gets all actual exported records without category or trace filtering.</summary>
    internal ConcurrentQueue<string> Records { get; } = new();

    /// <summary>Registers the completion record expected for one actual request.</summary>
    internal Task ExpectBoundary(string correlation)
        => _boundaries.GetOrAdd(correlation, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (LogRecord record in batch)
        {
            StringBuilder text = new();
            text.Append(record.CategoryName).Append('|').Append(record.LogLevel).Append('|').Append(record.EventId.Id)
                .Append('|').Append(record.FormattedMessage).Append('|').Append(record.Body).Append('|').Append(record.Exception);
            string? correlation = null;
            foreach (KeyValuePair<string, object?> field in record.Attributes ?? [])
            {
                text.Append('|').Append(field.Key).Append('=').Append(field.Value);
                if (field.Key == "CorrelationId") correlation = field.Value?.ToString();
            }
            record.ForEachScope(static (scope, snapshot) =>
            {
                foreach (KeyValuePair<string, object?> field in scope)
                    snapshot.Append('|').Append(field.Key).Append('=').Append(field.Value);
            }, text);
            text.Append("|TraceId=").Append(record.TraceId).Append("|SpanId=").Append(record.SpanId);
            Records.Enqueue(text.ToString());
            if (record.CategoryName == "Hexalith.EventStore.QueryBoundary" && correlation is not null
                && _boundaries.TryGetValue(correlation, out TaskCompletionSource? complete))
            {
                complete.TrySetResult();
            }
        }
        return ExportResult.Success;
    }
}
