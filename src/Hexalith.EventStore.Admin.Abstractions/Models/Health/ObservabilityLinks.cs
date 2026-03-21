namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Deep-link URLs to observability dashboards (ADR-P5).
/// </summary>
/// <param name="TraceUrl">URL to the distributed tracing dashboard, or null if not configured.</param>
/// <param name="MetricsUrl">URL to the metrics dashboard, or null if not configured.</param>
/// <param name="LogsUrl">URL to the log aggregation dashboard, or null if not configured.</param>
public record ObservabilityLinks(string? TraceUrl, string? MetricsUrl, string? LogsUrl);
