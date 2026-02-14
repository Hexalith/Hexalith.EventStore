namespace Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

/// <summary>
/// Fake implementation of <see cref="IEventPublisher"/> for unit testing.
/// Tracks all publish calls for test assertions and supports configurable failure modes.
/// </summary>
public sealed class FakeEventPublisher : IEventPublisher
{
    private readonly List<PublishCall> _publishCalls = [];
    private int? _failOnEventIndex;
    private string? _failureMessage;

    /// <summary>Gets the list of all publish calls for test assertions.</summary>
    public IReadOnlyList<PublishCall> PublishCalls => _publishCalls;

    /// <summary>Gets the total number of events published across all calls.</summary>
    public int TotalEventsPublished => _publishCalls.Sum(c => c.Events.Count);

    /// <summary>
    /// Configures the fake to always return failure with the specified message.
    /// </summary>
    /// <param name="failureMessage">The failure reason message.</param>
    public void SetupFailure(string failureMessage = "Pub/sub unavailable")
    {
        _failureMessage = failureMessage;
        _failOnEventIndex = null;
    }

    /// <summary>
    /// Configures the fake to fail on a specific event index (0-based), simulating partial failure.
    /// Events before the failure index are "published" successfully.
    /// </summary>
    /// <param name="eventIndex">The 0-based index of the event that should fail.</param>
    /// <param name="failureMessage">The failure reason message.</param>
    public void SetupPartialFailure(int eventIndex, string failureMessage = "Partial publication failure")
    {
        _failOnEventIndex = eventIndex;
        _failureMessage = failureMessage;
    }

    /// <inheritdoc/>
    public Task<EventPublishResult> PublishEventsAsync(
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _publishCalls.Add(new PublishCall(identity, events, correlationId, identity.PubSubTopic));

        if (_failOnEventIndex.HasValue && _failOnEventIndex.Value < events.Count)
        {
            return Task.FromResult(new EventPublishResult(false, _failOnEventIndex.Value, _failureMessage));
        }

        if (_failureMessage is not null && _failOnEventIndex is null)
        {
            return Task.FromResult(new EventPublishResult(false, 0, _failureMessage));
        }

        return Task.FromResult(new EventPublishResult(true, events.Count, null));
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
