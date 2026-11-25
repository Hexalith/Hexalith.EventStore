namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>A row recording a counter projection response.</summary>
/// <param name="Timestamp">The local time of the refresh.</param>
/// <param name="Count">The recorded count.</param>
/// <param name="ETag">The display prefix of the projection validator.</param>
internal sealed record CounterHistoryEntry(DateTime Timestamp, int Count, string ETag);
