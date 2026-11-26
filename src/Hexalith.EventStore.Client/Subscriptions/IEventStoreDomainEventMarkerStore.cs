namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Stores idempotency markers for consumed EventStore domain events.
/// </summary>
public interface IEventStoreDomainEventMarkerStore {
    /// <summary>Gets whether this store enforces content-bound, fenced admission.</summary>
    bool SupportsContentBoundAdmission => false;

    /// <summary>Gets whether the store persists content-bound markers across process replacement.</summary>
    bool IsDurable => false;

    /// <summary>Acquires the marker for the received event content.</summary>
    /// <param name="messageId">The persisted event identifier.</param>
    /// <param name="fingerprint">The fingerprint of the admitted event envelope.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The admission result and any lease needed to transition it.</returns>
    Task<EventStoreDomainEventMarkerAdmission> TryAcquireContentAsync(
        string messageId, string fingerprint, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The marker store does not provide content-bound admission.");

    /// <summary>Records proven handler dispatch under the supplied lease.</summary>
    /// <param name="lease">The fenced marker lease.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>Whether a completion transition remains.</returns>
    Task<bool> MarkFencedDispatchedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The marker store does not provide fenced dispatch.");

    /// <summary>Completes a marker after dispatch has been proven.</summary>
    /// <param name="lease">The fenced marker lease.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The completion operation.</returns>
    Task MarkFencedCompletedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The marker store does not provide fenced completion.");

    /// <summary>Releases a marker whose owner can prove handler dispatch never started.</summary>
    /// <param name="lease">The fenced marker lease.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The release operation.</returns>
    Task ReleaseFenceAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The marker store does not provide fenced release.");

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
    /// Durably records that handlers completed successfully and reports whether a separate completion
    /// transition remains.
    /// </summary>
    /// <remarks>
    /// The compatibility implementation completes through <see cref="MarkCompletedAsync(string, CancellationToken)"/>
    /// exactly once. Marker stores that support the durable dispatched phase override this method and return
    /// <see langword="true"/> while completion remains pending.
    /// </remarks>
    /// <param name="messageId">The EventStore event message ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the caller must still mark the message completed; otherwise, <see langword="false"/>.</returns>
    async Task<bool> MarkDispatchedAsync(string messageId, CancellationToken cancellationToken = default) {
        await MarkCompletedAsync(messageId, cancellationToken).ConfigureAwait(false);
        return false;
    }

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
