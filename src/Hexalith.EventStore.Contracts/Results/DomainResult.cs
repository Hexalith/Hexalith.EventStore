namespace Hexalith.EventStore.Contracts.Results;

using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Wraps the result of a domain service command processing. Contains an immutable list of
/// <see cref="IEventPayload"/> instances representing success events, rejection events, or no-op.
/// Mixed results (both regular and rejection events) are invalid and rejected at construction.
/// </summary>
public record DomainResult {
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainResult"/> record.
    /// </summary>
    /// <param name="events">The event payloads produced by the domain service.</param>
    /// <exception cref="ArgumentNullException">Thrown when events is null.</exception>
    /// <exception cref="ArgumentException">Thrown when events contain both IRejectionEvent and non-IRejectionEvent instances.</exception>
    public DomainResult(IReadOnlyList<IEventPayload> events) {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count > 0) {
            bool hasRejections = false;
            bool hasRegular = false;

            foreach (IEventPayload e in events) {
                if (e is IRejectionEvent) {
                    hasRejections = true;
                }
                else {
                    hasRegular = true;
                }

                if (hasRejections && hasRegular) {
                    throw new ArgumentException(
                        "Events cannot contain both IRejectionEvent and non-IRejectionEvent instances. " +
                        "A command either succeeds (regular events), is rejected (rejection events), or is a no-op (empty).",
                        nameof(events));
                }
            }
        }

        Events = events;
    }

    /// <summary>Gets the immutable list of event payloads.</summary>
    public IReadOnlyList<IEventPayload> Events { get; }

    /// <summary>Gets a value indicating whether the result represents a successful command (non-empty, no rejections).</summary>
    public bool IsSuccess => Events.Count > 0 && Events[0] is not IRejectionEvent;

    /// <summary>Gets a value indicating whether the result represents a domain rejection (non-empty, all rejections).</summary>
    public bool IsRejection => Events.Count > 0 && Events[0] is IRejectionEvent;

    /// <summary>Gets a value indicating whether the result is a no-op (empty events list — command acknowledged, no state change).</summary>
    public bool IsNoOp => Events.Count == 0;

    /// <summary>Creates a success result from regular event payloads.</summary>
    /// <param name="events">One or more regular event payloads.</param>
    /// <returns>A <see cref="DomainResult"/> with IsSuccess = true.</returns>
    /// <exception cref="ArgumentException">Thrown when events is empty.</exception>
    public static DomainResult Success(IReadOnlyList<IEventPayload> events) {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0) {
            throw new ArgumentException("Success result requires at least one event.", nameof(events));
        }

        return new DomainResult(events);
    }

    /// <summary>Creates a rejection result from rejection event payloads.</summary>
    /// <param name="rejectionEvents">One or more rejection events.</param>
    /// <returns>A <see cref="DomainResult"/> with IsRejection = true.</returns>
    /// <exception cref="ArgumentException">Thrown when rejectionEvents is empty.</exception>
    public static DomainResult Rejection(IReadOnlyList<IRejectionEvent> rejectionEvents) {
        ArgumentNullException.ThrowIfNull(rejectionEvents);

        if (rejectionEvents.Count == 0) {
            throw new ArgumentException("Rejection result requires at least one rejection event.", nameof(rejectionEvents));
        }

        // IRejectionEvent extends IEventPayload, so cast to the base list
        var events = new IEventPayload[rejectionEvents.Count];
        for (int i = 0; i < rejectionEvents.Count; i++) {
            events[i] = rejectionEvents[i];
        }

        return new DomainResult(events);
    }

    /// <summary>Creates a no-op result (empty events list — command acknowledged, no state change).</summary>
    /// <returns>A <see cref="DomainResult"/> with IsNoOp = true.</returns>
    public static DomainResult NoOp() => new(Array.Empty<IEventPayload>());
}
