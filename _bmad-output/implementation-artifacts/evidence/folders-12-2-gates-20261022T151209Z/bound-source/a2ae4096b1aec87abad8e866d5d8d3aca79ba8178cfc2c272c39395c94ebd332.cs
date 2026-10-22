using System.Globalization;

using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Security;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

internal static class SafeOutputValueFormatter {
    public static string Format(object? value)
        => value switch {
            null => string.Empty,
            AdminRedactedContent redacted => FormatRedactedContent(redacted),
            string text => SafeText(text),
            Enum e => e.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

    public static string SafeText(string? value)
        => UnsafeMarkerDetection.ContainsUnsafeMarker(value)
            ? AdminRedactedContent.DefaultPlaceholder
            : value ?? string.Empty;

    private static string FormatRedactedContent(AdminRedactedContent redacted) {
        List<string> parts = [
            $"placeholder={redacted.Placeholder}",
            $"contentKind={redacted.ContentKind}",
            $"reasonCode={redacted.ReasonCode}",
            $"stage={redacted.Stage}",
            $"retryable={FormatBool(redacted.Retryable)}",
            $"permanent={FormatBool(redacted.Permanent)}",
            $"safeNextAction={redacted.SafeNextAction}",
        ];

        if (redacted.MetadataVersion is int metadataVersion) {
            parts.Add($"metadataVersion={metadataVersion}");
        }

        AddIfPresent(parts, "tenantId", redacted.TenantId);
        AddIfPresent(parts, "domain", redacted.Domain);
        AddIfPresent(parts, "aggregateId", redacted.AggregateId);
        if (redacted.SequenceNumber is long sequenceNumber) {
            parts.Add($"sequenceNumber={sequenceNumber}");
        }

        AddIfPresent(parts, "correlationId", redacted.CorrelationId);
        return string.Join("; ", parts);
    }

    private static void AddIfPresent(List<string> parts, string name, string? value) {
        if (!string.IsNullOrWhiteSpace(value)) {
            parts.Add($"{name}={value}");
        }
    }

    private static string FormatBool(bool value)
        => value ? "true" : "false";
}
