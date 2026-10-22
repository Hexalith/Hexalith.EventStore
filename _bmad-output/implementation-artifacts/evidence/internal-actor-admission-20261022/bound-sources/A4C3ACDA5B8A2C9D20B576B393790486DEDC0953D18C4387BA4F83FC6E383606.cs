namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Marker interface for domain rejection events (D3). Extends <see cref="IEventPayload"/>
/// for programmatic identification of domain rejection events. Has no additional members.
/// </summary>
public interface IRejectionEvent : IEventPayload;
