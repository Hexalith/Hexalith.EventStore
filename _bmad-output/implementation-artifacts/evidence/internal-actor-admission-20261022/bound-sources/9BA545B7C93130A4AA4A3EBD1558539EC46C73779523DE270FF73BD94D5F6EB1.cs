namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Defines mandatory event metadata as typed static members.
/// Implement this interface on domain event payloads or adapters to get compile-time
/// safety for event routing metadata without coupling to a specific bounded context.
/// </summary>
public interface IEventContract : IEventPayload {
    /// <summary>
    /// Gets the event type discriminator used for routing and diagnostics.
    /// Must be kebab-case, no colons (reserved as actor ID separator).
    /// Example: "counter-created".
    /// </summary>
    static abstract string EventType { get; }

    /// <summary>
    /// Gets the owning domain name (kebab-case).
    /// Example: "counter".
    /// </summary>
    static abstract string Domain { get; }

    /// <summary>
    /// Gets the aggregate id this event belongs to.
    /// </summary>
    string AggregateId { get; }
}
