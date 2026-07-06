namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Stores idempotency markers for consumed EventStore domain events.
/// </summary>
public interface IEventStoreDomainEventMarkerStore {
    /// <summary>
    /// Attempts to acquire processing ownership for an EventStore message ID.
    /// </summary>
    /// <param name="messageId">The EventStore event message ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The marker acquisition result.</returns>
    Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an EventStore message ID as completed after it was handled or terminally skipped.
    /// </summary>
    /// <param name="messageId">The EventStore event message ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a marker after a retryable processing failure.
    /// </summary>
    /// <param name="messageId">The EventStore event message ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default);
}
