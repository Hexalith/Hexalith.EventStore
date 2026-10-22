namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Why an <see cref="UnpublishedPublicationIndex.TryAdd"/> call did not append a new entry.
/// The two refusal reasons are deliberately distinguished: at-capacity is an operational condition
/// the caller surfaces as backpressure, while an invalid entry is a data defect that must never be
/// reported as "too many pending commands" with an outstanding count far below the threshold.
/// </summary>
public enum PublicationIndexAddOutcome {
    /// <summary>The entry is tracked (newly appended, or an existing entry refreshed in place).</summary>
    Added = 1,

    /// <summary>The index already holds the configured maximum number of outstanding entries.</summary>
    AtCapacity = 2,

    /// <summary>The entry lacks the message id or correlation id recovery needs.</summary>
    InvalidEntry = 3,
}
