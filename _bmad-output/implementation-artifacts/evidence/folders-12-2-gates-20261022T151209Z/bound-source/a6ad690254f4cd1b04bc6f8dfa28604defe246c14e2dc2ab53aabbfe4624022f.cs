using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Represents readable projection wire events or one bounded unreadable classification.</summary>
/// <param name="Events">The readable protected-data-safe wire events.</param>
/// <param name="UnreadableReason">The unreadable classification, when any event cannot be read.</param>
/// <param name="SequenceNumber">The unreadable event sequence, when applicable.</param>
internal sealed record ProjectionEventReadabilityResult(
    ProjectionEventDto[]? Events,
    UnreadableProtectedDataReason? UnreadableReason,
    long? SequenceNumber) {
    /// <summary>Creates a readable result.</summary>
    /// <param name="events">The wire events.</param>
    /// <returns>A readable result.</returns>
    public static ProjectionEventReadabilityResult Readable(ProjectionEventDto[] events)
        => new(events, null, null);

    /// <summary>Creates an unreadable result.</summary>
    /// <param name="reason">The bounded unreadable classification.</param>
    /// <param name="sequenceNumber">The affected event sequence.</param>
    /// <returns>An unreadable result.</returns>
    public static ProjectionEventReadabilityResult Unreadable(
        UnreadableProtectedDataReason reason,
        long sequenceNumber)
        => new(null, reason, sequenceNumber);
}
