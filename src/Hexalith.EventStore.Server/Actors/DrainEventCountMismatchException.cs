namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Thrown when an `UnpublishedEventsRecord`'s persisted `EventCount` does not match its
/// `EndSequence - StartSequence + 1` range. The drain reminder converts this into the
/// stable activity reason code `drain_event_count_mismatch` via type-match (not message
/// prefix), so renaming the exception message will not regress the diagnostic vocabulary.
/// </summary>
internal sealed class DrainEventCountMismatchException : InvalidOperationException {
    public DrainEventCountMismatchException(
        string actorId,
        long startSequence,
        long endSequence,
        long eventCount,
        long expectedEventCount)
        : base(
            $"Drain record EventCount mismatch for {actorId}: startSequence={startSequence}, "
            + $"endSequence={endSequence}, eventCount={eventCount}, "
            + $"expectedEventCount={expectedEventCount}.") {
    }
}
