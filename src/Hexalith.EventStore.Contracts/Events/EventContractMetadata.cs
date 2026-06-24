namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Immutable container for resolved event contract metadata.
/// Produced by event contract resolvers from <see cref="IEventContract"/> implementations.
/// </summary>
/// <param name="EventType">The event type name (kebab-case routing key).</param>
/// <param name="Domain">The owning domain name.</param>
public record EventContractMetadata(
    string EventType,
    string Domain);
