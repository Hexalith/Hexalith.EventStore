namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Type of entry in a stream timeline.
/// </summary>
public enum TimelineEntryType
{
    /// <summary>A command was submitted.</summary>
    Command,

    /// <summary>An event was produced.</summary>
    Event,

    /// <summary>A query was executed.</summary>
    Query,
}
