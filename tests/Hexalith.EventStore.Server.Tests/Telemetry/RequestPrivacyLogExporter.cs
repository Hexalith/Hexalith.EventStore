using System.Collections.Concurrent;
using System.Text;

using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Snapshots every exported value before OpenTelemetry recycles the log record.</summary>
internal sealed class RequestPrivacyLogExporter : BaseExporter<LogRecord>
{
    /// <summary>Gets formatted messages, structured attributes, exceptions, and scopes at the export boundary.</summary>
    internal ConcurrentQueue<string> Records { get; } = new();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (LogRecord record in batch)
        {
            StringBuilder snapshot = new();
            snapshot.Append(record.CategoryName).Append('|').Append(record.LogLevel).Append('|')
                .Append(record.FormattedMessage).Append('|').Append(record.Body).Append('|').Append(record.Exception);
            foreach (KeyValuePair<string, object?> attribute in record.Attributes ?? [])
                snapshot.Append('|').Append(attribute.Key).Append('=').Append(attribute.Value);
            record.ForEachScope(static (scope, text) =>
            {
                foreach (KeyValuePair<string, object?> value in scope)
                    text.Append('|').Append(value.Key).Append('=').Append(value.Value);
            }, snapshot);
            Records.Enqueue(snapshot.ToString());
        }
        return ExportResult.Success;
    }
}
