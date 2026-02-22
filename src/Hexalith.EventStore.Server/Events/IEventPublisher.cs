namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Publishes persisted events to a DAPR pub/sub component with CloudEvents 1.0 envelope format (FR17).
/// </summary>
public interface IEventPublisher {
    /// <summary>
    /// Publishes persisted events to the pub/sub topic derived from the aggregate identity.
    /// Each event is wrapped in a CloudEvents 1.0 envelope via DAPR's native wrapping.
    /// Does NOT throw on publication failure -- returns a failure result instead.
    /// </summary>
    /// <param name="identity">The aggregate identity providing topic derivation.</param>
    /// <param name="events">The persisted event envelopes to publish.</param>
    /// <param name="correlationId">The correlation ID for tracing (rule #9).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publication result indicating success/failure and count of published events.</returns>
    Task<EventPublishResult> PublishEventsAsync(
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        string correlationId,
        CancellationToken cancellationToken = default);
}
