namespace Hexalith.EventStore.Admin.UI.Models;

/// <summary>
/// Represents a time bucket for the activity chart showing active stream count per period.
/// </summary>
/// <param name="Start">The start of the time bucket.</param>
/// <param name="End">The end of the time bucket.</param>
/// <param name="StreamCount">The number of streams with activity in this bucket.</param>
public record ActivityBucket(DateTimeOffset Start, DateTimeOffset End, int StreamCount);
