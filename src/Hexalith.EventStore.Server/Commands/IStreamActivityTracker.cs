namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Tracks stream activity for the admin stream activity index.
/// Implementations are advisory -- failures must not block command processing (Rule 12).
/// </summary>
public interface IStreamActivityTracker {
    /// <summary>
    /// Records stream activity in the admin activity index so it appears in the admin UI Streams and Events pages.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="newEventsAppended">The number of new events produced by this command.</param>
    /// <param name="timestamp">The timestamp of the activity.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        DateTimeOffset timestamp,
        CancellationToken ct = default);
}
