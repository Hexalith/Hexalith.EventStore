namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>
/// Per-event aggregate state snapshot captured during a replay request that opted into
/// timeline mode. Used by Admin surfaces (Blame, Step Through) that need state-after-each-event
/// without paying for one HTTP round trip per event.
/// </summary>
/// <param name="SequenceNumber">Stream sequence number of the event whose application produced this state.</param>
/// <param name="EventTypeName">Event type that was applied to reach this state.</param>
/// <param name="StateJson">State JSON serialized with the runtime serializer options after the event was applied.</param>
public sealed record AggregateReconstructionTimelineEntry(
    long SequenceNumber,
    string EventTypeName,
    string StateJson);
