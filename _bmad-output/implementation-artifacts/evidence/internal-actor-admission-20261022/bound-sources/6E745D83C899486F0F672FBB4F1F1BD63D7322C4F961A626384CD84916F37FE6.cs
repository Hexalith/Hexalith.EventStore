using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Aggregated health history for DAPR components over a time range.
/// </summary>
/// <param name="Entries">The health history entries.</param>
/// <param name="HasData">Whether any data is available.</param>
/// <param name="IsTruncated">Whether results were truncated due to entry cap.</param>
/// <param name="HistoryStatus">
/// Whether the history source was readable for this query. <see cref="SystemHealthMetricStatus.Available"/>
/// with <paramref name="HasData"/> false means the source was read successfully but contained no entries.
/// <see cref="SystemHealthMetricStatus.Unavailable"/> means the source could not be read.
/// </param>
/// <param name="StatusMessage">Human-readable explanation for unavailable or stale history evidence.</param>
public record DaprComponentHealthTimeline(
    IReadOnlyList<DaprHealthHistoryEntry> Entries,
    bool HasData,
    bool IsTruncated = false,
    SystemHealthMetricStatus HistoryStatus = SystemHealthMetricStatus.Unavailable,
    string? StatusMessage = null) {
    /// <summary>
    /// Gets an empty timeline indicating no data is available.
    /// </summary>
    public static DaprComponentHealthTimeline Empty { get; } = new([], false);
}
