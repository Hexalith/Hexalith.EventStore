namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Aggregated health history for DAPR components over a time range.
/// </summary>
/// <param name="Entries">The health history entries.</param>
/// <param name="HasData">Whether any data is available.</param>
/// <param name="IsTruncated">Whether results were truncated due to entry cap.</param>
public record DaprComponentHealthTimeline(
    IReadOnlyList<DaprHealthHistoryEntry> Entries,
    bool HasData,
    bool IsTruncated = false) {
    /// <summary>
    /// Gets an empty timeline indicating no data is available.
    /// </summary>
    public static DaprComponentHealthTimeline Empty { get; } = new([], false);
}
