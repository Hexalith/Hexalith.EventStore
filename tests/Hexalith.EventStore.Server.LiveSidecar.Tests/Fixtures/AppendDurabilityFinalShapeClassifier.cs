namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Classifies the final persisted stream shape observed after both Story 4.5 contenders quiesce.
/// Every anomaly gets its own recorded name, so a torn interleaving is captured verbatim rather
/// than collapsing into a bare pass/fail; <see cref="IsSound"/> then names the subset the reviewed
/// profile must not exhibit, which is what the live test asserts.
/// </summary>
public static class AppendDurabilityFinalShapeClassifier
{
    /// <summary>Facts read from Redis after both writers quiesce.</summary>
    /// <param name="FinalStateFullyRead">Whether every final read, including the one-past-the-end probe, completed.</param>
    /// <param name="FinalSequenceWithinBounds">Whether the metadata sequence is inside the bounded 0..2 range.</param>
    /// <param name="FinalSequence">The metadata sequence, or zero when metadata is absent.</param>
    /// <param name="MetadataPresent">Whether an aggregate metadata record was read.</param>
    /// <param name="EventSequenceNumbers">The sequence numbers of the events actually read, in read order.</param>
    /// <param name="EventMessageIds">The message ids of the events actually read, in read order.</param>
    /// <param name="UnexpectedNextEventPresent">Whether an event exists one past the metadata sequence.</param>
    /// <param name="AllEventsMatchAggregateIdentity">Whether every read event carries the probed aggregate identity.</param>
    /// <param name="ExactContendersOnly">Whether every read event matches one of the two exact contender identities.</param>
    /// <param name="MetadataLastModifiedMatchesLastEvent">Whether metadata's last-modified equals the last event timestamp.</param>
    public sealed record Input(
        bool FinalStateFullyRead,
        bool FinalSequenceWithinBounds,
        long FinalSequence,
        bool MetadataPresent,
        IReadOnlyList<long> EventSequenceNumbers,
        IReadOnlyList<string> EventMessageIds,
        bool UnexpectedNextEventPresent,
        bool AllEventsMatchAggregateIdentity,
        bool ExactContendersOnly,
        bool MetadataLastModifiedMatchesLastEvent);

    /// <summary>
    /// The classifications the reviewed Dapr <c>1.18.1</c> / <c>state.redis</c> / <c>redis:6</c>
    /// profile must not exhibit. A run producing any of these is a durability anomaly and turns the
    /// named invariant red; the classification is still recorded in full either way.
    /// </summary>
    public static readonly IReadOnlySet<string> UnsoundClassifications = new HashSet<string>(StringComparer.Ordinal)
    {
        "unclassified-final-shape",
        "final-sequence-out-of-bounds",
        "events-without-metadata",
        "metadata-sequence-without-matching-events",
        "non-contiguous-event-sequence",
        "duplicate-event-message-ids",
        "event-beyond-metadata-sequence",
        "foreign-aggregate-identity-present",
        "foreign-writer-present",
        "metadata-timestamp-mismatch",
    };

    /// <summary>Indicates whether a classification is one the reviewed profile may exhibit.</summary>
    /// <param name="classification">A name returned by <see cref="Classify"/>.</param>
    /// <returns><see langword="true"/> when the shape is sound.</returns>
    public static bool IsSound(string classification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classification);
        return !UnsoundClassifications.Contains(classification);
    }

    /// <summary>Classifies one observed final shape.</summary>
    /// <param name="input">The observed facts.</param>
    /// <returns>The deterministic classification name.</returns>
    public static string Classify(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.FinalSequenceWithinBounds)
        {
            return "final-sequence-out-of-bounds";
        }

        if (!input.FinalStateFullyRead)
        {
            return "unclassified-final-shape";
        }

        if (!input.MetadataPresent)
        {
            return input.EventSequenceNumbers.Count == 0 && !input.UnexpectedNextEventPresent
                ? "no-metadata-no-events"
                : "events-without-metadata";
        }

        if (input.EventSequenceNumbers.Count != input.FinalSequence)
        {
            return "metadata-sequence-without-matching-events";
        }

        if (!input.EventSequenceNumbers.SequenceEqual(
            Enumerable.Range(1, input.EventSequenceNumbers.Count).Select(value => (long)value)))
        {
            return "non-contiguous-event-sequence";
        }

        if (input.EventMessageIds.Distinct(StringComparer.Ordinal).Count() != input.EventMessageIds.Count)
        {
            return "duplicate-event-message-ids";
        }

        if (input.UnexpectedNextEventPresent)
        {
            return "event-beyond-metadata-sequence";
        }

        if (!input.AllEventsMatchAggregateIdentity)
        {
            return "foreign-aggregate-identity-present";
        }

        if (!input.ExactContendersOnly)
        {
            return "foreign-writer-present";
        }

        if (input.FinalSequence > 0 && !input.MetadataLastModifiedMatchesLastEvent)
        {
            return "metadata-timestamp-mismatch";
        }

        return $"gapless-{input.FinalSequence}-event-stream";
    }
}
