namespace Hexalith.EventStore.Testing.Fakes;

using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

/// <summary>
/// Fake implementation of <see cref="IEventPublisher"/> for unit testing.
/// Tracks all publish calls for test assertions and supports configurable failure modes.
/// Thread-safe for concurrent multi-tenant verification (Story 4.2).
/// </summary>
public sealed class FakeEventPublisher : IEventPublisher {
    private readonly ConcurrentBag<PublishCall> _publishCalls = [];
    private readonly ConcurrentDictionary<string, ConcurrentBag<EventEnvelope>> _eventsByTopic = new();
    private int? _failOnEventIndex;
    private string? _failureMessage;
    private int _publishedEventCount;

    /// <summary>Gets the list of all publish calls for test assertions.</summary>
    public IReadOnlyList<PublishCall> PublishCalls => [.. _publishCalls];

    /// <summary>Gets the total number of events published across all calls.</summary>
    public int TotalEventsPublished => _publishedEventCount;

    /// <summary>
    /// Gets all unique topic names that events have been published to.
    /// </summary>
    /// <returns>A collection of distinct topic names.</returns>
    public IReadOnlyList<string> GetPublishedTopics()
        => [.. _eventsByTopic.Keys.Order()];

    /// <summary>
    /// Gets all events published to a specific topic.
    /// </summary>
    /// <param name="topic">The topic name to query.</param>
    /// <returns>The events published to the specified topic, or an empty list if none.</returns>
    public IReadOnlyList<EventEnvelope> GetEventsForTopic(string topic) {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return _eventsByTopic.TryGetValue(topic, out ConcurrentBag<EventEnvelope>? events)
            ? [.. events]
            : [];
    }

    /// <summary>
    /// Asserts that no events were published to the specified topic.
    /// </summary>
    /// <param name="topic">The topic that should have zero events.</param>
    /// <exception cref="InvalidOperationException">Thrown when events were found on the specified topic.</exception>
    public void AssertNoEventsForTopic(string topic) {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (_eventsByTopic.TryGetValue(topic, out ConcurrentBag<EventEnvelope>? events) && !events.IsEmpty) {
            throw new InvalidOperationException(
                $"Expected no events for topic '{topic}', but found {events.Count} event(s).");
        }
    }

    /// <summary>
    /// Configures the fake to always return failure with the specified message.
    /// </summary>
    /// <param name="failureMessage">The failure reason message.</param>
    public void SetupFailure(string failureMessage = "Pub/sub unavailable") {
        _failureMessage = failureMessage;
        _failOnEventIndex = null;
    }

    /// <summary>
    /// Configures the fake to fail on a specific event index (0-based), simulating partial failure.
    /// Events before the failure index are "published" successfully.
    /// </summary>
    /// <param name="eventIndex">The 0-based index of the event that should fail.</param>
    /// <param name="failureMessage">The failure reason message.</param>
    public void SetupPartialFailure(int eventIndex, string failureMessage = "Partial publication failure") {
        _failOnEventIndex = eventIndex;
        _failureMessage = failureMessage;
    }

    /// <inheritdoc/>
    public Task<EventPublishResult> PublishEventsAsync(
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string topic = identity.PubSubTopic;
        _publishCalls.Add(new PublishCall(identity, events, correlationId, topic));

        int publishedCount = events.Count;
        bool success = true;
        string? failureReason = null;

        if (_failOnEventIndex.HasValue && _failOnEventIndex.Value < events.Count) {
            publishedCount = _failOnEventIndex.Value;
            success = false;
            failureReason = _failureMessage;
        }
        else if (_failureMessage is not null && _failOnEventIndex is null) {
            publishedCount = 0;
            success = false;
            failureReason = _failureMessage;
        }

        // Track only events that were actually published.
        if (publishedCount > 0) {
            ConcurrentBag<EventEnvelope> topicEvents = _eventsByTopic.GetOrAdd(topic, _ => []);
            for (int i = 0; i < publishedCount; i++) {
                topicEvents.Add(events[i]);
            }
        }

        _ = Interlocked.Add(ref _publishedEventCount, publishedCount);
        return Task.FromResult(new EventPublishResult(success, publishedCount, failureReason));
    }

    /// <summary>
    /// Record of a single publish call for test assertions.
    /// </summary>
    /// <param name="Identity">The aggregate identity used.</param>
    /// <param name="Events">The events that were published.</param>
    /// <param name="CorrelationId">The correlation ID used.</param>
    /// <param name="Topic">The topic derived from the identity.</param>
    public record PublishCall(
        AggregateIdentity Identity,
        IReadOnlyList<EventEnvelope> Events,
        string CorrelationId,
        string Topic);
}
